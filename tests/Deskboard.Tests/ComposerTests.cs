using System.Text.Json;
using Deskboard.Core.Api;
using Deskboard.Core.Models;
using Xunit;

namespace Deskboard.Tests;

public class ComposerTests
{
    [Fact]
    public void TextOnlyDraftProducesOneTextBlock()
    {
        var json = TumblrClient.BuildContentJson(new ComposerDraft { Text = "hello" });
        using var doc = JsonDocument.Parse(json);
        var block = doc.RootElement[0];
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("text", block.GetProperty("type").GetString());
        Assert.Equal("hello", block.GetProperty("text").GetString());
    }

    [Fact]
    public void ImagesBecomeImageBlocksAfterTheText()
    {
        var draft = new ComposerDraft { Text = "look" };
        draft.Images.Add(new UploadedMedia { Url = "https://cdn/x.png", Width = 10, Height = 20 });
        using var doc = JsonDocument.Parse(TumblrClient.BuildContentJson(draft));
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        var image = doc.RootElement[1];
        Assert.Equal("image", image.GetProperty("type").GetString());
        var media = image.GetProperty("media")[0];
        Assert.Equal("https://cdn/x.png", media.GetProperty("url").GetString());
        Assert.Equal(10, media.GetProperty("width").GetInt32());
    }

    [Fact]
    public void QuoteDraftUsesQuoteSubtype()
    {
        using var doc = JsonDocument.Parse(TumblrClient.BuildContentJson(
            new ComposerDraft { Text = "words", PostType = "quote" }));
        Assert.Equal("quote", doc.RootElement[0].GetProperty("subtype").GetString());
    }

    [Fact]
    public void LinkDraftAddsALinkBlock()
    {
        using var doc = JsonDocument.Parse(TumblrClient.BuildContentJson(new ComposerDraft
        {
            Text = "look at this",
            PostType = "link",
            LinkUrl = "https://example.com",
            LinkTitle = "Example",
        }));
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        var link = doc.RootElement[1];
        Assert.Equal("link", link.GetProperty("type").GetString());
        Assert.Equal("https://example.com", link.GetProperty("url").GetString());
        Assert.Equal("Example", link.GetProperty("title").GetString());
    }

    [Fact]
    public void EmptyDraftProducesNoBlocks()
    {
        using var doc = JsonDocument.Parse(TumblrClient.BuildContentJson(new ComposerDraft()));
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void WhWhitespaceIsNotPosted()
    {
        using var doc = JsonDocument.Parse(TumblrClient.BuildContentJson(new ComposerDraft { Text = "   " }));
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }
}
