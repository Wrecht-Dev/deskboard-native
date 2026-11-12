using System.IO;
using Deskboard.App.Services;
using Xunit;

namespace Deskboard.Tests;

public class GifTests
{
    private static byte[] Fixture() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "anim.gif"));

    [Fact]
    public void AnimatedGifDecodesEveryFrame()
    {
        var (frames, delays) = GifFrames.Decode(Fixture());
        Assert.Equal(3, frames.Count);
        Assert.Equal(3, delays.Count);
        Assert.All(frames, f => Assert.True(f.Length > 0));
    }

    [Fact]
    public void FramesArePng()
    {
        var (frames, _) = GifFrames.Decode(Fixture());
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, frames[0][..4]);
    }

    [Fact]
    public void DelaysAreClampedToSaneRange()
    {
        var (_, delays) = GifFrames.Decode(Fixture());
        Assert.All(delays, d => Assert.InRange(d, 30, 2000));
    }

    [Fact]
    public void GarbageReturnsNoFrames()
        => Assert.Empty(GifFrames.Decode(new byte[] { 1, 2, 3, 4 }).Frames);
}
