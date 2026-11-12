using System;
using System.Collections.Generic;
using System.Linq;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public enum ParagraphKind { Body, Heading1, Heading2, Quote, ListItem, Chat }

public sealed record RunViewModel(string Text, bool Bold = false, bool Italic = false,
    bool Underline = false, bool Strike = false, bool IsLink = false, string Url = "");

public sealed record ParagraphViewModel(ParagraphKind Kind, IReadOnlyList<RunViewModel> Runs)
{
    public string PlainText => string.Concat(Runs.Select(r => r.Text));
}

/// <summary>
/// Turns NPF blocks into displayable paragraphs. Text blocks carry formatting
/// ranges (bold/italic/strikethrough/underline/link/color) which are split into
/// styled runs so the native text view looks like the website.
/// </summary>
public static class RichTextBuilder
{
    /// <summary>
    /// Blocks that belong to the post body. A reblog carries the reblogger's new
    /// blocks in <c>content</c> and the original post in <c>trail</c>, so both are
    /// concatenated in order (comment first, then what was reblogged).
    /// </summary>
    public static IReadOnlyList<Block> BodyBlocks(Post post)
    {
        var blocks = new List<Block>(post.Content);
        foreach (var item in post.Trail) blocks.AddRange(item.Content);
        return blocks;
    }

    public static List<ParagraphViewModel> Build(Post post)
    {
        var output = new List<ParagraphViewModel>();
        foreach (var block in BodyBlocks(post))
        {
            switch (block.Type)
            {
                case "text":
                    AddText(output, block);
                    break;
                case "link":
                    output.Add(new ParagraphViewModel(ParagraphKind.Body, new[]
                    {
                        new RunViewModel(block.Title.Length > 0 ? block.Title : block.Url,
                            Bold: true, IsLink: true, Url: block.Url),
                        new RunViewModel(block.Description.Length > 0 ? "  " + block.Description : "",
                            Italic: true),
                    }));
                    break;
                case "poll":
                    output.Add(new ParagraphViewModel(ParagraphKind.Body,
                        new[] { new RunViewModel("Poll", Bold: true) }));
                    break;
            }
        }
        return output;
    }

    private static void AddText(List<ParagraphViewModel> output, Block block)
    {
        var text = block.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var kind = block.Subtype switch
        {
            "heading1" => ParagraphKind.Heading1,
            "heading2" => ParagraphKind.Heading2,
            "quote" => ParagraphKind.Quote,
            "chat" => ParagraphKind.Chat,
            "ordered-list-item" or "unordered-list-item" => ParagraphKind.ListItem,
            _ => ParagraphKind.Body,
        };

        var runs = Split(text, block.Formatting);
        output.Add(new ParagraphViewModel(kind, runs));
    }

    private static List<RunViewModel> Split(string text, List<FormatRange> formatting)
    {
        if (formatting.Count == 0) return new List<RunViewModel> { new(text) };

        // Collect run boundaries from every range, then hydrate each slice.
        var points = new SortedSet<int> { 0, text.Length };
        foreach (var range in formatting)
        {
            var start = Math.Clamp(range.Start, 0, text.Length);
            var end = Math.Clamp(range.End, 0, text.Length);
            if (start < end) { points.Add(start); points.Add(end); }
        }

        var ordered = points.ToArray();
        var runs = new List<RunViewModel>();
        for (var i = 0; i < ordered.Length - 1; i++)
        {
            var from = ordered[i];
            var to = ordered[i + 1];
            if (to <= from) continue;
            var slice = text[from..to];
            var active = formatting.Where(r => r.Start <= from && r.End >= to).ToList();
            var bold = active.Any(r => r.Type is "bold");
            var italic = active.Any(r => r.Type is "italic");
            var strike = active.Any(r => r.Type is "strikethrough");
            var underline = active.Any(r => r.Type is "underline");
            var link = active.FirstOrDefault(r => r.Type == "link");
            runs.Add(new RunViewModel(slice, bold, italic, underline, strike, link is not null, link?.Url ?? ""));
        }
        return Merge(runs);
    }

    private static List<RunViewModel> Merge(List<RunViewModel> runs)
    {
        var merged = new List<RunViewModel>();
        foreach (var run in runs)
        {
            if (run.Text.Length == 0) continue;
            if (merged.Count > 0)
            {
                var last = merged[^1];
                if (last.Bold == run.Bold && last.Italic == run.Italic && last.Underline == run.Underline
                    && last.Strike == run.Strike && last.IsLink == run.IsLink && last.Url == run.Url)
                {
                    merged[^1] = last with { Text = last.Text + run.Text };
                    continue;
                }
            }
            merged.Add(run);
        }
        return merged;
    }
}
