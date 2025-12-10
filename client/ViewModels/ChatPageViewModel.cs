using System.Collections.ObjectModel;
using client.Models;

namespace client.ViewModels
{
    public class ChatPageViewModel : ViewModelBase
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ChatPageViewModel()
        {
            Messages.Add(new ChatMessage { MessageText = "Hello!", IsIncoming = true });
            Messages.Add(new ChatMessage { MessageText = "Hi! How are you?", IsIncoming = false });
            Messages.Add(new ChatMessage { MessageText = "Looks great!", IsIncoming = true });
            Messages.Add(new ChatMessage { MessageText = "уаццууца great!", IsIncoming = true });
        }
    }
}
