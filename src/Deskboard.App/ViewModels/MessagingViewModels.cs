using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deskboard.Core.Models;

namespace Deskboard.App.ViewModels;

public sealed partial class ConversationItemViewModel : ObservableObject
{
    private readonly MainViewModel _host;
    public Conversation Conversation { get; }
    public string Title { get; }
    public string Preview { get; }
    public string Initial { get; }
    public string UpdatedLabel { get; }
    public bool HasUnread => Conversation.Unread > 0;

    public ConversationItemViewModel(Conversation conversation, MainViewModel host)
    {
        Conversation = conversation;
        _host = host;
        var others = conversation.Participants
            .Where(p => !p.Equals(host.Identity, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Title = others.Count > 0 ? string.Join(", ", others) : conversation.Title;
        Preview = conversation.Preview;
        Initial = Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";
        UpdatedLabel = conversation.Updated > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(conversation.Updated).ToLocalTime().ToString("MMM d")
            : "";
    }

    [RelayCommand]
    private Task Open() => _host.OpenConversationAsync(this);
}

public sealed partial class MessageItemViewModel : ObservableObject
{
    public string Text { get; }
    public string Author { get; }
    public string TimeLabel { get; }
    public bool IsMine { get; }
    public string Initial { get; }

    public MessageItemViewModel(Message message, string myName, Conversation conversation)
    {
        Text = message.Text;
        TimeLabel = message.TimeLabel;
        Author = conversation.ParticipantNames.TryGetValue(message.ParticipantKey, out var name)
            ? name : message.ParticipantKey;
        IsMine = Author.Equals(myName, StringComparison.OrdinalIgnoreCase);
        Initial = Author.Length > 0 ? Author[..1].ToUpperInvariant() : "?";
    }
}
