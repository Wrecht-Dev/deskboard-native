using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var found = 0;
foreach (var blog in new[] { "staff", "tumblr", "artspotting", "variety" })
{
    var page = await client.GetBlogPostsAsync(blog, 20);
    foreach (var p in page.Items.Where(x => x.Trail.Count >= 2).Take(2))
    {
        found++;
        Console.WriteLine($"post {p.BlogName} content={p.Content.Count} trail={p.Trail.Count}");
        for (var i = 0; i < p.Trail.Count; i++)
        {
            var texts = p.Trail[i].Content.Where(b => b.Type == "text").Select(b => b.Text.Trim()).Where(t => t.Length > 0).ToList();
            Console.WriteLine($"  trail[{i}] {p.Trail[i].BlogName,-22} {string.Join(" | ", texts.Select(t => t[..Math.Min(60, t.Length)]))}");
        }
    }
}
Console.WriteLine($"found={found}");
