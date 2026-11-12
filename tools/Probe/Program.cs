using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var home = await client.GetHomeAsync(14);
foreach (var p in home.Items)
{
    var gifs = p.Content.SelectMany(b => b.Media).Count(m => m.IsGif);
    var imgs = p.Content.SelectMany(b => b.Media).Count(m => m.Kind == "image");
    if (gifs > 0 || imgs > 0) Console.WriteLine($"{p.BlogName,-24} gifs={gifs} imgs={imgs}");
}
var art = await client.GetBlogPostsAsync("artspotting", 20);
var gifPosts = art.Items.Where(p => p.Content.SelectMany(b => b.Media).Any(m => m.IsGif)).ToList();
Console.WriteLine($"artspotting: {art.Items.Count} posts, {gifPosts.Count} with gif");
if (gifPosts.Count > 0)
{
    var m = gifPosts[0].Content.SelectMany(b => b.Media).First(x => x.IsGif);
    Console.WriteLine("gif url: " + m.Url);
}
