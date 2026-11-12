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
}
