using Deskboard.App.Services;
using Xunit;

namespace Deskboard.Tests;

public class VersionTests
{
    [Theory]
    [InlineData("v0.2.0", "0.1.3", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("v0.1.4", "0.2.0", false)]
    [InlineData("v1.0.0", "0.9.9", true)]
    [InlineData("v0.10.0", "0.9.0", true)]
    [InlineData("v0.2.0-beta.1", "0.2.0", false)]
    public void ComparesDottedVersions(string latest, string current, bool expected)
        => Assert.Equal(expected, AppInfo.IsNewer(latest, current));
}
