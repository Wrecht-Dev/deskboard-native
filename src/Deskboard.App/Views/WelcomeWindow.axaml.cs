using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Deskboard.App.Views;

public partial class WelcomeWindow : Window
{
    public string Choice { get; private set; } = "browse";

    public WelcomeWindow() => InitializeComponent();

    private void OnSignIn(object? sender, RoutedEventArgs e) { Choice = "signin"; Close(); }
    private void OnBrowserSession(object? sender, RoutedEventArgs e) { Choice = "browser"; Close(); }
    private void OnBrowse(object? sender, RoutedEventArgs e) { Choice = "browse"; Close(); }
}
