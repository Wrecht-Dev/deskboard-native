using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public sealed record FeedOption(string Key, string Label);

/// <summary>One post as the feed template renders it.</summary>
public sealed partial class PostItemViewModel : ObservableObject
{
    private static readonly HttpClient Images = new();

    public Post Post { get; }

    public string BlogName { get; }
    public string Handle { get; }
    public string TimeAgo { get; }
    public int NoteCount { get; }
    public int ReblogCount { get; }
    public int LikeCount { get; }
    public string NotesLabel => NoteCount > 0 ? Short(NoteCount) + " notes" : "";
    public string ReblogLabel => ReblogCount > 0 ? Short(ReblogCount) : "";
    public string LikeLabel => LikeCount > 0 ? Short(LikeCount) : "";
    public string TagsText { get; }
    public bool HasTags { get; }
    public bool HasReblogLabel { get; }
    public string RebloggedFrom { get; }
    public string BodyText { get; }
    public bool HasBody { get; }
    public bool HasImage { get; }
    public string? ImageUrl { get; }
    public double ImageHeight { get; }

    [ObservableProperty] private Bitmap? _avatar;
    [ObservableProperty] private Bitmap? _hero;

    public PostItemViewModel(Post post)
    {
        Post = post;
        BlogName = post.DisplayName;
        Handle = post.Handle;
        TimeAgo = Humanize(post.Timestamp);
        NoteCount = post.NoteCount;
        ReblogCount = post.ReblogCount;
        LikeCount = post.LikeCount;
        TagsText = string.Join("  ", post.Tags.Take(8).Select(t => "#" + t));
        HasTags = TagsText.Length > 0;
        HasReblogLabel = post.IsReblog;
        RebloggedFrom = post.RebloggedFromName.Length > 0
            ? $"reblogged from @{post.RebloggedFromName}"
            : "reblogged";
        BodyText = ExtractText(post);
        HasBody = BodyText.Length > 0;

        var media = post.Content.SelectMany(b => b.Media).FirstOrDefault(m => m.Kind == "image");
        ImageUrl = media?.Url;
        HasImage = media is not null;
        ImageHeight = media is not null && media.Height > 0
            ? Math.Clamp(420.0 * media.Height / Math.Max(1, media.Width), 140, 520)
            : 300;
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

    public async Task LoadImagesAsync()
    {
        if (!string.IsNullOrEmpty(Post.BlogAvatar))
        {
            var bmp = await TryLoad(Post.BlogAvatar, 80);
            if (bmp is not null) await Dispatcher.UIThread.InvokeAsync(() => Avatar = bmp);
        }
        if (ImageUrl is not null)
        {
            var bmp = await TryLoad(ImageUrl, 900);
            if (bmp is not null) await Dispatcher.UIThread.InvokeAsync(() => Hero = bmp);
        }
    }

    private static async Task<Bitmap?> TryLoad(string url, int width)
    {
        try
        {
            using var stream = await Images.GetStreamAsync(url);
            return Bitmap.DecodeToWidth(stream, width);
        }
        catch (Exception) { return null; }
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

    public ObservableCollection<PostItemViewModel> Posts { get; } = new();
    public List<FeedOption> Feeds { get; } = new()
    {
        new("home", "Home"),
        new("following", "Following"),
        new("likes", "Likes"),
        new("explore", "Explore"),
    };

    [ObservableProperty] private FeedOption? _selectedFeed;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "Starting…";
    [ObservableProperty] private string _headline = "Home";
    [ObservableProperty] private string _subhead = "";
    [ObservableProperty] private bool _hasStatus;

    public MainViewModel()
    {
        SelectedFeed = Feeds[0];
        Headline = "Home";
        _ = InitializeAsync();
    }

    partial void OnSelectedFeedChanged(FeedOption? value)
    {
        if (value is null) return;
        Headline = value.Label;
        _ = LoadAsync(value.Key, reset: true);
    }

    private async Task InitializeAsync()
    {
        Status = "Connecting to Tumblr…"; HasStatus = true;
        if (!await _client.EnsureTokenAsync())
        {
            Status = "Couldn't reach Tumblr — check your connection.";
            return;
        }
        await LoadAsync("home", reset: true);
    }

    [RelayCommand]
    public async Task LoadMoreAsync() => await LoadAsync(SelectedFeed?.Key ?? "home", reset: false);

    private async Task LoadAsync(string feed, bool reset)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            Paged<Post> page = !reset && _next is not null
                ? await _client.GetPageAsync(_next)
                : feed switch
                {
                    "following" => await _client.GetFollowingAsync(),
                    "likes" => await _client.GetLikesAsync(),
                    "explore" => await _client.GetExploreAsync(),
                    _ => await _client.GetHomeAsync()
                };

            if (reset)
            {
                Posts.Clear();
                _next = null;
            }

            var added = 0;
            foreach (var post in page.Items)
            {
                Posts.Add(new PostItemViewModel(post));
                added++;
            }
            _next = page.NextUrl;

            Subhead = $"{Headline} · {Posts.Count} posts{(page.Total is { } t ? $" of {t}" : "")}";
            HasStatus = Posts.Count == 0;
            Status = Posts.Count == 0
                ? (_client.HasSession ? "Nothing here yet." : "Signed-out feed only — add cookies to see your dashboard.")
                : "";

            foreach (var item in Posts.TakeLast(added))
                _ = item.LoadImagesAsync();
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
