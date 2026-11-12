using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var page = await client.GetHomeAsync(8);
var post = page.Items.FirstOrDefault(p => p.BlogAvatar.Length > 0);
var media = page.Items.SelectMany(p => p.Content).SelectMany(b => b.Media).FirstOrDefault();
Console.WriteLine("avatar url: " + post?.BlogAvatar);
Console.WriteLine("media url:  " + media?.Url);
using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Chrome/150");
http.DefaultRequestHeaders.Referrer = new Uri("https://www.tumblr.com/");
foreach (var (label, url) in new[] { ("avatar", post?.BlogAvatar ?? ""), ("media", media?.Url ?? "") })
{
    if (string.IsNullOrEmpty(url)) { Console.WriteLine($"{label}: none"); continue; }
    try
    {
        var res = await http.GetAsync(url);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Console.WriteLine($"{label}: {(int)res.StatusCode} {res.Content.Headers.ContentType} {bytes.Length} bytes");
    }
    catch (Exception ex) { Console.WriteLine($"{label}: EX {ex.GetType().Name} {ex.Message}"); }
}
