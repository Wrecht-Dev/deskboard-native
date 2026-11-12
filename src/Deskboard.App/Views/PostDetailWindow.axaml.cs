using Avalonia.Controls;
using Avalonia.Interactivity;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class PostDetailWindow : Window
{
    public PostDetailWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PostDetailViewModel vm)
                vm.CloseRequested += (_, _) => Close();
        };
    }

    private async void OnDeleteNote(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PostDetailViewModel vm) return;
        if ((sender as Control)?.DataContext is not NoteViewModel note) return;
        if (await ConfirmWindow.AskAsync(this, "Remove this note from your post?", "Remove"))
            await vm.DeleteNoteAsync(note);
    }

    private async void OnFlagNote(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PostDetailViewModel vm) return;
        if ((sender as Control)?.DataContext is not NoteViewModel note) return;
        if (await ConfirmWindow.AskAsync(this, "Report this note to Tumblr?", "Report"))
            await vm.FlagNoteAsync(note);
    }
}
