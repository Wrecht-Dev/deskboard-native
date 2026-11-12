using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var conv = await client.GetConversationsAsync();
var req = await client.GetConversationsAsync(requests: true);
Console.WriteLine($"conversations={conv.Conversations.Count} requests={req.Conversations.Count}");
