using Deskboard.Core.Api;
using Xunit;

namespace Deskboard.Tests;

public class LoginTests
{
    [Fact]
    public void ParsesNameValuePairs()
    {
        var jar = SetCookieParser.Parse(new[]
        {
            "sid=abc123; Path=/; Domain=.tumblr.com; Secure; HttpOnly",
            "tmgioct=xyz; Path=/; Max-Age=3600",
        });
        Assert.Equal("abc123", jar["sid"]);
        Assert.Equal("xyz", jar["tmgioct"]);
    }

    [Fact]
    public void IgnoresMalformedAndDeletionCookies()
    {
        var jar = SetCookieParser.Parse(new[]
        {
            "no-equals-sign",
            "empty=",
            "gone=deleted; Path=/",
            "ok=fine",
        });
        Assert.False(jar.ContainsKey("gone"));
        Assert.False(jar.ContainsKey("empty"));
        Assert.Equal("fine", jar["ok"]);
        Assert.Single(jar);
    }

    [Fact]
    public void KeepsValuesContainingEquals()
    {
        var jar = SetCookieParser.Parse(new[] { "token=a=b=c; Path=/" });
        Assert.Equal("a=b=c", jar["token"]);
    }

    [Fact]
    public void SignOutOverwritesNativeSessionAndDisablesBrowserRefresh()
    {
        var dir = Directory.CreateTempSubdirectory("deskboard-session-");
        try
        {
            var cookies = new Dictionary<string, string> { ["sid"] = "test-session" };
            CookieStore.SaveStore(cookies, dir.FullName);
            using var client = new TumblrClient(dataDir: dir.FullName, cookies: cookies)
            {
                AutoRefreshSession = true,
            };
            client.SignOut();

            Assert.False(client.HasSession);
            Assert.False(client.AutoRefreshSession);
            var path = Path.Combine(dir.FullName, "cookies.json");
            using var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Assert.Empty(saved.RootElement.GetProperty("cookies").EnumerateObject());
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void NetscapeImportKeepsOnlyTumblrDomains()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[]
            {
                "#HttpOnly_.tumblr.com	TRUE	/	TRUE	0	sid	first",
                "www.tumblr.com	FALSE	/	TRUE	0	other	second",
                "nottumblr.com	FALSE	/	TRUE	0	sid	unexpected",
            });
            var cookies = CookieStore.LoadNetscapeFile(path);
            Assert.Equal(2, cookies.Count);
            Assert.Equal("first", cookies["sid"]);
        }
        finally { File.Delete(path); }
    }
}
