using Deskboard.Core.Api;
using Deskboard.Core.Models;
var draft = new ComposerDraft { Text = "hello there", Tags = "art, design" };
draft.Images.Add(new UploadedMedia { Url = "https://64.media.tumblr.com/x.png", Width = 800, Height = 600 });
Console.WriteLine(TumblrClient.BuildContentJson(draft));
var client = new TumblrClient();
await client.EnsureTokenAsync();
var (name, blogs) = await client.GetIdentityAsync();
var blog = blogs.FirstOrDefault() ?? name;
try
{
    var drafts = await client.GetDraftsAsync(blog);
    Console.WriteLine($"drafts: {drafts.Items.Count} | next={drafts.NextUrl is not null}");
    var queue = await client.GetQueueAsync(blog);
    Console.WriteLine($"queue: {queue.Items.Count} | next={queue.NextUrl is not null}");
    var inbox = await client.GetInboxAsync();
    Console.WriteLine($"inbox: {inbox.Items.Count} | next={inbox.NextUrl is not null}");
}
catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
