using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var page = await client.GetHomeAsync(14);
var video = page.Items.SelectMany(p => p.Content).SelectMany(b => b.Media).FirstOrDefault(m => m.Kind == "video");
Console.WriteLine(video is null ? "no video in home" : video.Url);
var tagged = await client.GetTaggedAsync("video", "top", 10);
var v2 = tagged.Items.SelectMany(p => p.Content).SelectMany(b => b.Media).FirstOrDefault(m => m.Kind == "video");
Console.WriteLine(v2 is null ? "no video in tag" : v2.Url);
