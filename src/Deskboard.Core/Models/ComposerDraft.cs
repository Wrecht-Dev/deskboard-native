namespace Deskboard.Core.Models;

/// <summary>What the composer collected before it becomes a Tumblr post.</summary>
public sealed class ComposerDraft
{
    public string Text { get; set; } = "";
    /// <summary>text | quote | link | photo</summary>
    public string PostType { get; set; } = "text";
    public string LinkUrl { get; set; } = "";
    public string LinkTitle { get; set; } = "";
    public string Tags { get; set; } = "";
    public List<UploadedMedia> Images { get; } = new();
}

public sealed class UploadedMedia
{
    public string Url { get; init; } = "";
    public string MimeType { get; init; } = "image/png";
    public int Width { get; init; }
    public int Height { get; init; }
}
