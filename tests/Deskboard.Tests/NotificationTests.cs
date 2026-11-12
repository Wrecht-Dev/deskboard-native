using Deskboard.Core.Models;
using Xunit;

namespace Deskboard.Tests;

public class NotificationTests
{
    [Theory]
    [InlineData("like", "liked your post")]
    [InlineData("reblog", "reblogged your post")]
    [InlineData("reply", "replied to your post")]
    [InlineData("follow", "started following you")]
    [InlineData("follower", "started following you")]
    [InlineData("ask", "sent you an ask")]
    public void ActionLabelsMatchTheWebsite(string type, string expected)
        => Assert.Equal(expected, new Notification { Type = type }.Action);

    [Fact]
    public void FollowBackOnlyWhenTheyFollowAndYouDont()
    {
        Assert.True(new Notification { Type = "follow", FollowingYou = true, Followed = false }.NeedsFollowBack);
        Assert.False(new Notification { Type = "follow", FollowingYou = true, Followed = true }.NeedsFollowBack);
        Assert.False(new Notification { Type = "like", FollowingYou = true }.NeedsFollowBack);
    }

    [Fact]
    public void DisplayNameFallsBackToBlogName()
    {
        Assert.Equal("artspotting", new Notification { BlogName = "artspotting" }.DisplayName);
        Assert.Equal("Art Spotting", new Notification { BlogName = "artspotting", BlogTitle = "Art Spotting" }.DisplayName);
    }
}
