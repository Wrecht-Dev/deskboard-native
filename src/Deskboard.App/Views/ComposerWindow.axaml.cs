using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class ComposerWindow : Window
{
    public ComposerWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ComposerViewModel vm)
                vm.Completed += (_, label) => Close();
        };
    }

    private async void OnAddGif(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ComposerViewModel vm) return;
        var picker = new GifPickerWindow();
        try
        {
            var gifs = await vm.LoadGifsAsync();
            picker.SetGifs(gifs);
        }
        catch (System.Exception) { }
        await picker.ShowDialog(this);
        if (picker.Selected is { } gif) vm.AddGif(gif);
    }

    private async void OnAttach(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ComposerViewModel vm) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Attach an image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp" } },
            },
        });
        var file = files.FirstOrDefault();
        if (file?.TryGetLocalPath() is { } path) await vm.AttachAsync(path);
    }
}
