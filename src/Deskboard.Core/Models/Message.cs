namespace Deskboard.Core.Models;

public sealed class Conversation
{
    public string Id { get; init; } = "";
    public List<string> Participants { get; init; } = new();
    /// <summary>participant key (t:…) → blog name, for resolving message authorship.</summary>
    public Dictionary<string, string> ParticipantNames { get; init; } = new();
    public string LastMessage { get; init; } = "";
    public long Updated { get; init; }
    public int Unread { get; init; }
    public bool CanSend { get; init; } = true;

    public string Title => Participants.Count switch
    {
        0 => "(no messages)",
        1 => Participants[0],
        2 => Participants[0] + " and " + Participants[1],
        _ => $"{Participants[0]} and {Participants.Count - 1} others",
    };

    public string Preview => LastMessage.Length > 120 ? LastMessage[..120] + "…" : LastMessage;
}

public sealed class Message
{
    public string Id { get; init; } = "";
    public string Participant { get; init; } = "";
    public string ParticipantKey { get; init; } = "";
    public string Text { get; init; } = "";
    public long Timestamp { get; init; }
    public string Type { get; init; } = "text";
    public bool IsMine { get; init; }

    public string TimeLabel => Timestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).ToLocalTime().ToString("HH:mm")
        : "";

    public bool IsMineAgainst(string myName)
        => Participant.Length > 0 && Participant.Equals(myName, StringComparison.OrdinalIgnoreCase);
}

public sealed class ConversationsPage
{
    public List<Conversation> Conversations { get; init; } = new();
    public string? NextUrl { get; init; }
}
