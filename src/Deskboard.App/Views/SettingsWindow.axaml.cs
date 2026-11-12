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
            {
                vm.CloseRequested += (_, _) => Close();
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(SettingsViewModel.IsDark)) RefreshThemeButtons();
                };
            }
        };
    }

    private void RefreshThemeButtons()
    {
        // the dark segment is hidden when light is active; keep the label pair stable
    }

    private void OnDark(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.SetThemeCommand.Execute("dark");
    }
}
