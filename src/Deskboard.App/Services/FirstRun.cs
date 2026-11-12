using System;
using System.IO;
using System.Text.Json;
using Deskboard.Core.Api;

namespace Deskboard.App.Services;

/// <summary>Remembers that the welcome screen was shown, so it appears once.</summary>
public static class FirstRun
{
    private sealed class State { public bool WelcomeShown { get; set; } }

    private static string FilePath(string? configDir = null)
        => Path.Combine(configDir ?? Paths.ConfigDir, "app_state.json");

    public static bool WelcomeShown(string? configDir = null)
    {
        try
        {
            var path = FilePath(configDir);
            if (!File.Exists(path)) return false;
            return JsonSerializer.Deserialize<State>(File.ReadAllText(path))?.WelcomeShown ?? false;
        }
        catch (Exception) { return false; }
    }

    public static void MarkWelcomeShown(string? configDir = null)
    {
        try
        {
            var dir = configDir ?? Paths.ConfigDir;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath(dir), JsonSerializer.Serialize(new State { WelcomeShown = true }));
        }
        catch (Exception) { }
    }
}
