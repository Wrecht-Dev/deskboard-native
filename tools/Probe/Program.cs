using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var (name, blogs) = await client.GetIdentityAsync();
var page = await client.GetNotificationsAsync(blogs.FirstOrDefault() ?? name);
Console.WriteLine($"notifications={page.Notifications.Count} next={page.NextUrl is not null}");
foreach (var n in page.Notifications.Take(3))
    Console.WriteLine($"  {n.Type,-8} from={n.BlogName,-22} avatar={(n.AvatarUrl.Length > 0 ? "yes" : "no")} followBack={n.NeedsFollowBack}");
