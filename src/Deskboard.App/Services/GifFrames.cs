using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Deskboard.App.Services;

/// <summary>
/// Decodes an animated GIF into PNG frames plus per-frame delays using Skia.
/// Kept free of Avalonia types so it can be unit tested headlessly.
/// </summary>
public static class GifFrames
{
    public static (List<byte[]> Frames, List<int> Delays) Decode(byte[] bytes, int maxFrames = 200)
    {
        var frames = new List<byte[]>();
        var delays = new List<int>();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) return (frames, delays);

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
        var count = Math.Min(codec.FrameCount, maxFrames);
        for (var i = 0; i < count; i++)
        {
            using var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(i));
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) continue;
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
            if (encoded is null) continue;
            frames.Add(encoded.ToArray());
            var delay = codec.FrameInfo.Length > i ? codec.FrameInfo[i].Duration : 100;
            delays.Add(Math.Clamp(delay, 30, 2000));
        }
        return (frames, delays);
    }
}
