using System;
using System.IO;
using System.Text.Json;
using Deskboard.Core.Api;

namespace Deskboard.App.Services;

/// <summary>Tiny persisted app preferences (theme etc.).</summary>
public static class AppSettings
{
    private sealed class Model
    {
        public string Theme { get; set; } = "dark";
    }

    private static string FilePath => Path.Combine(Paths.ConfigDir, "app_settings.json");

    public static string Theme
    {
        get
        {
            try
            {
                if (!File.Exists(FilePath)) return "dark";
                var model = JsonSerializer.Deserialize<Model>(File.ReadAllText(FilePath));
                return model?.Theme is "light" or "dark" ? model.Theme : "dark";
            }
            catch (Exception) { return "dark"; }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Paths.ConfigDir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(new Model { Theme = value }));
            }
            catch (Exception) { }
        }
    }
}
