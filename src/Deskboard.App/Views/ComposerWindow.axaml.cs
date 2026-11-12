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
