namespace Deskboard.Core.Models;

/// <summary>What the composer collected before it becomes a Tumblr post.</summary>
public sealed class ComposerDraft
{
    public string Text { get; set; } = "";
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
