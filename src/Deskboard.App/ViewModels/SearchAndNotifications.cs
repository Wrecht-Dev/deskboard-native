using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.App.Services;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public sealed partial class BlogRowViewModel : ObservableObject
{
    private readonly MainViewModel _host;
    public string Name { get; }
    public string Title { get; }
    public string Description { get; }
    public string Handle { get; }
    public string FollowersLabel { get; }
    [ObservableProperty] private Bitmap? _avatar;

    public BlogRowViewModel(Blog blog, MainViewModel host)
    {
        _host = host;
        Name = blog.Name;
        Title = string.IsNullOrEmpty(blog.Title) ? blog.Name : blog.Title;
        Description = "";
        Handle = blog.Handle;
        FollowersLabel = blog.Followers > 0 ? $"{blog.Followers / 1000.0:0.#}K followers" : "";
        AvatarUrl = blog.Avatar;
    }

    public string AvatarUrl { get; }

    [RelayCommand]
    private Task Open() => _host.OpenBlogAsync(Name);

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(AvatarUrl)) return;
        var bitmap = await ImageLoader.LoadAsync(AvatarUrl, 72);
        if (bitmap is not null) await ImageLoader.ApplyAsync(b => Avatar = b, bitmap);
    }
}

public sealed partial class NotificationItemViewModel : ObservableObject
{
    private readonly MainViewModel _host;
    public string DisplayName { get; }
    public string Action { get; }
    public string TimeAgo { get; }
    public string BlogName { get; }
    public bool NeedsFollowBack { get; }
    [ObservableProperty] private Bitmap? _avatar;
    [ObservableProperty] private bool _isFollowing;
    [ObservableProperty] private bool _isFollowBusy;

    public NotificationItemViewModel(Notification notification, MainViewModel host)
    {
        _host = host;
        BlogName = notification.BlogName;
        DisplayName = notification.DisplayName;
        Action = notification.Action;
        TimeAgo = Humanize(notification.Timestamp);
        NeedsFollowBack = notification.NeedsFollowBack;
        AvatarUrl = notification.AvatarUrl;
    }

    public string AvatarUrl { get; }

    [RelayCommand]
    private Task OpenBlog() => _host.OpenBlogAsync(BlogName);

    [RelayCommand]
    private async Task FollowBack()
    {
        if (IsFollowBusy) return;
        IsFollowBusy = true;
        try
        {
            await _host.FollowBlogAsync(BlogName);
            IsFollowing = true;
        }
        finally { IsFollowBusy = false; }
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(AvatarUrl)) return;
        var bitmap = await ImageLoader.LoadAsync(AvatarUrl, 64);
        if (bitmap is not null) await ImageLoader.ApplyAsync(b => Avatar = b, bitmap);
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
            < 43200 => $"{(int)delta.TotalDays}d",
            _ => DateTimeOffset.FromUnixTimeSeconds(unix).ToString("MMM d"),
        };
    }
}
