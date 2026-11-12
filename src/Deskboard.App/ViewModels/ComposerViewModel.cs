using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public enum ComposerMode { New, Submit, Edit }

public sealed partial class ComposerViewModel : ObservableObject
{
    private readonly TumblrClient _client;
    private string _editPostId = "";
    private string _editBlog = "";

    public ObservableCollection<string> Blogs { get; } = new();
    public ObservableCollection<UploadedMedia> Images { get; } = new();

    [ObservableProperty] private string _selectedBlog = "";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _tags = "";
    [ObservableProperty] private int _postTypeIndex;
    [ObservableProperty] private string _linkUrl = "";
    [ObservableProperty] private string _linkTitle = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DateTimeOffset? _publishAt = DateTimeOffset.Now.AddHours(1);
    [ObservableProperty] private bool _showSchedule;

    public ComposerMode Mode { get; private set; } = ComposerMode.New;
    public string WindowTitle => Mode switch
    {
        ComposerMode.Submit => "Submit a post",
        ComposerMode.Edit => "Edit post",
        _ => "New post",
    };
    public bool IsNew => Mode == ComposerMode.New;
    public bool IsSubmit => Mode == ComposerMode.Submit;
    public bool IsEdit => Mode == ComposerMode.Edit;

    public string CharacterLabel => $"{Text.Length}";
    public string MediaLabel => Images.Count == 0 ? "" : $"{Images.Count} media attached";
    public string PostType => PostTypeIndex switch { 1 => "quote", 2 => "link", _ => "text" };
    public bool IsLink => PostTypeIndex == 2;
    public bool IsQuote => PostTypeIndex == 1;

    public bool CanSubmit => !IsBusy && SelectedBlog.Length > 0 &&
                             (Text.Trim().Length > 0 || Images.Count > 0 ||
                              (IsLink && LinkUrl.Trim().Length > 0));

    public event EventHandler<string>? Completed;

    public ComposerViewModel(TumblrClient client, IEnumerable<string> blogs)
    {
        _client = client;
        foreach (var blog in blogs) Blogs.Add(blog);
        SelectedBlog = Blogs.Count > 0 ? Blogs[0] : "";
    }

    /// <summary>Submission to another blog.</summary>
    public static ComposerViewModel ForSubmission(TumblrClient client, string blog)
        => new(client, new[] { blog })
        {
            Mode = ComposerMode.Submit,
        };

    /// <summary>Editing one of your own posts.</summary>
    public static ComposerViewModel ForEdit(TumblrClient client, IEnumerable<string> blogs,
        string blog, PostItemViewModel item)
    {
        var text = string.Join("\n\n", item.Post.Content
            .Where(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text))
            .Select(b => b.Text));
        var vm = new ComposerViewModel(client, blogs)
        {
            Mode = ComposerMode.Edit,
            Text = text,
            Tags = string.Join(", ", item.Post.Tags),
            SelectedBlog = blog,
        };
        vm._editPostId = item.Post.Id;
        vm._editBlog = blog;
        return vm;
    }

    private async Task SaveEditAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        HasStatus = true;
        Status = "Saving…";
        try
        {
            var draft = new ComposerDraft
            {
                Text = Text,
                Tags = Tags,
                PostType = PostType,
                LinkUrl = LinkUrl,
                LinkTitle = LinkTitle,
            };
            foreach (var image in Images) draft.Images.Add(image);
            await _client.EditPostAsync(_editBlog, _editPostId, draft);
            Completed?.Invoke(this, "Post updated.");
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't save: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task SubmitToBlogAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        HasStatus = true;
        Status = "Submitting…";
        try
        {
            var draft = new ComposerDraft
            {
                Text = Text,
                Tags = Tags,
                PostType = PostType,
                LinkUrl = LinkUrl,
                LinkTitle = LinkTitle,
            };
            foreach (var image in Images) draft.Images.Add(image);
            await _client.SubmitPostAsync(SelectedBlog, draft);
            Completed?.Invoke(this, $"Submitted to {SelectedBlog} for review.");
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't submit: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task Save() => Mode == ComposerMode.Edit ? SaveEditAsync() : SubmitToBlogAsync();

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(CharacterLabel));
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnPostTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(PostType));
        OnPropertyChanged(nameof(IsLink));
        OnPropertyChanged(nameof(IsQuote));
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnLinkUrlChanged(string value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnSelectedBlogChanged(string value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnShowScheduleChanged(bool value) => OnPropertyChanged(nameof(ShowSchedule));

    public void AddGif(Deskboard.Core.Models.GifItem gif)
    {
        Images.Add(new UploadedMedia
        {
            Url = gif.Url,
            MimeType = gif.MimeType,
            Width = gif.Width,
            Height = gif.Height,
        });
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(MediaLabel));
    }

    public async Task<System.Collections.Generic.IReadOnlyList<Deskboard.Core.Models.GifItem>> LoadGifsAsync()
        => await _client.GetPopularGifsAsync();

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
            OnPropertyChanged(nameof(MediaLabel));
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
            var draft = new ComposerDraft
            {
                Text = Text,
                Tags = Tags,
                PostType = PostType,
                LinkUrl = LinkUrl,
                LinkTitle = LinkTitle,
            };
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
