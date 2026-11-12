using Deskboard.Core.Api;
var client = new TumblrClient();
Console.WriteLine($"hasSession={client.HasSession}");
await client.EnsureTokenAsync();
try
{
    var (name, blogs) = await client.GetIdentityAsync();
    Console.WriteLine($"identity='{name}' blogs=[{string.Join(",", blogs)}]");
}
catch (Exception ex) { Console.WriteLine("identity ERROR: " + ex.Message); }
