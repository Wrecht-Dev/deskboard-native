using System.Text.Json;
using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var page = await client.GetHomeAsync(4);
var post = page.Items.FirstOrDefault(p => p.NoteCount > 0);
Console.WriteLine($"post {post?.Id} by {post?.BlogName} notes={post?.NoteCount}");
if (post is null) return;
var req = new HttpRequestMessage(HttpMethod.Get,
    $"https://www.tumblr.com/api/v2/blog/{post.BlogName}/notes?id={post.Id}&mode=all&limit=5");
req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + File.ReadAllText(Path.Combine(Paths.ConfigDir, "api_token")).Trim());
req.Headers.TryAddWithoutValidation("Accept", "application/json;format=camelcase");
req.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", CookieStore.LoadStore().Select(kv => $"{kv.Key}={kv.Value}")));
using var http = new HttpClient();
var res = await http.SendAsync(req);
var body = await res.Content.ReadAsStringAsync();
Console.WriteLine($"HTTP {(int)res.StatusCode} len={body.Length}");
using var doc = JsonDocument.Parse(body);
var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;
Console.WriteLine("response keys: " + string.Join(",", root.EnumerateObject().Select(p => p.Name)));
if (root.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
{
    Console.WriteLine($"notes len={notes.GetArrayLength()}");
    if (notes.GetArrayLength() > 0)
        Console.WriteLine("note keys: " + string.Join(",", notes[0].EnumerateObject().Select(p => p.Name)));
    if (root.TryGetProperty("_links", out var links))
        Console.WriteLine("_links: " + links.GetRawText()[..Math.Min(200, links.GetRawText().Length)]);
}
