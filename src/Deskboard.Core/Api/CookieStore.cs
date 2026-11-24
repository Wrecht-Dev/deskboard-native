using System.Text.Json;

namespace Deskboard.Core.Api;

/// <summary>
/// Loads the user's Tumblr session cookies from a local store.
///
/// Sources, in order: the DESKBOARD_COOKIES environment variable (a JSON
/// dictionary), the native store, then the legacy Python app's store.
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
        var nativePath = Path.Combine(dataDir ?? Paths.DataDir, "cookies.json");
        candidates.Add(nativePath);
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "deskboard", "cookies.json"));

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                var jar = doc.RootElement.TryGetProperty("cookies", out var c) ? c : doc.RootElement;
                if (jar.ValueKind != JsonValueKind.Object) continue;
                var result = new Dictionary<string, string>();
                foreach (var prop in jar.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString()!.Length > 0)
                        result[prop.Name] = prop.Value.GetString()!;
                if (path == nativePath || result.Count > 0) return result;
            }
            catch (Exception) { /* try the next candidate */ }
        }
        return new Dictionary<string, string>();
    }

    /// <summary>Persist the current session jar for the next launch.</summary>
    public static void SaveStore(IReadOnlyDictionary<string, string> cookies, string? dataDir = null)
    {
        var dir = dataDir ?? Paths.DataDir;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "cookies.json");
        var temp = Path.Combine(dir, $".cookies-{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temp, options))
                JsonSerializer.Serialize(stream, new { cookies });
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
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
            if (!domain.Equals("tumblr.com", StringComparison.OrdinalIgnoreCase) &&
                !domain.EndsWith(".tumblr.com", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields[5].Length > 0 && fields[6].Length > 0) cookies[fields[5]] = fields[6];
        }
        return cookies;
    }
}
