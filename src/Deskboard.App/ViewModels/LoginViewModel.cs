using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.Core.Api;

namespace Deskboard.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly TumblrClient _client;

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _twoFactorCode = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _needsTwoFactor;
    [ObservableProperty] private bool _isBusy;

    public event EventHandler<bool>? Completed;

    public bool CanSubmit => !IsBusy && Email.Trim().Length > 0 && Password.Length > 0;

    public LoginViewModel(TumblrClient client) => _client = client;

    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));

    [RelayCommand]
    private async Task SignIn()
    {
        if (!CanSubmit) return;
        IsBusy = true;
        HasStatus = true;
        Status = "Signing in…";
        try
        {
            // The password never leaves this object; it is sent once to Tumblr and
            // not stored anywhere.
            var result = await _client.LoginAsync(Email, Password,
                NeedsTwoFactor ? TwoFactorCode.Trim() : null);
            Status = result.Message;
            switch (result.Status)
            {
                case LoginStatus.Success:
                    Password = "";
                    TwoFactorCode = "";
                    Completed?.Invoke(this, true);
                    break;
                case LoginStatus.NeedsTwoFactor:
                    NeedsTwoFactor = true;
                    Status = "Two-factor is on for this account. Enter the code below.";
                    break;
                case LoginStatus.NeedsCaptcha:
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Status = $"Couldn't sign in: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void UseBrowserSession()
    {
        var ok = _client.RefreshSessionFromBrowser();
        HasStatus = true;
        Status = ok
            ? "Imported your Firefox session."
            : "No Firefox profile with a Tumblr session was found on this machine.";
        if (ok) Completed?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => Completed?.Invoke(this, false);
}
