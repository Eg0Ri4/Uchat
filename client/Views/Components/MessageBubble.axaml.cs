using Avalonia.Controls;
using Avalonia.Interactivity;
using client.Models;
using client.ViewModels;
using client.Views;   // тут лежить ChatPage

namespace client.Views.Components
{
    public partial class MessageBubble : UserControl
    {
        public MessageBubble()
        {
            InitializeComponent();
        }

        private ChatPage? FindChatPage()
        {
            // Піднімаємось по дереву елементів через Parent
            Control? current = this;

            while (current != null)
            {
                if (current is ChatPage chatPage)
                    return chatPage;

                current = current.Parent as Control;
            }

            return null;
        }

        private void OnEditClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ChatMessage message)
                return;

            var chatPage = FindChatPage();
            if (chatPage?.DataContext is ChatPageViewModel vm)
            {
                vm.StartEdit(message);
            }
        }

        private void OnDeleteClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ChatMessage message)
                return;

            var chatPage = FindChatPage();
            if (chatPage?.DataContext is ChatPageViewModel vm)
            {
                vm.DeleteMessage(message);
            }
        }
    }
}
