using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Deskboard.App.Views;

public partial class ConfirmWindow : Window
{
    private bool _confirmed;

    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public static async Task<bool> AskAsync(Window owner, string message, string action)
    {
        var window = new ConfirmWindow();
        window.MessageText.Text = message;
        window.ConfirmButton.Content = action;
        await window.ShowDialog(owner);
        return window._confirmed;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
