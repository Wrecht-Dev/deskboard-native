using System.Text.Json;
using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var root = await client.RawGetAsync("/v2/tabs/for_you", new Dictionary<string, string?> { ["notes_info"] = "false", ["reblog_info"] = "true", ["limit"] = "5" });
var elements = root.GetProperty("response").GetProperty("timeline").GetProperty("elements");
foreach (var e in elements.EnumerateArray())
{
    if (!e.TryGetProperty("objectType", out var ot) || ot.GetString() != "post") continue;
    var blog = e.GetProperty("blog");
    var flags = new[] { "isAdult", "isNSFW", "isNsfw", "adult" }
        .Where(f => blog.TryGetProperty(f, out _) || e.TryGetProperty(f, out _))
        .Select(f => $"{f}={(blog.TryGetProperty(f, out var v1) ? v1.GetRawText() : e.GetProperty(f).GetRawText())}");
    Console.WriteLine($"{blog.GetProperty("name").GetString(),-24} {string.Join(" ", flags)}");
}
