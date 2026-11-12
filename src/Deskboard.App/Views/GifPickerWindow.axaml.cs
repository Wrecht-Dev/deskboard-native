using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Deskboard.Core.Models;

namespace Deskboard.App.Views;

public partial class GifPickerWindow : Window
{
    private readonly List<GifItem> _gifs = new();

    public GifItem? Selected { get; private set; }

    public GifPickerWindow() => InitializeComponent();

    public void SetGifs(IReadOnlyList<GifItem> gifs)
    {
        _gifs.Clear();
        _gifs.AddRange(gifs);
        GifList.ItemsSource = null;

        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var gif in gifs)
        {
            var image = new Image { Width = 150, Height = 150, Stretch = Avalonia.Media.Stretch.UniformToFill };
            _ = LoadThumbAsync(gif.Thumbnail, image);
            var button = new Button
            {
                Content = image,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
            };
            button.Click += (_, _) => { Selected = gif; Close(); };
            panel.Children.Add(button);
        }
        GifList.ItemsSource = new[] { panel };
        GifList.ItemTemplate = null;
    }

    private static async System.Threading.Tasks.Task LoadThumbAsync(string url, Image image)
    {
        var bitmap = await Services.ImageLoader.LoadAsync(url, 220);
        if (bitmap is not null)
            await Dispatcher.UIThread.InvokeAsync(() => image.Source = bitmap);
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
