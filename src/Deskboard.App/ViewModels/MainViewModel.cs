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
    public bool IsVideo { get; }
    public bool IsAudio { get; }
    public string Badge => IsVideo ? "▶" : IsAudio ? "♪" : IsGif ? "GIF" : "";
    public bool HasBadge => Badge.Length > 0;
    public double Height { get; }
    [ObservableProperty] private Bitmap? _bitmap;

    public MediaItemViewModel(Media media)
    {
        // Videos render their poster image (inline playback comes later).
        Url = media.Kind == "video" && media.Poster.Length > 0 ? media.Poster : media.Url;
        IsGif = media.IsGif && media.Kind != "video";
        IsVideo = media.Kind == "video";
        IsAudio = media.Kind == "audio";
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
    public bool IsMine => _host.PrimaryBlog.Length > 0 &&
                          Post.BlogName.Equals(_host.PrimaryBlog, StringComparison.OrdinalIgnoreCase);
    public string RebloggedFrom { get; }
    public string BodyText { get; }
    public bool HasBody { get; }
    public IReadOnlyList<ParagraphViewModel> Paragraphs { get; }
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
        Paragraphs = RichTextBuilder.Build(post);
        HasBody = Paragraphs.Count > 0;
        foreach (var media in post.Content.SelectMany(b => b.Media)
                     .Where(m => m.Kind is "image" or "video" or "audio").Take(4))
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
    private string? _notificationsNext;
    private string _tagSort = "latest";
    private string _blog = "";
    private string _selfBlog = "";

    public ObservableCollection<PostItemViewModel> Posts { get; } = new();
    public ObservableCollection<string> Blogs { get; } = new();
    public ObservableCollection<BlogRowViewModel> SearchBlogs { get; } = new();
    public ObservableCollection<NotificationItemViewModel> Notifications { get; } = new();
    public ObservableCollection<string> SearchTags { get; } = new();
    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = new();
    public ObservableCollection<MessageItemViewModel> ThreadMessages { get; } = new();
    public TumblrClient Client => _client;
    public bool ShowFeed => !IsNotificationsPage && !IsMessagesPage;
    public string PrimaryBlog => _selfBlog.Length > 0 ? _selfBlog : _blog;

    public List<FeedOption> Feeds { get; } = new()
    {
        new("home", "Home"),
        new("following", "Following"),
        new("explore", "Explore"),
        new("likes", "Likes"),
        new("notifications", "Activity"),
        new("messages", "Messages"),
        new("library", "Library"),
    };

    [ObservableProperty] private FeedOption? _selectedFeed;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private string _headline = "Home";
    [ObservableProperty] private string _subhead = "";
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _isTagPage;
    [ObservableProperty] private bool _isNotificationsPage;
    [ObservableProperty] private bool _isMessagesPage;

    partial void OnIsNotificationsPageChanged(bool value) => OnPropertyChanged(nameof(ShowFeed));
    partial void OnIsMessagesPageChanged(bool value) => OnPropertyChanged(nameof(ShowFeed));
    [ObservableProperty] private bool _inThread;
    [ObservableProperty] private bool _showingRequests;
    [ObservableProperty] private bool _isLibraryPage;
    [ObservableProperty] private string _libraryKind = "inbox";
    [ObservableProperty] private string _libraryLabel = "Inbox";
    [ObservableProperty] private ConversationItemViewModel? _selectedConversation;
    [ObservableProperty] private string _threadTitle = "";
    [ObservableProperty] private string _composerText = "";
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _hasSearchMeta;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _isBlogPage;
    [ObservableProperty] private bool _isFollowing;
    [ObservableProperty] private string _blogFollowersLabel = "";
    [ObservableProperty] private bool _canAsk;
    [ObservableProperty] private bool _canSubmitBlog;
    [ObservableProperty] private bool _hasMoreNotifications;
    [ObservableProperty] private bool _canAskAnon;
    [ObservableProperty] private bool _isFollowBusy;
    [ObservableProperty] private bool _isLatest = true;
    [ObservableProperty] private bool _isTop;
    [ObservableProperty] private string _identity = "";
    [ObservableProperty] private bool _hasMore;

    private readonly bool _demoMode;
    public bool DemoMode => _demoMode;

    public MainViewModel()
    {
        _demoMode = Environment.GetEnvironmentVariable("DESKBOARD_DEMO") == "1";
        SelectedFeed = Feeds[0];
        if (_demoMode)
        {
            LoadDemoData();
            return;
        }
        _ = InitializeAsync();
    }

    /// <summary>
    /// Demo mode: synthetic posts so screenshots and UI work never expose a real
    /// account's content. Enabled with DESKBOARD_DEMO=1.
    /// </summary>
    private void LoadDemoData()
    {
        Identity = "demo";
        Headline = "Home";
        Subhead = "Home · demo data";
        Blogs.Add("demo");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var samples = new (string Blog, string Text, string Tags, int Notes)[]
        {
            ("deskboard", "Welcome to Deskboard Native — a native, cross-platform Tumblr client built with .NET and Avalonia. This feed is running on synthetic demo data.", "deskboard, avalonia, dotnet", 128),
            ("deskboard", "The feed is virtualised: only the cards you can see are built, and the next page loads before you reach the bottom.", "performance, desktop", 64),
            ("deskboard", "Everything here is rendered natively: rich text, photosets, animated GIFs, and both light and dark themes. No webview involved.", "native, gtk4, csharp", 42),
        };
        foreach (var (blog, text, tags, notes) in samples)
        {
            var post = new Post
            {
                Id = Guid.NewGuid().ToString("N"),
                BlogName = blog,
                BlogTitle = "Deskboard",
                NoteCount = notes,
                ReblogCount = notes / 4,
                Timestamp = now,
                Tags = tags.Split(", ").ToList(),
                Content = new List<Block> { new() { Type = "text", Text = text } },
            };
            Posts.Add(new PostItemViewModel(post, this));
        }
    }

    public async Task InitializeAsync()
    {
        Status = "Connecting to Tumblr…"; HasStatus = true;
        if (!await _client.EnsureTokenAsync())
        {
            Status = "Couldn't reach Tumblr — check your connection.";
            return;
        }
        for (var attempt = 0; attempt < 2 && Identity.Length == 0; attempt++)
        {
            try
            {
                var (name, blogs) = await _client.GetIdentityAsync();
                _blog = blogs.FirstOrDefault() ?? name;
                _selfBlog = _blog;
                Identity = name;
                Blogs.Clear();
                foreach (var blog in blogs) Blogs.Add(blog);
            }
            catch (Exception)
            {
                if (attempt == 0) await Task.Delay(600);
            }
        }
        await LoadFeedAsync();
    }

    partial void OnSelectedFeedChanged(FeedOption? value)
    {
        if (value is null || IsTagPage) return;
        if (_demoMode)
        {
            Headline = value.Label;
            return;
        }
        _blog = "";
        Headline = value.Label;
        CanGoBack = false;
        IsBlogPage = false;
        IsMessagesPage = false;
        IsLibraryPage = false;
        InThread = false;
        if (value.Key == "notifications")
        {
            IsNotificationsPage = true;
            SearchBlogs.Clear(); HasSearchMeta = false;
            _ = LoadNotificationsAsync();
            return;
        }
        if (value.Key == "library")
        {
            IsNotificationsPage = false;
            IsMessagesPage = false;
            IsLibraryPage = true;
            _ = LoadLibraryAsync("inbox");
            return;
        }
        if (value.Key == "messages")
        {
            IsLibraryPage = false;
            IsNotificationsPage = false;
            IsMessagesPage = true;
            InThread = false;
            SearchBlogs.Clear(); HasSearchMeta = false;
            _ = LoadConversationsAsync();
            return;
        }
        IsNotificationsPage = false;
        _ = LoadFeedAsync();
    }

    public async Task SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        IsTagPage = false;
        IsBlogPage = false;
        IsNotificationsPage = false;
        CanGoBack = true;
        Headline = "Search";
        SearchQuery = query.Trim();
        Subhead = $"results for “{SearchQuery}”";
        SearchBlogs.Clear();
        SearchTags.Clear();
        try
        {
            var blogs = await _client.SearchBlogsAsync(SearchQuery);
            foreach (var blog in blogs.Blogs) SearchBlogs.Add(new BlogRowViewModel(blog, this));
            foreach (var tag in blogs.Tags) SearchTags.Add(tag);
            HasSearchMeta = SearchBlogs.Count > 0 || SearchTags.Count > 0;
            foreach (var row in SearchBlogs) _ = row.LoadAsync();
        }
        catch (Exception) { HasSearchMeta = false; }
        await LoadPostsAsync(() => _client.SearchPostsAsync(SearchQuery));
    }

    public async Task FollowBlogAsync(string blog)
    {
        try
        {
            await _client.FollowAsync(blog);
            Status = $"Following {blog}."; HasStatus = true;
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't follow: {ex.Message}";
        }
    }

    private async Task LoadNotificationsAsync(bool reset = true)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            // Activity needs the signed-in blog name; a cold start may not have it yet.
            await EnsureIdentityAsync();
            if (string.IsNullOrEmpty(PrimaryBlog))
            {
                HasStatus = true;
                Status = "Sign in first — Activity is per blog.";
                return;
            }

            var url = reset ? null : _notificationsNext;
            var page = await _client.GetNotificationsAsync(PrimaryBlog, url);
            if (reset) Notifications.Clear();
            foreach (var n in page.Notifications)
            {
                var vm = new NotificationItemViewModel(n, this);
                Notifications.Add(vm);
                _ = vm.LoadAsync();
            }
            _notificationsNext = page.NextUrl;
            HasMoreNotifications = page.NextUrl is not null;
            Subhead = $"{Notifications.Count} recent";
            HasStatus = Notifications.Count == 0;
            Status = Notifications.Count == 0 ? "No activity yet." : "";
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load activity: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task LoadMoreNotificationsAsync()
    {
        if (IsLoading || _notificationsNext is null) return;
        await LoadNotificationsAsync(reset: false);
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
        IsBlogPage = true;
        IsNotificationsPage = false;
        CanGoBack = true;
        Headline = blog;
        Subhead = "loading…";
        await LoadPostsAsync(() => _client.GetBlogPostsAsync(blog));
        try
        {
            var info = await _client.GetBlogInfoAsync(blog);
            IsFollowing = info.IsFollowing;
            BlogFollowersLabel = info.Followers > 0
                ? $"{info.Followers / 1000.0:0.#}K followers"
                : "";
            CanAsk = info.Ask;
            CanAskAnon = info.AskAnon;
            CanSubmitBlog = info.CanSubmit;
        }
        catch (Exception) { CanAsk = false; }
    }

    [RelayCommand]
    public async Task ToggleFollowAsync()
    {
        if (IsFollowBusy || string.IsNullOrWhiteSpace(Headline)) return;
        IsFollowBusy = true;
        var wanted = !IsFollowing;
        IsFollowing = wanted; // optimistic
        try
        {
            if (wanted) await _client.FollowAsync(Headline);
            else await _client.UnfollowAsync(Headline);
            Status = wanted ? $"Following {Headline}." : $"Unfollowed {Headline}.";
            HasStatus = true;
        }
        catch (Exception ex)
        {
            IsFollowing = !wanted;
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't update follow: {ex.Message}";
        }
        finally { IsFollowBusy = false; }
    }

    public async Task OpenTagAsync(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        IsTagPage = true;
        IsBlogPage = false;
        IsNotificationsPage = false;
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
        IsBlogPage = false;
        IsNotificationsPage = SelectedFeed?.Key == "notifications";
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

    private async Task LoadConversationsAsync(bool requests = false)
    {
        if (IsLoading) return;
        ShowingRequests = requests;
        IsLoading = true;
        try
        {
            Conversations.Clear();
            var page = await _client.GetConversationsAsync(requests: requests);
            foreach (var conversation in page.Conversations)
                Conversations.Add(new ConversationItemViewModel(conversation, this, requests));
            Subhead = $"{Conversations.Count} conversations";
            HasStatus = Conversations.Count == 0;
            Status = Conversations.Count == 0 ? "No messages yet." : "";
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load messages: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task ShowRequestsAsync() => await LoadConversationsAsync(requests: true);

    [RelayCommand]
    public async Task ShowInboxAsync() => await LoadConversationsAsync(requests: false);

    public async Task AcceptRequestAsync(ConversationItemViewModel item)
    {
        var participant = item.Conversation.Participants
            .FirstOrDefault(p => !p.Equals(Identity, StringComparison.OrdinalIgnoreCase))
            ?? item.Conversation.Participants.FirstOrDefault() ?? "";
        try
        {
            await _client.AcceptConversationAsync(item.Conversation.Id, participant);
            Conversations.Remove(item);
            Status = "Request accepted."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't accept: {ex.Message}";
            HasStatus = true;
        }
    }

    public async Task OpenConversationAsync(ConversationItemViewModel item)
    {
        SelectedConversation = item;
        InThread = true;
        ThreadTitle = item.Title;
        ThreadMessages.Clear();
        try
        {
            var messages = await _client.GetMessagesAsync(item.Conversation.Id);
            foreach (var message in messages)
                ThreadMessages.Add(new MessageItemViewModel(message, Identity, item.Conversation));
            Headline = item.Title;
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load the conversation: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (IsSending || string.IsNullOrWhiteSpace(ComposerText) || SelectedConversation is null) return;
        var text = ComposerText.Trim();
        var conversation = SelectedConversation.Conversation;
        var participant = conversation.Participants
            .FirstOrDefault(p => !p.Equals(Identity, StringComparison.OrdinalIgnoreCase));
        if (participant is null) { Status = "No recipient on this conversation."; HasStatus = true; return; }

        IsSending = true;
        try
        {
            await _client.SendMessageAsync(participant, text, conversation.Id);
            ComposerText = "";
            var sent = new Message { Text = text, Participant = Identity, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            ThreadMessages.Add(new MessageItemViewModel(sent, Identity, conversation) { });
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't send: {ex.Message}";
        }
        finally { IsSending = false; }
    }

    [RelayCommand]
    public void BackToConversations()
    {
        InThread = false;
        SelectedConversation = null;
        ThreadMessages.Clear();
        _ = LoadConversationsAsync();
    }

    [RelayCommand]
    public async Task SetLibraryAsync(string kind) => await LoadLibraryAsync(kind);

    public async Task LoadLibraryAsync(string kind)
    {
        if (IsLoading) return;
        LibraryKind = kind;
        LibraryLabel = kind switch { "drafts" => "Drafts", "queue" => "Queue", _ => "Inbox" };
        Headline = LibraryLabel;
        IsLoading = true;
        try
        {
            var page = kind switch
            {
                "drafts" => await _client.GetDraftsAsync(PrimaryBlog),
                "queue" => await _client.GetQueueAsync(PrimaryBlog),
                _ => await _client.GetInboxAsync(),
            };
            Posts.Clear();
            foreach (var post in page.Items) Posts.Add(new PostItemViewModel(post, this));
            _next = page.NextUrl;
            Subhead = $"{LibraryLabel} · {Posts.Count} posts";
            HasStatus = Posts.Count == 0;
            Status = Posts.Count == 0 ? $"Nothing in {LibraryLabel.ToLowerInvariant()}." : "";
            foreach (var item in Posts) _ = item.LoadImagesAsync();
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load {LibraryLabel.ToLowerInvariant()}: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    public async Task DeletePostAsync(PostItemViewModel item)
    {
        try
        {
            await _client.DeletePostAsync(item.Post);
            Posts.Remove(item);
            Status = "Post deleted."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't delete: {ex.Message}";
            HasStatus = true;
        }
    }

    public async Task MutePostAsync(PostItemViewModel item, bool mute = true)
    {
        try
        {
            await _client.MutePostAsync(item.Post, mute);
            Status = mute ? "Post muted." : "Post unmuted."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't mute: {ex.Message}";
            HasStatus = true;
        }
    }

    public async Task BlockBlogAsync(PostItemViewModel item)
    {
        try
        {
            await _client.BlockBlogAsync(item.Post.BlogName);
            Status = $"Blocked {item.Post.BlogName}."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't block: {ex.Message}";
            HasStatus = true;
        }
    }

    public async Task ReportPostAsync(PostItemViewModel item)
    {
        try
        {
            await _client.ReportPostAsync(item.Post);
            Status = "Report sent."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't report: {ex.Message}";
            HasStatus = true;
        }
    }

    public async Task EnsureIdentityAsync()
    {
        if (Identity.Length > 0 && Blogs.Count > 0) return;
        try
        {
            var (name, blogs) = await _client.GetIdentityAsync();
            _blog = blogs.FirstOrDefault() ?? name;
            _selfBlog = _blog;
            Identity = name;
            Blogs.Clear();
            foreach (var blog in blogs) Blogs.Add(blog);
        }
        catch (Exception) { }
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
