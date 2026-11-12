namespace Deskboard.Core.Models;

/// <summary>One entry from Tumblr's GIF library.</summary>
public sealed class GifItem
{
    public string Url { get; init; } = "";
    public string Poster { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public string MimeType { get; init; } = "image/gif";
    public string Thumbnail => Poster.Length > 0 ? Poster : Url;
}
