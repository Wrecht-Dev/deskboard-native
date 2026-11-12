using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var tags = await client.GetTrendingTagsAsync();
Console.WriteLine($"trending: {tags.Count} tags -> {string.Join(", ", tags.Take(6))}");
