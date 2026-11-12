using System.Collections.Generic;
using System.Linq;
using Deskboard.App.ViewModels;
using Deskboard.Core.Models;
using Xunit;

namespace Deskboard.Tests;

public class RichTextTests
{
    private static Block Text(string text, List<FormatRange>? formatting = null, string subtype = "") => new()
    {
        Type = "text",
        Text = text,
        Subtype = subtype,
        Formatting = formatting ?? new List<FormatRange>(),
    };

    private static Deskboard.Core.Models.Post Post(params Block[] blocks) => new()
    {
        BlogName = "test",
        Content = blocks.ToList(),
    };

    [Fact]
    public void PlainTextIsOneRun()
    {
        var paragraphs = RichTextBuilder.Build(Post(Text("hello world")));
        var run = Assert.Single(Assert.Single(paragraphs).Runs);
        Assert.Equal("hello world", run.Text);
        Assert.False(run.Bold);
    }

    [Fact]
    public void BoldRangeSplitsIntoThreeRuns()
    {
        var paragraphs = RichTextBuilder.Build(Post(Text("one two three",
            new List<FormatRange> { new() { Start = 4, End = 7, Type = "bold" } })));
        var runs = Assert.Single(paragraphs).Runs;
        Assert.Equal(3, runs.Count);
        Assert.Equal("one ", runs[0].Text);
        Assert.Equal("two", runs[1].Text);
        Assert.True(runs[1].Bold);
        Assert.Equal(" three", runs[2].Text);
        Assert.False(runs[2].Bold);
    }

    [Fact]
    public void AdjacentEqualRunsAreMerged()
    {
        var paragraphs = RichTextBuilder.Build(Post(Text("abcd",
            new List<FormatRange>
            {
                new() { Start = 0, End = 2, Type = "italic" },
                new() { Start = 2, End = 4, Type = "italic" },
            })));
        var run = Assert.Single(Assert.Single(paragraphs).Runs);
        Assert.Equal("abcd", run.Text);
        Assert.True(run.Italic);
    }

    [Fact]
    public void HeadingSubtypeBecomesHeadingParagraph()
    {
        var paragraphs = RichTextBuilder.Build(Post(Text("A title", subtype: "heading1")));
        Assert.Equal(ParagraphKind.Heading1, Assert.Single(paragraphs).Kind);
    }

    [Fact]
    public void QuoteAndListSubtypesMap()
    {
        var paragraphs = RichTextBuilder.Build(Post(
            Text("quoted", subtype: "quote"),
            Text("item", subtype: "unordered-list-item")));
        Assert.Equal(ParagraphKind.Quote, paragraphs[0].Kind);
        Assert.Equal(ParagraphKind.ListItem, paragraphs[1].Kind);
    }

    [Fact]
    public void LinkRangeCarriesUrl()
    {
        var paragraphs = RichTextBuilder.Build(Post(Text("see site",
            new List<FormatRange> { new() { Start = 4, End = 8, Type = "link", Url = "https://example.com" } })));
        var link = Assert.Single(paragraphs).Runs.Single(r => r.IsLink);
        Assert.Equal("site", link.Text);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void ReblogTrailIsUsedWhenContentEmpty()
    {
        var post = new Deskboard.Core.Models.Post
        {
            Content = new List<Block>(),
            Trail = new List<TrailItem>
            {
                new() { BlogName = "a", Content = new List<Block> { Text("from the trail") } },
            },
        };
        var paragraphs = RichTextBuilder.Build(post);
        Assert.Equal("from the trail", Assert.Single(paragraphs).PlainText);
    }
}
