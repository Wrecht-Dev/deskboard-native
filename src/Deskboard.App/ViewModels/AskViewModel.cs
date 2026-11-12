using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.Core.Api;

namespace Deskboard.App.ViewModels;

public sealed partial class AskViewModel : ObservableObject
{
    private readonly TumblrClient _client;
    private readonly string _blog;

    public string Title => $"Ask {_blog}";
    public bool AllowAnonymous { get; }

    [ObservableProperty] private string _question = "";
    [ObservableProperty] private bool _anonymous;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _hint = "";

    public event EventHandler<bool>? Completed;

    public AskViewModel(TumblrClient client, string blog, bool allowAnonymous)
    {
        _client = client;
        _blog = blog;
        AllowAnonymous = allowAnonymous;
    }

    public bool CanSend => !IsBusy && Question.Trim().Length is > 0 and <= 500;
    public string Counter => $"{Question.Length}/500";

    partial void OnQuestionChanged(string value)
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(Counter));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSend));

    [RelayCommand]
    private async Task Send()
    {
        if (!CanSend) return;
        IsBusy = true;
        HasError = false;
        try
        {
            await _client.SendAskAsync(_blog, Question.Trim(), Anonymous);
            Hint = "Ask sent.";
            Completed?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            HasError = true;
            Error = ex is TumblrException ? ex.Message : $"Couldn't send: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel() => Completed?.Invoke(this, false);
}
