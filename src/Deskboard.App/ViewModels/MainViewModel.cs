using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.App.Services;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public sealed record FeedOption(string Key, string Label);

public sealed partial class MediaItemViewModel : ObservableObject
{
    public string Url { get; }
    public bool IsGif { get; }
    public double Height { get; }
    [ObservableProperty] private Bitmap? _bitmap;

    public MediaItemViewModel(Media media)
    {
        Url = media.Url;
        IsGif = media.IsGif;
        Height = media.Width > 0 ? Math.Clamp(420.0 * media.Height / media.Width, 120, 420) : 280;
    }

    public async Task LoadAsync(int width)
    {
        var bitmap = await ImageLoader.LoadAsync(Url, width);
        if (bitmap is not null) await ImageLoader.ApplyAsync(b => Bitmap = b, bitmap);
    }
}

public sealed partial class PostItemViewModel : ObservableObject
{
    private readonly MainViewModel _host;

    public Post Post { get; }
    public string BlogName { get; }
    public string Handle { get; }
    public string TimeAgo { get; }
    public int NoteCount { get; }
    public int ReblogCount { get; }
    public string NotesLabel => NoteCount > 0 ? Short(NoteCount) + " notes" : "";
    public string ReblogLabel => ReblogCount > 0 ? Short(ReblogCount) : "";
    public string TagsText { get; }
    public string FirstTag { get; }
    public bool HasTags { get; }
    public bool HasReblogLabel { get; }
    public string RebloggedFrom { get; }
    public string BodyText { get; }
    public bool HasBody { get; }
    public ObservableCollection<MediaItemViewModel> Media { get; } = new();
    public bool HasMedia => Media.Count > 0;
    public bool SingleMedia => Media.Count == 1;
    public bool MultipleMedia => Media.Count > 1;
    public bool HasGif => Media.Any(m => m.IsGif);

    [ObservableProperty] private Bitmap? _avatar;
    [ObservableProperty] private bool _isLiked;
    [ObservableProperty] private string _likeGlyph = "♡";
    [ObservableProperty] private string _likeCountLabel = "";

    public PostItemViewModel(Post post, MainViewModel host)
    {
        Post = post;
        _host = host;
        BlogName = post.DisplayName;
        Handle = post.Handle;
        TimeAgo = Humanize(post.Timestamp);
        NoteCount = post.NoteCount;
        ReblogCount = post.ReblogCount;
        IsLiked = post.IsLiked;
        LikeGlyph = IsLiked ? "♥" : "♡";
        LikeCountLabel = post.LikeCount > 0 ? Short(post.LikeCount) : "";
        TagsText = string.Join("  ", post.Tags.Take(8).Select(t => "#" + t));
        FirstTag = post.Tags.FirstOrDefault() ?? "";
        HasTags = TagsText.Length > 0;
        HasReblogLabel = post.IsReblog;
        RebloggedFrom = post.RebloggedFromName.Length > 0
            ? $"reblogged from @{post.RebloggedFromName}"
            : "reblogged";
        BodyText = ExtractText(post);
        HasBody = BodyText.Length > 0;
        foreach (var media in post.Content.SelectMany(b => b.Media).Where(m => m.Kind == "image").Take(4))
            Media.Add(new MediaItemViewModel(media));
    }

    [RelayCommand] private Task OpenBlog() => _host.OpenBlogAsync(Post.BlogName);
    [RelayCommand] private Task OpenTag() => _host.OpenTagAsync(FirstTag);
    [RelayCommand] private async Task ToggleLike() => await _host.ToggleLikeAsync(this);

    public async Task LoadImagesAsync()
    {
        if (!string.IsNullOrEmpty(Post.BlogAvatar))
        {
            var bmp = await ImageLoader.LoadAsync(Post.BlogAvatar, 80);
            if (bmp is not null) await ImageLoader.ApplyAsync(b => Avatar = b, bmp);
        }
        foreach (var item in Media) _ = item.LoadAsync(SingleMedia ? 900 : 480);
    }

    public void ApplyLike(bool liked)
    {
        IsLiked = liked;
        LikeGlyph = liked ? "♥" : "♡";
        var next = Math.Max(0, Post.LikeCount + (liked ? 1 : 0) + (liked ? 0 : 0));
        var baseCount = Post.IsLiked ? Post.LikeCount : Post.LikeCount;
        next = liked ? baseCount + 1 : Math.Max(0, baseCount - (Post.IsLiked ? 1 : 0));
        LikeCountLabel = next > 0 ? Short(next) : "";
    }

    private static string Short(int n) => n switch
    {
        < 1000 => n.ToString(),
        < 1_000_000 => (n / 1000.0).ToString("0.#") + "K",
        _ => (n / 1_000_000.0).ToString("0.#") + "M"
    };

    private static string ExtractText(Post post)
    {
        var blocks = post.Content.Count > 0 ? post.Content : post.Trail.SelectMany(t => t.Content).ToList();
        var parts = new List<string>();
        foreach (var block in blocks)
        {
            if (block.Type == "text" && !string.IsNullOrWhiteSpace(block.Text))
                parts.Add(block.Subtype == "quote" ? $"“{block.Text.Trim()}”" : block.Text.Trim());
            else if (block.Type == "link" && block.Title.Length > 0)
                parts.Add(block.Title);
        }
        var text = string.Join("\n\n", parts);
        return text.Length > 900 ? text[..900] + "…" : text;
    }

