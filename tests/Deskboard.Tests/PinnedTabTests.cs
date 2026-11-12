using System.Collections.Generic;
using System.IO;
using Deskboard.App.Services;
using Xunit;

namespace Deskboard.Tests;

public class PinnedTabTests
{
    [Fact]
    public void RoundTripsThroughDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "deskboard-tabs-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var tabs = new List<PinnedTab>
            {
                new() { Title = "Art", Kind = "tag", Value = "art", Sort = "top" },
                new() { Title = "Cats", Kind = "search", Value = "cats", Sort = "latest" },
                new() { Title = "Your tags", Kind = "your-tags" },
            };
            PinnedTabStore.Save(tabs, dir);
            var loaded = PinnedTabStore.Load(dir);
            Assert.Equal(3, loaded.Count);
            Assert.Equal("top", loaded[0].Sort);
            Assert.Equal("search", loaded[1].Kind);
            Assert.Equal("your-tags", loaded[2].Kind);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void InvalidSortFallsBackToLatest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "deskboard-tabs-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(PinnedTabStore.FilePath(dir),
                "[{\"Title\":\"x\",\"Kind\":\"tag\",\"Value\":\"art\",\"Sort\":\"WAT\"}]");
            Assert.Equal("latest", PinnedTabStore.Load(dir)[0].Sort);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void MissingFileReturnsDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "deskboard-tabs-" + System.Guid.NewGuid().ToString("N"));
        Assert.NotEmpty(PinnedTabStore.Load(dir));
    }

    [Fact]
    public void SortLabelMatchesSort()
    {
        Assert.Equal("Top", new PinnedTab { Sort = "top" }.SortLabel);
        Assert.Equal("Latest", new PinnedTab { Sort = "latest" }.SortLabel);
    }
}
