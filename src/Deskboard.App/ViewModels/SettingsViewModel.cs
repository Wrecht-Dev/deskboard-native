using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.App.Services;
using Deskboard.Core.Api;

namespace Deskboard.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly TumblrClient _client;
    public TumblrClient Client => _client;

    public ObservableCollection<string> Blogs { get; } = new();
    public ObservableCollection<string> MutedWords { get; } = new();
    public ObservableCollection<string> ServerSettings { get; } = new();
    public FeedFilter Filters { get; }

    [ObservableProperty] private string _identity = "";
    [ObservableProperty] private string _sessionState = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isDark;

    public event EventHandler? CloseRequested;

    [ObservableProperty] private string _newMutedWord = "";
    [ObservableProperty] private bool _filtersEnabled;
    [ObservableProperty] private bool _hideAdult;

    public SettingsViewModel(TumblrClient client, string identity, System.Collections.Generic.IEnumerable<string> blogs,
        FeedFilter? filters = null)
    {
        _client = client;
        Filters = filters ?? FeedFilter.Load();
        FiltersEnabled = Filters.Enabled;
        HideAdult = AppSettings.BlurAdult;
        foreach (var word in Filters.MutedWords) MutedWords.Add(word);
        Identity = string.IsNullOrEmpty(identity) ? "(signed out)" : identity;
        SessionState = client.HasSession
            ? "Signed in with your browser session (cookies + CSRF verified on writes)."
            : "No session found — sign-in is read from your Firefox profile on first request.";
        foreach (var blog in blogs) Blogs.Add(blog);
        IsDark = AppSettings.Theme != "light";
    }

    partial void OnHideAdultChanged(bool value) => AppSettings.BlurAdult = value;

    partial void OnFiltersEnabledChanged(bool value)
    {
        Filters.Enabled = value;
        Filters.Save();
    }

    [RelayCommand]
    private void AddMutedWord()
    {
        var word = NewMutedWord.Trim();
        if (word.Length == 0) return;
        if (!MutedWords.Contains(word, System.StringComparer.OrdinalIgnoreCase))
        {
            MutedWords.Add(word);
            Filters.MutedWords = MutedWords.ToList();
            Filters.Save();
        }
        NewMutedWord = "";
    }

    [RelayCommand]
    private void RemoveMutedWord(string word)
    {
        MutedWords.Remove(word);
        Filters.MutedWords = MutedWords.ToList();
        Filters.Save();
    }

    [RelayCommand]
    private void SetTheme(string theme)
    {
        var light = theme == "light";
        IsDark = !light;
        AppSettings.Theme = light ? "light" : "dark";
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = light ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadServerSettings()
    {
        HasStatus = true;
        Status = "Loading account settings…";
        try
        {
            ServerSettings.Clear();
            var blogSettings = await _client.GetBlogSettingsAsync(Blogs.FirstOrDefault() ?? "");
            foreach (var (key, value) in blogSettings.Take(20))
                ServerSettings.Add($"blog · {key}: {value}");
            var userSettings = await _client.GetUserSettingsAsync();
            foreach (var (key, value) in userSettings.Take(20))
                ServerSettings.Add($"account · {key}: {value}");
            Status = $"Loaded {ServerSettings.Count} settings from Tumblr.";
        }
        catch (System.Exception ex)
        {
            Status = ex is TumblrException ? ex.Message : $"Couldn't load settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        _client.SignOut();
        HasStatus = true;
        Status = "Signed out. Restart Deskboard to sign in again.";
        SessionState = "Signed out.";
    }

    [RelayCommand]
    private void ReimportSession()
    {
        var ok = _client.RefreshSessionFromBrowser();
        HasStatus = true;
        Status = ok ? "Fresh cookies imported from Firefox." : "Couldn't read a Firefox profile on this machine.";
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
