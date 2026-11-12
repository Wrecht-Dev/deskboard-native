using Deskboard.Core.Api;

using var client = new TumblrClient();
Console.WriteLine($"cookies={client.HasSession} token={(await client.EnsureTokenAsync() ? "ok" : "fail")}");
var home = await client.GetHomeAsync(14);
Console.WriteLine($"home: {home.Items.Count} | next={home.NextUrl is not null}");
foreach (var p in home.Items.Take(4)) Console.WriteLine($"  {p.BlogName,-26} blocks={p.Content.Count,2} trail={p.Trail.Count,2} notes={p.NoteCount}");
var likes = await client.GetLikesAsync(10);
Console.WriteLine($"likes: {likes.Items.Count} | total={likes.Total}");
var following = await client.GetFollowingAsync();
Console.WriteLine($"following: {following.Items.Count} | next={following.NextUrl is not null}");
var post = home.Items.FirstOrDefault(p => p.Content.Count > 0 || p.Trail.Count > 0);
if (post is not null) Console.WriteLine($"first text: {(post.Content.FirstOrDefault()?.Text ?? post.Trail[0].Content[0].Text)[..Math.Min(80, (post.Content.FirstOrDefault()?.Text ?? "x").Length)]}");
