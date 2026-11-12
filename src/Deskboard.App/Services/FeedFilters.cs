using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.Services;

/// <summary>
/// Keyword mute list. Applied client-side to rendered posts (the website only
/// filters tags you explicitly follow), so it works across feeds, DMs-of-posts
/// and pinned tabs alike.
/// </summary>
public sealed class FeedFilter
{
    public List<string> MutedWords { get; set; } = new();
    public bool Enabled { get; set; } = true;

    public static string FilePath(string? configDir = null)
        => Path.Combine(configDir ?? Paths.ConfigDir, "filters.json");

    public static FeedFilter Load(string? configDir = null)
    {
        try
        {
            var path = FilePath(configDir);
            if (!File.Exists(path)) return new FeedFilter();
            var filter = JsonSerializer.Deserialize<FeedFilter>(File.ReadAllText(path)) ?? new FeedFilter();
            filter.MutedWords = filter.MutedWords
                .Select(w => w.Trim())
                .Where(w => w.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return filter;
        }
        catch (Exception) { return new FeedFilter(); }
    }

    public void Save(string? configDir = null)
    {
        try
        {
            var dir = configDir ?? Paths.ConfigDir;
            Directory.CreateDirectory(dir);
            var path = FilePath(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception) { }
    }

    public bool IsMuted(string text, IEnumerable<string> tags)
    {
        if (!Enabled || MutedWords.Count == 0) return false;
        var haystack = string.Join("\n", tags) + "\n" + text;
        return MutedWords.Any(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsMuted(Post post)
    {
        if (!Enabled || MutedWords.Count == 0) return false;
        var parts = new List<string> { post.BlogName, post.Summary };
        parts.AddRange(post.Tags);
        foreach (var block in post.Content) parts.Add(block.Text);
        foreach (var item in post.Trail) parts.AddRange(item.Content.Select(b => b.Text));
        return IsMuted(string.Join("\n", parts.Where(p => !string.IsNullOrEmpty(p))), post.Tags);
    }
}
