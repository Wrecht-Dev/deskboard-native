namespace Deskboard.Core.Models;

/// <summary>One activity entry on a post (like, reblog, reply, follow…).</summary>
public sealed class Note
{
    public string Type { get; init; } = "";
    public long Timestamp { get; init; }
    public string BlogName { get; init; } = "";
    public string BlogTitle { get; init; } = "";
    public string BlogUrl { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public bool Followed { get; init; }
    public bool Adult { get; init; }

    public string DisplayName => string.IsNullOrEmpty(BlogTitle) ? BlogName : BlogTitle;

    public string Action => Type switch
    {
        "like" => "liked your post",
        "reblog" => "reblogged your post",
        "reply" => "replied to your post",
        "follow" => "started following you",
        "mention" => "mentioned you",
        "answer" => "answered your question",
        "posted" => "posted this",
        "asked" => "asked",
        _ => Type,
    };
}

public sealed class NotesPage
{
    public List<Note> Notes { get; init; } = new();
    public int Total { get; init; }
    public int TotalLikes { get; init; }
    public int TotalReblogs { get; init; }
    public int TotalReplies { get; init; }
    public string? NextUrl { get; init; }
}
