using Deskboard.Core.Api;
var client = new TumblrClient();
await client.EnsureTokenAsync();
var (name, blogs) = await client.GetIdentityAsync();
var page = await client.GetConversationsAsync();
Console.WriteLine($"conversations: {page.Conversations.Count} | next={page.NextUrl is not null}");
foreach (var c in page.Conversations.Take(2))
    Console.WriteLine($"  {c.Title,-34} unread={c.Unread} preview=\"{c.Preview}\"");
var first = page.Conversations.FirstOrDefault(c => c.Participants.Count > 1);
if (first is not null)
{
    var msgs = await client.GetMessagesAsync(first.Id);
    Console.WriteLine($"thread id={first.Id} msgs={msgs.Count}");
    foreach (var m in msgs.Take(4))
    {
        var who = first.ParticipantNames.TryGetValue(m.ParticipantKey, out var n) ? n : m.ParticipantKey;
        Console.WriteLine($"  {(who.Equals(name, StringComparison.OrdinalIgnoreCase) ? "ME" : who),-18} {m.TimeLabel} {m.Text[..Math.Min(40, m.Text.Length)]}");
    }
}
