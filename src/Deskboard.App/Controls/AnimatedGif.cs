using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Deskboard.App.Services;

namespace Deskboard.App.Controls;

/// <summary>
/// Plays animated GIFs natively: Skia decodes every frame once, then a timer
/// swaps Avalonia bitmaps using each frame's own delay.
/// </summary>
public sealed class AnimatedGif : UserControl
{
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<AnimatedGif, string?>(nameof(Url));

    private readonly Image _image = new() { Stretch = Avalonia.Media.Stretch.Uniform };
    private readonly DispatcherTimer _timer = new();
    private List<Bitmap> _frames = new();
    private List<int> _delays = new();
    private int _index;
    private bool _loading;

    /// <summary>How frames fit the control (mirrors Image.Stretch).</summary>
    public Avalonia.Media.Stretch Stretch
    {
        get => _image.Stretch;
        set => _image.Stretch = value;
    }

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public AnimatedGif()
    {
        Content = _image;
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) => { if (_frames.Count > 1) Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UrlProperty && Url is { Length: > 0 } url)
            _ = LoadAsync(url);
    }

    private async Task LoadAsync(string url)
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var bytes = await ImageLoader.GetBytesAsync(url);
            if (bytes is null) return;
            var (frames, delays) = await Task.Run(() => Decode(bytes));
            if (frames.Count == 0) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _frames = frames;
                _delays = delays;
                _index = 0;
                _image.Source = frames[0];
                if (frames.Count > 1) Start();
            });
        }
        catch (Exception) { /* leave the static frame */ }
        finally { _loading = false; }
    }

    private static (List<Bitmap>, List<int>) Decode(byte[] bytes)
    {
        var (pngFrames, delays) = GifFrames.Decode(bytes);
        var frames = new List<Bitmap>();
        foreach (var png in pngFrames)
        {
            using var stream = new MemoryStream(png);
            frames.Add(new Bitmap(stream));
        }
        return (frames, delays);
    }

    private void Start()
    {
        _timer.Stop();
        _index = 0;
        _image.Source = _frames[0];
        _timer.Interval = TimeSpan.FromMilliseconds(_delays[0]);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames.Count <= 1) { _timer.Stop(); return; }
        _index = (_index + 1) % _frames.Count;
        _image.Source = _frames[_index];
        _timer.Interval = TimeSpan.FromMilliseconds(_delays[_index]);
    }
}
