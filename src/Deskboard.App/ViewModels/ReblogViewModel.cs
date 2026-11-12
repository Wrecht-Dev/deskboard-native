using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public enum ReblogMode { WithText, Now, Schedule }

public sealed partial class ReblogViewModel : ObservableObject
{
    private readonly TumblrClient _client;
    private readonly Post _post;

    public PostItemViewModel Source { get; }
    public ObservableCollection<string> Blogs { get; } = new();

    [ObservableProperty] private string _selectedBlog = "";
    [ObservableProperty] private string _comment = "";
    [ObservableProperty] private string _tags = "";
    [ObservableProperty] private ReblogMode _mode = ReblogMode.WithText;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DateTimeOffset? _publishAt = DateTimeOffset.Now.AddHours(1);

    public string Title => $"Reblog {Source.BlogName}";
    public bool IsWithText => Mode == ReblogMode.WithText;
    public bool IsSchedule => Mode == ReblogMode.Schedule;

    public event EventHandler<bool>? Completed;

    public ReblogViewModel(PostItemViewModel source, TumblrClient client, IEnumerable<string> blogs)
    {
        Source = source;
        _client = client;
        _post = source.Post;
        foreach (var blog in blogs) Blogs.Add(blog);
        SelectedBlog = Blogs.Count > 0 ? Blogs[0] : "";
    }

    partial void OnModeChanged(ReblogMode value)
    {
        OnPropertyChanged(nameof(IsWithText));
        OnPropertyChanged(nameof(IsNow));
        OnPropertyChanged(nameof(IsSchedule));
    }

    public bool IsNow => Mode == ReblogMode.Now;

    [RelayCommand]
    private void SetMode(string mode) => Mode = mode switch
    {
        "now" => ReblogMode.Now,
        "schedule" => ReblogMode.Schedule,
        _ => ReblogMode.WithText,
    };

    [RelayCommand]
    private async Task Reblog()
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(SelectedBlog))
        {
            HasError = true; Error = "Choose a blog to reblog to."; return;
        }
        if (Mode == ReblogMode.Schedule && PublishAt is null)
        {
            HasError = true; Error = "Pick when this should publish."; return;
        }

        IsBusy = true; HasError = false;
        try
        {
            var comment = Mode == ReblogMode.WithText ? Comment : "";
            var tags = Mode == ReblogMode.WithText ? Tags : "";
            var publishOn = Mode == ReblogMode.Schedule
                ? PublishAt?.ToUnixTimeSeconds().ToString()
                : null;
            await _client.ReblogAsync(_post, SelectedBlog, comment, tags, publishOn);
            Completed?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            HasError = true;
            Error = ex is TumblrException ? ex.Message : $"Couldn't reblog: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel() => Completed?.Invoke(this, false);
}
