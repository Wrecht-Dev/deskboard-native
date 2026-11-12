using System.Text.Json.Serialization;

namespace Deskboard.Core.Models;

/// <summary>
/// Tumblr delivers modern posts as NPF ("Neue Post Format"): a post has a list of
/// typed content blocks plus a reblog trail of previous contributions.
/// </summary>
public sealed class Post
{
    public string Id { get; init; } = "";
    public string BlogName { get; init; } = "";
    public string BlogTitle { get; init; } = "";
    public string BlogUrl { get; init; } = "";
    public string BlogAvatar { get; init; } = "";
    public string PostUrl { get; init; } = "";
    public long Timestamp { get; init; }
    public List<string> Tags { get; init; } = new();
    public int NoteCount { get; init; }
    public int LikeCount { get; init; }
    public int ReblogCount { get; init; }
    public string ReblogKey { get; init; } = "";
    public bool IsLiked { get; init; }
    public bool CanLike { get; init; } = true;
    public bool CanReblog { get; init; } = true;
    public bool CanReply { get; init; } = true;
    public string Summary { get; init; } = "";
    public string PostType { get; init; } = "blocks";
    public List<Block> Content { get; init; } = new();
    public List<TrailItem> Trail { get; init; } = new();
    public string RebloggedFromName { get; init; } = "";
    public bool IsAdult { get; init; }

    public string DisplayName => string.IsNullOrEmpty(BlogTitle) ? BlogName : BlogTitle;
    public string Handle => $"{BlogName}.tumblr.com";
    public bool IsReblog => Trail.Count > 0 || !string.IsNullOrEmpty(RebloggedFromName);
}

public sealed class TrailItem
{
    public string BlogName { get; init; } = "";
    public string BlogTitle { get; init; } = "";
    public string BlogUrl { get; init; } = "";
    public List<Block> Content { get; init; } = new();
    public string DisplayName => string.IsNullOrEmpty(BlogTitle) ? BlogName : BlogTitle;
}

public sealed class Block
{
    public string Type { get; init; } = "text";
    public string Text { get; init; } = "";
    public string Subtype { get; init; } = "";
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Provider { get; init; } = "";
    public string SiteName { get; init; } = "";
    public List<FormatRange> Formatting { get; init; } = new();
    public List<Media> Media { get; init; } = new();

    public Media? PrimaryMedia => Media.Count > 0 ? Media[0] : null;
}

public sealed class Media
{
    public string Url { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public string Mime { get; init; } = "";
    public string Kind { get; init; } = "image"; // image | video | audio
    public string Poster { get; init; } = "";

    public bool IsGif => Mime.Contains("gif") || Url.Contains(".gif", StringComparison.OrdinalIgnoreCase);
    public double Aspect => Height > 0 ? (double)Width / Height : 1.0;
}

public sealed class FormatRange
{
    [JsonPropertyName("start")] public int Start { get; init; }
    [JsonPropertyName("end")] public int End { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("url")] public string Url { get; init; } = "";
}

public sealed class Blog
{
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public string Url { get; init; } = "";
    public string Avatar { get; init; } = "";
    public long Followers { get; init; }
    public long Posts { get; init; }
    public bool IsFollowing { get; init; }
    public bool Ask { get; init; }
    public bool AskAnon { get; init; }
    public bool CanSubmit { get; init; }

    public string DisplayName => string.IsNullOrEmpty(Title) ? Name : Title;
    public string Handle => $"{Name}.tumblr.com";
}

/// <summary>One page of a paginated endpoint.</summary>
public sealed class Paged<T>
{
    public List<T> Items { get; init; } = new();
    public string? NextUrl { get; init; }
    public long? Total { get; init; }
}
