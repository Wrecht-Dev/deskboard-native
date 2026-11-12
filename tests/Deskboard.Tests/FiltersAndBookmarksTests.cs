using System.Collections.Generic;
using System.IO;
using Deskboard.App.Services;
using Deskboard.Core.Models;
using Xunit;

namespace Deskboard.Tests;

public class FiltersAndBookmarksTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "deskboard-f-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void MutedWordMatchesTextAndTags()
    {
        var filter = new FeedFilter { MutedWords = new List<string> { "spoilers", "politics" } };
        Assert.True(filter.IsMuted("big spoilers ahead", new string[0]));
        Assert.True(filter.IsMuted("a post", new[] { "politics" }));
        Assert.False(filter.IsMuted("a harmless post", new[] { "art" }));
    }

    [Fact]
    public void DisabledFilterNeverMatches()
    {
        var filter = new FeedFilter { MutedWords = new List<string> { "x" }, Enabled = false };
        Assert.False(filter.IsMuted("x marks the spot", new string[0]));
    }

    [Fact]
    public void MutingIsCaseInsensitive()
    {
        var filter = new FeedFilter { MutedWords = new List<string> { "Spoilers" } };
        Assert.True(filter.IsMuted("SPOILERS!", new string[0]));
    }

    [Fact]
    public void FilterRoundTripsAndDeduplicates()
    {
        var dir = TempDir();
        try
        {
            var filter = new FeedFilter { MutedWords = new List<string> { "art", " ART ", "cats" } };
            filter.Save(dir);
            var loaded = FeedFilter.Load(dir);
            Assert.Equal(2, loaded.MutedWords.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void BookmarksRoundTrip()
    {
        var dir = TempDir();
        try
        {
            var post = new Post
            {
                Id = "42",
                BlogName = "staff",
                PostUrl = "https://staff.tumblr.com/post/42",
                NoteCount = 7,
                Content = new List<Block> { new() { Type = "text", Text = "hello world" } },
            };
            var bookmark = BookmarkStore.FromPost(post);
            BookmarkStore.Save(new[] { bookmark }, dir);
            var loaded = BookmarkStore.Load(dir);
            var one = Assert.Single(loaded);
            Assert.Equal("42", one.PostId);
            Assert.Equal("hello world", one.Summary);
            Assert.Equal(7, one.NoteCount);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmptyBookmarkStoreIsEmpty()
    {
        var dir = TempDir();
        Assert.Empty(BookmarkStore.Load(dir));
    }
}
