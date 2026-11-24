using Avalonia.Controls;
using Avalonia.Interactivity;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                vm.CloseRequested += (_, _) => Close();
        };
    }

    public void StartAtKeywordFilters()
    {
        Opened += (_, _) =>
        {
            KeywordInput.BringIntoView();
            KeywordInput.Focus();
        };
    }

    private async void OnSignIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        var login = new LoginViewModel(vm.Client);
        var window = new LoginWindow { DataContext = login };
        await window.ShowDialog(this);
    }
}