    private static string Humanize(long unix)
    {
        if (unix <= 0) return "";
        var delta = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
        return delta.TotalMinutes switch
        {
            < 1 => "now",
            < 60 => $"{(int)delta.TotalMinutes}m",
            < 1440 => $"{(int)delta.TotalHours}h",
            < 10080 => $"{(int)delta.TotalDays}d",
            _ => DateTimeOffset.FromUnixTimeSeconds(unix).ToString("MMM d")
        };
    }
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly TumblrClient _client = new();
    private string? _next;
    private string _tagSort = "latest";
    private string _blog = "";

    public ObservableCollection<PostItemViewModel> Posts { get; } = new();

    public List<FeedOption> Feeds { get; } = new()
    {
        new("home", "Home"),
        new("following", "Following"),
        new("explore", "Explore"),
        new("likes", "Likes"),
    };

    [ObservableProperty] private FeedOption? _selectedFeed;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private string _headline = "Home";
    [ObservableProperty] private string _subhead = "";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _isTagPage;
    [ObservableProperty] private bool _isLatest = true;
    [ObservableProperty] private bool _isTop;
    [ObservableProperty] private string _identity = "";
    [ObservableProperty] private bool _hasMore;

    public MainViewModel()
    {
        SelectedFeed = Feeds[0];
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        Status = "Connecting to Tumblr…"; HasStatus = true;
        if (!await _client.EnsureTokenAsync())
        {
            Status = "Couldn't reach Tumblr — check your connection.";
            return;
        }
        try
        {
            var (name, blogs) = await _client.GetIdentityAsync();
            _blog = blogs.FirstOrDefault() ?? name;
            Identity = name;
        }
        catch (Exception) { _blog = ""; }
        await LoadFeedAsync();
    }

    partial void OnSelectedFeedChanged(FeedOption? value)
    {
        if (value is null || IsTagPage) return;
        _blog = "";
        Headline = value.Label;
        CanGoBack = false;
        _ = LoadFeedAsync();
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoading || _next is null) return;
        await LoadPageAsync(_next);
    }

    public async Task OpenBlogAsync(string blog)
    {
        if (string.IsNullOrWhiteSpace(blog)) return;
        IsTagPage = false;
        CanGoBack = true;
        Headline = blog;
        Subhead = "loading…";
        await LoadPostsAsync(() => _client.GetBlogPostsAsync(blog));
    }

    public async Task OpenTagAsync(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        IsTagPage = true;
        CanGoBack = true;
        Headline = "#" + tag.TrimStart('#');
        Subhead = "loading…";
        _tagSort = "latest"; IsLatest = true; IsTop = false;
        await LoadPostsAsync(() => _client.GetTaggedAsync(tag, _tagSort));
    }

    [RelayCommand]
    public async Task SetTagSortAsync(string sort)
    {
        if (!IsTagPage) return;
        var tag = Headline.TrimStart('#');
        _tagSort = sort;
        IsLatest = sort == "latest";
        IsTop = sort == "top";
        await LoadPostsAsync(() => _client.GetTaggedAsync(tag, _tagSort));
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        CanGoBack = false;
        IsTagPage = false;
        Headline = SelectedFeed?.Label ?? "Home";
        await LoadFeedAsync();
    }

    private async Task LoadFeedAsync()
    {
        var key = SelectedFeed?.Key ?? "home";
        await LoadPostsAsync(() => key switch
        {
            "following" => _client.GetFollowingAsync(),
            "likes" => _client.GetLikesAsync(),
            "explore" => _client.GetExploreAsync(),
            _ => _client.GetHomeAsync()
        });
    }

    private async Task LoadPostsAsync(Func<Task<Paged<Post>>> fetch)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var page = await fetch();
            Posts.Clear();
            foreach (var post in page.Items) Posts.Add(new PostItemViewModel(post, this));
            _next = page.NextUrl;
            HasMore = _next is not null;
            Subhead = $"{Headline} · {Posts.Count} posts{(page.Total is { } t ? $" of {t}" : "")}";
            HasStatus = Posts.Count == 0;
            Status = Posts.Count == 0
                ? (_client.HasSession ? "Nothing here yet." : "Signed-out feed only.")
                : "";
            foreach (var item in Posts) _ = item.LoadImagesAsync();
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task LoadPageAsync(string url)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var page = await _client.GetPageAsync(url);
            foreach (var post in page.Items)
            {
                var item = new PostItemViewModel(post, this);
                Posts.Add(item);
                _ = item.LoadImagesAsync();
            }
            _next = page.NextUrl;
            HasMore = _next is not null;
            Subhead = $"{Headline} · {Posts.Count} posts";
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load more: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    public async Task ToggleLikeAsync(PostItemViewModel item)
    {
        if (!_client.HasSession) { Status = "Sign in to like posts."; HasStatus = true; return; }
        var newState = !item.IsLiked;
        item.ApplyLike(newState);
        try
        {
            if (newState) await _client.LikeAsync(item.Post);
            else await _client.UnlikeAsync(item.Post);
        }
        catch (Exception ex)
        {
            item.ApplyLike(!newState);
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't update the like: {ex.Message}";
        }
    }
}
