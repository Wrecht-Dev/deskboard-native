using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Deskboard.Core.Api;

namespace Deskboard.App.Services;

/// <summary>
/// A user-defined tab. Unlike the website, a pinned tab can aggregate a saved
/// tag, a search, or all followed tags, and remembers its own Latest/Top sort.
/// </summary>
public sealed class PinnedTab
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "tag";   // tag | search | your-tags
    public string Value { get; set; } = "";     // tag name or query
    public string Sort { get; set; } = "latest"; // latest | top

    public string SortLabel => Sort == "top" ? "Top" : "Latest";

    public string KindLabel => Kind switch
    {
        "search" => "search",
        "your-tags" => "all followed tags",
        _ => "tag",
    };
}

public static class PinnedTabStore
{
    public static string FilePath(string? configDir = null)
        => Path.Combine(configDir ?? Paths.ConfigDir, "pinned_tabs.json");

    public static List<PinnedTab> Load(string? configDir = null)
    {
        try
        {
            var path = FilePath(configDir);
            if (!File.Exists(path)) return Defaults();
            var tabs = JsonSerializer.Deserialize<List<PinnedTab>>(File.ReadAllText(path)) ?? new();
            foreach (var tab in tabs)
            {
                if (tab.Id.Length == 0) tab.Id = Guid.NewGuid().ToString("N");
                if (tab.Sort is not ("latest" or "top")) tab.Sort = "latest";
                if (tab.Kind is not ("tag" or "search" or "your-tags")) tab.Kind = "tag";
            }
            return tabs;
        }
        catch (Exception) { return Defaults(); }
    }

    public static void Save(IEnumerable<PinnedTab> tabs, string? configDir = null)
    {
        try
        {
            var dir = configDir ?? Paths.ConfigDir;
            Directory.CreateDirectory(dir);
            var path = FilePath(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(tabs, new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception) { }
    }

    /// <summary>First run gets one useful tab rather than an empty state.</summary>
    public static List<PinnedTab> Defaults() => new()
    {
        new PinnedTab { Title = "Your tags", Kind = "your-tags", Sort = "latest" },
        new PinnedTab { Title = "#art", Kind = "tag", Value = "art", Sort = "latest" },
    };
}
