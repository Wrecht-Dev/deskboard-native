using Deskboard.Core.Api;
var client = new TumblrClient();
var result = await client.LoginAsync("deskboard-test@example.invalid", "not-a-real-password-123");
Console.WriteLine($"status={result.Status} cookies={result.CookieCount} message={result.Message}");
var bad = SetCookieParser.Parse(new[] { "sid=abc123; Path=/; Domain=.tumblr.com; Secure", "tmgioct=xyz; Path=/", "badcookie" });
Console.WriteLine($"parsed: {string.Join(",", bad.Select(kv => kv.Key + "=" + kv.Value))}");
