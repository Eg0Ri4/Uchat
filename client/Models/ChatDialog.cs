using System.Collections.ObjectModel;

namespace client.Models
{
    public class ChatDialog
    {
        public int Id { get; set; }                // унікальний ID чату (потім потрібно для сервера)
        public string Title { get; set; } = "";    // назва чату (@user чи Group)
        
        // Повідомлення в цьому чаті
        public ObservableCollection<ChatMessage> Messages { get; } = new();
    }
}
