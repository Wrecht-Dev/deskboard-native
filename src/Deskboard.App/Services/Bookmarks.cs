using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.Services;

/// <summary>One locally saved post (works offline, independent of likes).</summary>
public sealed class Bookmark
{
    public string PostId { get; set; } = "";
    public string BlogName { get; set; } = "";
    public string Summary { get; set; } = "";
    public string PostUrl { get; set; } = "";
    public long SavedAt { get; set; }
    public int NoteCount { get; set; }

    public string SavedLabel => SavedAt > 0
        ? DateTimeOffset.FromUnixTimeSeconds(SavedAt).ToLocalTime().ToString("MMM d")
        : "";
}

public static class BookmarkStore
{
    public static string FilePath(string? configDir = null)
        => Path.Combine(configDir ?? Paths.ConfigDir, "bookmarks.json");

    public static List<Bookmark> Load(string? configDir = null)
    {
        try
        {
            var path = FilePath(configDir);
            if (!File.Exists(path)) return new List<Bookmark>();
            return JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(path))?
                .Where(b => b.PostId.Length > 0).ToList() ?? new List<Bookmark>();
        }
        catch (Exception) { return new List<Bookmark>(); }
    }

    public static void Save(IEnumerable<Bookmark> bookmarks, string? configDir = null)
    {
        try
        {
            var dir = configDir ?? Paths.ConfigDir;
            Directory.CreateDirectory(dir);
            var path = FilePath(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception) { }
    }

    public static Bookmark FromPost(Post post)
    {
        var text = post.Content.Count > 0 ? post.Content : post.Trail.SelectMany(t => t.Content).ToList();
        var summary = string.Join(" ", text.Where(b => !string.IsNullOrWhiteSpace(b.Text)).Select(b => b.Text.Trim()));
        if (summary.Length > 400) summary = summary[..400];
        return new Bookmark
        {
            PostId = post.Id,
            BlogName = post.BlogName,
            Summary = summary,
            PostUrl = post.PostUrl,
            NoteCount = post.NoteCount,
            SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }
}
