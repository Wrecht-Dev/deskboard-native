using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Deskboard.Core.Api;

/// <summary>
/// Reads the user's Tumblr session directly from a local Firefox profile.
/// Tumblr rotates its session cookies, so the app refreshes them from the
/// browser once on a 401 instead of sending the user back to a login screen.
///
/// Uses the system <c>sqlite3</c> binary against a copy of the profile database:
/// no extra package, no native dependency, and no lock on a live profile.
/// </summary>
public static class FirefoxCookies
{
    public static IReadOnlyList<string> Profiles()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            Path.Combine(home, ".mozilla", "firefox"),
            Path.Combine(home, "snap", "firefox", "common", ".mozilla", "firefox"),
            Path.Combine(home, ".var", "app", "org.mozilla.firefox", ".mozilla", "firefox"),
        };
        var found = new List<string>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            try { found.AddRange(Directory.GetFiles(root, "cookies.sqlite", SearchOption.AllDirectories)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return found;
    }

    /// <summary>Returns tumblr.com cookies, or an empty dictionary.</summary>
    public static Dictionary<string, string> ReadTumblrCookies()
    {
        var best = new Dictionary<string, string>();
        foreach (var db in Profiles())
        {
            var jar = ReadProfile(db);
            if (jar.Count > best.Count) best = jar;
        }
        return best;
    }

    private static Dictionary<string, string> ReadProfile(string dbPath)
    {
        var jar = new Dictionary<string, string>();
        var copy = Path.Combine(Path.GetTempPath(), "deskboard-fx-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try { File.Copy(dbPath, copy, overwrite: true); }
        catch (IOException) { return jar; }

        try
        {
            var start = new ProcessStartInfo("sqlite3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-readonly");
            start.ArgumentList.Add("-separator");
            start.ArgumentList.Add("\t");
            start.ArgumentList.Add(copy);
            start.ArgumentList.Add(
                "SELECT name, value, host FROM moz_cookies WHERE host LIKE '%tumblr%' AND value <> '';");

            using var process = Process.Start(start);
            if (process is null) return jar;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                var (name, value, host) = (parts[0].Trim(), parts[1], parts[2].Trim().TrimStart('.').ToLowerInvariant());
                if (name.Length == 0 || value.Length == 0) continue;
                if (host == "tumblr.com" || host.EndsWith(".tumblr.com")) jar[name] = value;
            }
        }
        catch (Exception) { /* sqlite3 missing or profile unreadable */ }
        finally
        {
            try { File.Delete(copy); } catch (IOException) { }
        }
        return jar;
    }
}
