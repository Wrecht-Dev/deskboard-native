using System.Text.Json;
using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var root = await client.RawGetAsync("/v2/gif/popular", new Dictionary<string, string?> { ["offset"] = "0" });
var resp = root.TryGetProperty("response", out var r) ? r : root;
Console.WriteLine("response kind=" + resp.ValueKind + " keys=" + (resp.ValueKind == JsonValueKind.Object ? string.Join(",", resp.EnumerateObject().Select(p => p.Name)) : "-"));
var raw = root.GetRawText();
Console.WriteLine("head: " + raw[..Math.Min(600, raw.Length)]);
