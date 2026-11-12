using System.Text.Json;
using Deskboard.Core.Models;

namespace Deskboard.Core.Api;

internal static class JsonHelpers
{
    public static string Str(this JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    public static long Num(this JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt64(out var i) => i,
            JsonValueKind.String when long.TryParse(v.GetString(), out var s) => s,
            _ => 0
        };
    }

    public static bool Bool(this JsonElement el, string name, bool fallback = false)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }
}

internal static class PostParser
{
    public static Post Parse(JsonElement p)
    {
        var blog = p.TryGetProperty("blog", out var b) && b.ValueKind == JsonValueKind.Object ? b : default;

        var content = p.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array
            ? ParseBlocks(c) : new List<Block>();

        var trail = new List<TrailItem>();
        if (p.TryGetProperty("trail", out var tr) && tr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tr.EnumerateArray())
            {
                var blocks = item.TryGetProperty("content", out var tc) && tc.ValueKind == JsonValueKind.Array
                    ? ParseBlocks(tc) : new List<Block>();
                if (blocks.Count == 0) continue;
                trail.Add(new TrailItem
                {
                    BlogName = item.Str("blogName"),
                    BlogTitle = item.Str("blogTitle"),
                    BlogUrl = item.Str("blogUrl"),
                    Content = blocks,
                });
            }
        }

        var tags = new List<string>();
        if (p.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array)
            foreach (var t in tg.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString()))
                {
                    var s = t.GetString()!.Trim();
                    if (!tags.Contains(s)) tags.Add(s);
                }

        string avatar = "";
        if (blog.ValueKind == JsonValueKind.Object && blog.TryGetProperty("avatar", out var av) &&
            av.ValueKind == JsonValueKind.Array)
        {
            var best = av.EnumerateArray().Where(a => a.Str("url").Length > 0)
                .OrderByDescending(a => a.Num("width")).FirstOrDefault();
            if (best.ValueKind == JsonValueKind.Object) avatar = best.Str("url");
        }

        return new Post
        {
            Id = p.Str("idString") is { Length: > 0 } id ? id : p.Num("id").ToString(),
            BlogName = p.Str("blogName") is { Length: > 0 } bn ? bn : blog.Str("name"),
            BlogTitle = blog.Str("title"),
            BlogUrl = blog.Str("url"),
            BlogAvatar = avatar,
            PostUrl = p.Str("postUrl"),
            Timestamp = p.Num("timestamp"),
            Tags = tags,
            NoteCount = (int)p.Num("noteCount"),
            LikeCount = (int)p.Num("likeCount"),
            ReblogCount = (int)p.Num("reblogCount"),
            ReblogKey = p.Str("reblogKey"),
            IsLiked = p.Bool("liked"),
            CanLike = p.Bool("canLike", true),
            CanReblog = p.Bool("canReblog", true),
            CanReply = p.Bool("canReply", true),
            Summary = p.Str("summary"),
            PostType = p.Str("type") is { Length: > 0 } pt ? pt : "blocks",
            Content = content,
            Trail = trail,
            RebloggedFromName = p.Str("rebloggedFromName"),
        };
    }

    private static List<Block> ParseBlocks(JsonElement array)
    {
        var blocks = new List<Block>();
        foreach (var b in array.EnumerateArray())
        {
            if (b.ValueKind != JsonValueKind.Object) continue;
            var type = b.Str("type");
            var media = ParseMedia(b);
            blocks.Add(new Block
            {
                Type = string.IsNullOrEmpty(type) ? "text" : type,
                Text = b.Str("text"),
                Subtype = b.Str("subtype"),
                Url = b.Str("url") is { Length: > 0 } u ? u : b.Str("embedUrl"),
                Title = b.Str("title"),
                Description = b.Str("description"),
                Provider = b.Str("provider"),
                SiteName = b.Str("siteName"),
                Media = media,
            });
        }
        return blocks;
    }

    private static List<Media> ParseMedia(JsonElement block)
    {
        var list = new List<Media>();
        if (!block.TryGetProperty("media", out var media)) 
        {
            // video/audio sometimes carry a bare url + poster
            if (block.Str("url").Length > 0 && block.Str("type") is "video" or "audio")
                list.Add(new Media { Url = block.Str("url"), Kind = block.Str("type") });
            return list;
        }

        if (media.ValueKind == JsonValueKind.Object) media = Wrap(media);
        if (media.ValueKind != JsonValueKind.Array) return list;

        var best = new Dictionary<string, Media>();
        var order = new List<string>();
        foreach (var m in media.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object) continue;
            var url = m.Str("url");
            if (url.Length == 0) continue;
            var key = m.Str("mediaKey") is { Length: > 0 } mk ? mk : url.Split("/s")[0];
            var candidate = new Media
            {
                Url = url,
                Width = (int)m.Num("width"),
                Height = (int)m.Num("height"),
                Mime = m.Str("type"),
                Kind = block.Str("type") == "video" ? "video" : block.Str("type") == "audio" ? "audio" : "image",
                Poster = block.Str("poster"),
            };
            if (!best.TryGetValue(key, out var existing)) { best[key] = candidate; order.Add(key); }
            else if (candidate.Width * candidate.Height > existing.Width * existing.Height) best[key] = candidate;
        }
        foreach (var k in order) list.Add(best[k]);
        return list;
    }

    private static JsonElement Wrap(JsonElement obj)
    {
        // build a one-element array so callers can enumerate uniformly
        using var doc = JsonDocument.Parse("[" + obj.GetRawText() + "]");
        return doc.RootElement.Clone();
    }
}

internal static class BlogParser
{
    public static Blog Parse(JsonElement root)
    {
        var b = root.TryGetProperty("response", out var r) && r.TryGetProperty("blog", out var blog) ? blog : root;
        string avatar = "";
        if (b.TryGetProperty("avatar", out var av) && av.ValueKind == JsonValueKind.Array)
        {
            var best = av.EnumerateArray().Where(a => a.Str("url").Length > 0)
                .OrderByDescending(a => a.Num("width")).FirstOrDefault();
            if (best.ValueKind == JsonValueKind.Object) avatar = best.Str("url");
        }
        return new Blog
        {
            Name = b.Str("name"),
            Title = b.Str("title"),
            Url = b.Str("url"),
            Avatar = avatar,
            Followers = b.Num("followers"),
            Posts = b.Num("posts"),
            IsFollowing = b.Bool("isFollowing"),
            Ask = b.Bool("ask"),
            AskAnon = b.Bool("askAnon"),
            CanSubmit = b.Bool("canSubmit"),
        };
    }
}
