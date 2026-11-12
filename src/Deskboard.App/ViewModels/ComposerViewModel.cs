using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public sealed partial class ComposerViewModel : ObservableObject
{
    private readonly TumblrClient _client;

    public ObservableCollection<string> Blogs { get; } = new();
    public ObservableCollection<UploadedMedia> Images { get; } = new();

    [ObservableProperty] private string _selectedBlog = "";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _tags = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DateTimeOffset? _publishAt = DateTimeOffset.Now.AddHours(1);
    [ObservableProperty] private bool _showSchedule;

    public string CharacterLabel => $"{Text.Length}";
    public bool CanSubmit => !IsBusy && SelectedBlog.Length > 0 &&
                             (Text.Trim().Length > 0 || Images.Count > 0);

    public event EventHandler<string>? Completed;

    public ComposerViewModel(TumblrClient client, IEnumerable<string> blogs)
    {
        _client = client;
        foreach (var blog in blogs) Blogs.Add(blog);
        SelectedBlog = Blogs.Count > 0 ? Blogs[0] : "";
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(CharacterLabel));
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnSelectedBlogChanged(string value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnShowScheduleChanged(bool value) => OnPropertyChanged(nameof(ShowSchedule));

    public async Task AttachAsync(string path)
    {
        IsBusy = true;
        HasStatus = true;
        Status = "Uploading…";
        try
        {
            var media = await _client.UploadImageAsync(path);
            if (media is null) { Status = "Tumblr didn't accept that image."; return; }
            Images.Add(media);
            OnPropertyChanged(nameof(CanSubmit));
            Status = $"{Path.GetFileName(path)} attached.";
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't upload: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task SubmitAsync(string state, string? publishOn = null)
    {
        if (IsBusy) return;
        if (SelectedBlog.Length == 0) { HasStatus = true; Status = "Choose a blog first."; return; }
        IsBusy = true;
        HasStatus = true;
        Status = state switch
        {
            "queue" => "Adding to queue…",
            "draft" => "Saving draft…",
            "published" when publishOn is not null => "Scheduling…",
            _ => "Posting…",
        };
        try
        {
            var draft = new ComposerDraft { Text = Text, Tags = Tags };
            foreach (var image in Images) draft.Images.Add(image);
            await _client.CreatePostAsync(SelectedBlog, draft, state, publishOn);
            var label = state switch
            {
                "queue" => "Queued.",
                "draft" => "Draft saved.",
                "published" when publishOn is not null => "Scheduled.",
                _ => "Posted.",
            };
            Completed?.Invoke(this, label);
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't post: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task Publish() => SubmitAsync("published");
    [RelayCommand] private Task Queue() => SubmitAsync("queue");
    [RelayCommand] private Task Draft() => SubmitAsync("draft");

    [RelayCommand]
    private Task Schedule()
    {
        if (PublishAt is null)
        {
            HasStatus = true;
            Status = "Pick a publish time first.";
            return Task.CompletedTask;
        }
        return SubmitAsync("published", PublishAt.Value.ToUnixTimeSeconds().ToString());
    }

    [RelayCommand] private void ToggleSchedule() => ShowSchedule = !ShowSchedule;
}
