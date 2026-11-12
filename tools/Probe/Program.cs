using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var (name, blogs) = await client.GetIdentityAsync();
var blog = blogs.FirstOrDefault() ?? name;
Console.WriteLine($"blog={blog}");
foreach (var q in new[] { "?limit=20", "?limit=10&before=", "", "?limit=20&before=0", "?limit=25" })
{
    try
    {
        var page = await client.GetNotificationsAsync(blog, $"/v2/blog/{blog}/notifications{q}");
        Console.WriteLine($"{q,-28} -> {page.Notifications.Count} notifications");
    }
    catch (Exception ex) { Console.WriteLine($"{q,-28} -> {ex.Message}"); }
}
