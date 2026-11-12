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
        public bool BlurAdult { get; set; }
    }

    public static bool BlurAdult
    {
        get => Read()?.BlurAdult ?? false;
        set => Write(new Model { Theme = Theme, BlurAdult = value });
    }

    private static Model? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Model();
            return JsonSerializer.Deserialize<Model>(File.ReadAllText(FilePath)) ?? new Model();
        }
        catch (Exception) { return new Model(); }
    }

    private static void Write(Model model)
    {
        try
        {
            Directory.CreateDirectory(Paths.ConfigDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(model));
        }
        catch (Exception) { }
    }

    private static string FilePath => Path.Combine(Paths.ConfigDir, "app_settings.json");

    public static string Theme
    {
        get => Read()?.Theme is "light" or "dark" ? Read()!.Theme : "dark";
        set => Write(new Model { Theme = value, BlurAdult = BlurAdult });
    }
}
