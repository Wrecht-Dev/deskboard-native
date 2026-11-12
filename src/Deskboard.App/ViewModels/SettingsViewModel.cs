using System;
using System.Collections.ObjectModel;
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

    [ObservableProperty] private string _identity = "";
    [ObservableProperty] private string _sessionState = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isDark;

    public event EventHandler? CloseRequested;

    public SettingsViewModel(TumblrClient client, string identity, System.Collections.Generic.IEnumerable<string> blogs)
    {
        _client = client;
        Identity = string.IsNullOrEmpty(identity) ? "(signed out)" : identity;
        SessionState = client.HasSession
            ? "Signed in with your browser session (cookies + CSRF verified on writes)."
            : "No session found — sign-in is read from your Firefox profile on first request.";
        foreach (var blog in blogs) Blogs.Add(blog);
        IsDark = AppSettings.Theme != "light";
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
    private void ReimportSession()
    {
        var ok = _client.RefreshSessionFromBrowser();
        HasStatus = true;
        Status = ok ? "Fresh cookies imported from Firefox." : "Couldn't read a Firefox profile on this machine.";
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
