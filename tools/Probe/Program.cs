using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var (name, blogs) = await client.GetIdentityAsync();
var blog = blogs.FirstOrDefault() ?? name;
foreach (var (label, task) in new (string, Func<Task<Deskboard.Core.Models.Paged<Deskboard.Core.Models.Post>>>)[]
{
    ("inbox", () => client.GetInboxAsync()),
    ("drafts", () => client.GetDraftsAsync(blog)),
    ("queue", () => client.GetQueueAsync(blog)),
})
{
    try { var page = await task(); Console.WriteLine($"{label}: {page.Items.Count} items | next={page.NextUrl is not null}"); }
    catch (Exception ex) { Console.WriteLine($"{label}: ERROR {ex.Message}"); }
}
