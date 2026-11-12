using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Deskboard.App.Services;

/// <summary>
/// Shared image loader: Tumblr's CDN rejects requests without a browser UA and a
/// Referer, so both are set once here. Decoded bitmaps are cached by (url, width)
/// and delivered on the UI thread.
/// </summary>
public static class ImageLoader
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, Bitmap> Cache = new();
    private static readonly object Gate = new();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Referrer = new Uri("https://www.tumblr.com/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/png,image/jpeg,*/*");
        return client;
    }

    private static string Key(string url, int width) => $"{url.Split('?')[0]}|{width}";

    public static async Task<Bitmap?> LoadAsync(string url, int width)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var key = Key(url, width);
        lock (Gate)
            if (Cache.TryGetValue(key, out var cached)) return cached;

        try
        {
            // The decoder needs a seekable stream; network streams are not.
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;
            var bitmap = Bitmap.DecodeToWidth(buffer, width);
            lock (Gate)
            {
                if (Cache.Count > 300) Cache.Clear();
                Cache[key] = bitmap;
            }
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Raw bytes (used by the animated GIF decoder).</summary>
    public static async Task<byte[]?> GetBytesAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try { return await Http.GetByteArrayAsync(url).ConfigureAwait(false); }
        catch (Exception) { return null; }
    }

    /// <summary>Apply a bitmap to a property on the UI thread.</summary>
    public static Task ApplyAsync(Action<Bitmap> setter, Bitmap bitmap)
        => Dispatcher.UIThread.InvokeAsync(() => setter(bitmap)).GetTask();
}
