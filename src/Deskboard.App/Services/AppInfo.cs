using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Deskboard.App.Services;

public static class AppInfo
{
    public const string Repo = "Wrecht-Dev/deskboard-native";

    public static string Version
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }
            return "0.0.0";
        }
    }

    /// <summary>True when <paramref name="latest"/> is a newer dotted version.</summary>
    public static bool IsNewer(string latest, string current)
    {
        static int[] Parts(string value)
        {
            var cleaned = value.Trim().TrimStart('v', 'V');
            var dash = cleaned.IndexOf('-');
            if (dash > 0) cleaned = cleaned[..dash];
            var parts = cleaned.Split('.');
            var result = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                int.TryParse(parts[i], out result[i]);
            return result;
        }

        var a = Parts(latest);
        var b = Parts(current);
        var length = Math.Max(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }
}

public sealed class UpdateInfo
{
    public string LatestTag { get; init; } = "";
    public string Url { get; init; } = "";
}

public static class UpdateChecker
{
    /// <summary>Checks GitHub's latest release. Network failures are silent.</summary>
    public static async Task<UpdateInfo?> CheckAsync(string currentVersion)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Deskboard-Native/" + currentVersion);
            var json = await http.GetStringAsync(
                $"https://api.github.com/repos/{AppInfo.Repo}/releases/latest").ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            if (tag.Length == 0) return null;
            return new UpdateInfo
            {
                LatestTag = tag,
                Url = url.Length > 0 ? url : $"https://github.com/{AppInfo.Repo}/releases",
            };
        }
        catch (Exception) { return null; }
    }
}
