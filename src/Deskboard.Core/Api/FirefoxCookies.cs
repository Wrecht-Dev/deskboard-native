using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Deskboard.Core.Api;

/// <summary>
/// Reads the user's Tumblr session directly from a local Firefox profile.
/// Tumblr rotates its session cookies, so the app refreshes them from the
/// browser once on a 401 instead of sending the user back to a login screen.
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
            try
            {
                found.AddRange(Directory.GetFiles(root, "cookies.sqlite", SearchOption.AllDirectories));
            }
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
        try
        {
            File.Copy(dbPath, copy, overwrite: true);
        }
        catch (IOException) { return jar; }

        try
        {
            using var connection = new SqliteConnection($"Data Source={copy};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name, value, host FROM moz_cookies WHERE host LIKE '%tumblr%' AND value <> ''";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var value = reader.GetString(1);
                var host = reader.GetString(2).TrimStart('.').ToLowerInvariant();
                if ((host == "tumblr.com" || host.EndsWith(".tumblr.com")) && name.Length > 0)
                    jar[name] = value;
            }
        }
        catch (Exception) { /* locked or unreadable profile */ }
        finally
        {
            try { File.Delete(copy); } catch (IOException) { }
        }
        return jar;
    }
}
