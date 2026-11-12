using System.Text.Json;
using System.Text.RegularExpressions;

namespace Deskboard.Core.Api;

/// <summary>
/// Loads the user's Tumblr session cookies from a local store.
///
/// Sources, in order: the DESKBOARD_COOKIES environment variable (a JSON
/// dictionary), the Python app's store (so an existing sign-in carries over), or
/// a Netscape cookies.txt passed as the first CLI argument.
/// </summary>
public static class CookieStore
{
    public static Dictionary<string, string> LoadStore(string? dataDir = null)
    {
        var env = Environment.GetEnvironmentVariable("DESKBOARD_COOKIES");
        if (!string.IsNullOrWhiteSpace(env))
        {
            try
            {
                // An explicit override wins even when empty ("{}" = signed out).
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(env);
                if (parsed is not null) return parsed;
            }
            catch (JsonException) { /* ignore malformed override */ }
        }

        var candidates = new List<string>();
        if (dataDir is not null) candidates.Add(Path.Combine(dataDir, "cookies.json"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "deskboard", "cookies.json"));
        candidates.Add(Path.Combine(Paths.DataDir, "cookies.json"));

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                var jar = doc.RootElement.TryGetProperty("cookies", out var c) ? c : doc.RootElement;
                var result = new Dictionary<string, string>();
                foreach (var prop in jar.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString()!.Length > 0)
                        result[prop.Name] = prop.Value.GetString()!;
                if (result.Count > 0) return result;
            }
            catch (Exception) { /* try the next candidate */ }
        }
        return new Dictionary<string, string>();
    }

    /// <summary>Persist the current session jar for the next launch.</summary>
    public static void SaveStore(IReadOnlyDictionary<string, string> cookies, string? dataDir = null)
    {
        try
        {
            var dir = dataDir ?? Paths.DataDir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "cookies.json");
            var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["source"] = "firefox",
                ["cookies"] = cookies,
            });
            File.WriteAllText(path, json);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception) { }
    }

    /// <summary>Parse a Netscape cookies.txt export, keeping tumblr.com cookies.</summary>
    public static Dictionary<string, string> LoadNetscapeFile(string path)
    {
        var cookies = new Dictionary<string, string>();
        if (!File.Exists(path)) return cookies;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw;
            if (line.StartsWith("#HttpOnly_")) line = line["#HttpOnly_".Length..];
            else if (line.StartsWith('#')) continue;
            var fields = line.Split('\t');
            if (fields.Length < 7) continue;
            var domain = fields[0].TrimStart('.');
            if (!domain.EndsWith("tumblr.com", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields[5].Length > 0 && fields[6].Length > 0) cookies[fields[5]] = fields[6];
        }
        return cookies;
    }
}
