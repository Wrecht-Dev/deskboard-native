namespace Deskboard.Core.Models;

public sealed class SearchBlogs
{
    public List<Blog> Blogs { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public List<SearchCommunity> Communities { get; init; } = new();
}

public sealed class SearchCommunity
{
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Avatar { get; init; } = "";
    public long Members { get; init; }
}

public sealed class Notification
{
    public string Type { get; init; } = "";
    public long Timestamp { get; init; }
    public string BlogName { get; init; } = "";
    public string BlogTitle { get; init; } = "";
    public string BlogUrl { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public string PostId { get; init; } = "";
    public string ReplyText { get; init; } = "";
    public string AskQuestion { get; init; } = "";
    public bool Followed { get; init; }
    public bool FollowingYou { get; init; }

    public string DisplayName => string.IsNullOrEmpty(BlogTitle) ? BlogName : BlogTitle;

    public string Action => Type switch
    {
        "like" => "liked your post",
        "reblog" => "reblogged your post",
        "reply" => "replied to your post",
        "follow" or "follower" => "started following you",
        "mention" => "mentioned you",
        "ask" or "ask_send" => "sent you an ask",
        "answer" => "answered your ask",
        "post" => "posted",
        "poll" => "voted in your poll",
        _ => Type.Replace('_', ' '),
    };

    public bool NeedsFollowBack => Type is "follow" or "follower" && FollowingYou && !Followed;
}

public sealed class NotificationsPage
{
    public List<Notification> Notifications { get; init; } = new();
    public string? NextUrl { get; init; }
    public int Total { get; init; }
}
