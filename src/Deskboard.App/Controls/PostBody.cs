using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Controls;

/// <summary>
/// Renders NPF paragraphs with real typography: headings, quotes, list items and
/// inline formatting (bold / italic / underline / strike / links).
/// </summary>
public sealed class PostBody : StackPanel
{
    public static readonly StyledProperty<IReadOnlyList<ParagraphViewModel>?> ParagraphsProperty =
        AvaloniaProperty.Register<PostBody, IReadOnlyList<ParagraphViewModel>?>(nameof(Paragraphs));

    public IReadOnlyList<ParagraphViewModel>? Paragraphs
    {
        get => GetValue(ParagraphsProperty);
        set => SetValue(ParagraphsProperty, value);
    }

    public PostBody()
    {
        Spacing = 8;
        Orientation = Orientation.Vertical;
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += (_, _) => Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ParagraphsProperty) Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        var paragraphs = Paragraphs;
        if (paragraphs is null) return;

        foreach (var paragraph in paragraphs)
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                FontSize = 15,
                Foreground = Brush("TextBrush", "#FFFFFF"),
            };

            switch (paragraph.Kind)
            {
                case ParagraphKind.Heading1:
                    block.FontSize = 24; block.FontWeight = FontWeight.Bold; block.LineHeight = 30;
                    break;
                case ParagraphKind.Heading2:
                    block.FontSize = 19; block.FontWeight = FontWeight.Bold; block.LineHeight = 26;
                    break;
                case ParagraphKind.Quote:
                    block.FontStyle = FontStyle.Italic;
                    block.Foreground = Brush("DimBrush", "#A8B4BE");
                    block.Margin = new Thickness(14, 2, 0, 2);
                    break;
                case ParagraphKind.ListItem:
                    block.Margin = new Thickness(14, 0, 0, 0);
                    break;
            }

            foreach (var run in paragraph.Runs)
            {
                var inline = BuildInline(run);
                block.Inlines!.Add(inline);
            }

            if (paragraph.Kind == ParagraphKind.ListItem)
            {
                var bullet = new TextBlock
                {
                    Text = "•", FontSize = 15, Margin = new Thickness(0, 0, 6, 0),
                    Foreground = Brush("DimBrush", "#A8B4BE"), VerticalAlignment = VerticalAlignment.Top,
                };
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                row.Children.Add(bullet);
                Grid.SetColumn(block, 1);
                row.Children.Add(block);
                Children.Add(row);
            }
            else
            {
                Children.Add(block);
            }
        }
    }

    private IBrush Brush(string key, string fallback)
    {
        if (Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var value) &&
            value is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse(fallback));
    }

    private Inline BuildInline(RunViewModel run)
    {
        var inline = new Run(run.Text);
        if (run.Bold) inline.FontWeight = FontWeight.Bold;
        if (run.Italic) inline.FontStyle = FontStyle.Italic;
        if (run.Underline || run.IsLink) inline.TextDecorations = TextDecorations.Underline;
        if (run.Strike) inline.TextDecorations = TextDecorations.Strikethrough;
        if (run.IsLink) inline.Foreground = Brush("TagBrush", "#7CC4FF");
        return inline;
    }

}
