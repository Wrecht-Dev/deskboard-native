using System.Collections.Generic;
using Deskboard.Core.Models;
using Xunit;

namespace Deskboard.Tests;

public class MessagingTests
{
    [Fact]
    public void ConversationTitleUsesTheOtherParticipants()
        => Assert.Equal("dado8099", new Conversation { Participants = new() { "dado8099" } }.Title);

    [Fact]
    public void ConversationTitleJoinsTwoParticipants()
        => Assert.Equal("a and b", new Conversation { Participants = new() { "a", "b" } }.Title);

    [Fact]
    public void PreviewTruncatesLongMessages()
    {
        var text = new string('x', 200);
        var preview = new Conversation { LastMessage = text }.Preview;
        Assert.Equal(121, preview.Length);
        Assert.EndsWith("…", preview);
    }

    [Fact]
    public void MessageAuthorshipComparesToIdentity()
    {
        var mine = new Message { Participant = "adelaidesf" };
        var theirs = new Message { Participant = "dado8099" };
        Assert.True(mine.IsMineAgainst("adelaidesf"));
        Assert.False(theirs.IsMineAgainst("adelaidesf"));
    }

    [Fact]
    public void TimeLabelFormatsFromMilliseconds()
    {
        var message = new Message { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        Assert.Matches(@"^\d\d:\d\d$", message.TimeLabel);
    }
}
