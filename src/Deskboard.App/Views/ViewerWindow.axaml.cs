using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Deskboard.App.Views;

/// <summary>Full-size media viewer with zoom, matching the web lightbox behaviour.</summary>
public partial class ViewerWindow : Window
{
    private Bitmap? _original;
    private double _zoom = 1.0;

    public ViewerWindow() => InitializeComponent();

    public static async Task ShowAsync(Window owner, string url, bool isGif)
    {
        var window = new ViewerWindow { Title = "Media" };
        window.Show(owner);
        await window.LoadAsync(url, isGif);
    }

    public async Task LoadAsync(string url, bool isGif)
    {
        if (isGif)
        {
            Animation.Url = url;
            Animation.IsVisible = true;
            return;
        }

        var bitmap = await Services.ImageLoader.LoadAsync(url, 2400);
        if (bitmap is null) return;
        _original = bitmap;
        await Dispatcher.UIThread.InvokeAsync(() => { Picture.Source = bitmap; ApplyZoom(); });
    }

    private void ApplyZoom()
    {
        if (_original is null) return;
        Picture.Width = _original.PixelSize.Width * _zoom;
        Picture.Height = _original.PixelSize.Height * _zoom;
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) { _zoom = Math.Min(4, _zoom * 1.25); ApplyZoom(); }
    private void OnZoomOut(object? sender, RoutedEventArgs e) { _zoom = Math.Max(0.2, _zoom / 1.25); ApplyZoom(); }
    private void OnFit(object? sender, RoutedEventArgs e) { _zoom = 1.0; ApplyZoom(); }
    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
