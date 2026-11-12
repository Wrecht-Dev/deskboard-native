using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.App.Services;
using Deskboard.Core.Api;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public sealed partial class NoteViewModel : ObservableObject
{
    public string DisplayName { get; }
    public string Action { get; }
    public string TimeAgo { get; }
    public string Initial { get; }
    [ObservableProperty] private Bitmap? _avatar;

    public NoteViewModel(Note note, bool canModerate = false)
    {
        Note = note;
        CanModerate = canModerate;
        DisplayName = note.DisplayName;
        Action = note.Action;
        TimeAgo = Humanize(note.Timestamp);
        Initial = string.IsNullOrEmpty(note.BlogName) ? "?" : note.BlogName[..1].ToUpperInvariant();
        AvatarUrl = note.AvatarUrl;
    }

    public string AvatarUrl { get; }
    public Note Note { get; }
    public bool CanModerate { get; }

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(AvatarUrl)) return;
        var bitmap = await ImageLoader.LoadAsync(AvatarUrl, 48);
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
            _ => DateTimeOffset.FromUnixTimeSeconds(unix).ToString("MMM d, yyyy")
        };
    }
}

public sealed partial class PostDetailViewModel : ObservableObject
{
    private readonly TumblrClient _client;
    private readonly string _blog;
    private string? _next;

    public PostItemViewModel Item { get; }
    public ObservableCollection<NoteViewModel> Notes { get; } = new();

    [ObservableProperty] private string _notesHeadline = "Notes";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private string _replyText = "";
    [ObservableProperty] private bool _canReply;
    [ObservableProperty] private bool _isReplying;
    [ObservableProperty] private string _replyStatus = "";
    [ObservableProperty] private bool _hasMore;

    public event EventHandler? CloseRequested;

    public PostDetailViewModel(PostItemViewModel item, TumblrClient client, string blog)
    {
        Item = item;
        _client = client;
        _blog = blog;
        CanReply = client.HasSession && !string.IsNullOrEmpty(blog);
        _ = LoadNotesAsync();
    }

    [RelayCommand]
    private async Task LoadNotesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var page = await _client.GetNotesAsync(Item.Post.BlogName, Item.Post.Id, _next);
            if (_next is null) Notes.Clear();
            foreach (var note in page.Notes)
            {
                var canModerate = Item.Post.BlogName.Equals(_blog, StringComparison.OrdinalIgnoreCase);
                var vm = new NoteViewModel(note, canModerate);
                Notes.Add(vm);
                _ = vm.LoadAsync();
            }
            _next = page.NextUrl;
            HasMore = _next is not null;
            NotesHeadline = page.Total > 0
                ? $"{page.Total} notes · {page.TotalLikes} likes · {page.TotalReblogs} reblogs · {page.TotalReplies} replies"
                : "Notes";
            HasStatus = Notes.Count == 0;
            Status = Notes.Count == 0 ? "No notes yet." : "";
        }
        catch (Exception ex)
        {
            HasStatus = true;
            Status = ex is TumblrException ? ex.Message : $"Couldn't load notes: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ReplyAsync()
    {
        if (IsReplying || string.IsNullOrWhiteSpace(ReplyText)) return;
        IsReplying = true;
        ReplyStatus = "";
        try
        {
            await _client.ReplyAsync(Item.Post, _blog, ReplyText.Trim());
            ReplyText = "";
            ReplyStatus = "Reply posted.";
            _next = null;
            await LoadNotesAsync();
        }
        catch (Exception ex)
        {
            ReplyStatus = ex is TumblrException ? ex.Message : $"Couldn't reply: {ex.Message}";
        }
        finally { IsReplying = false; }
    }

    public async Task DeleteNoteAsync(NoteViewModel note)
    {
        try
        {
            await _client.DeleteNoteAsync(_blog, note.Note.BlogName, note.Note.Timestamp, Item.Post.Id);
            Notes.Remove(note);
            Status = "Note removed."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't remove the note: {ex.Message}";
            HasStatus = true;
        }
    }

    public async Task FlagNoteAsync(NoteViewModel note)
    {
        try
        {
            await _client.FlagNoteAsync(_blog, note.Note.BlogName, note.Note.Timestamp);
            Status = "Note flagged."; HasStatus = true;
        }
        catch (Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't flag the note: {ex.Message}";
            HasStatus = true;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
