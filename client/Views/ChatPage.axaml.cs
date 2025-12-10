using Avalonia.Controls;
using Avalonia.Input;
using client.ViewModels;

namespace client.Views
{
    public partial class ChatPage : UserControl
    {
        public ChatPage()
        {
            InitializeComponent();
        }
private void OnMessageBoxKeyDown(object? sender, KeyEventArgs e)
{
    // простий Enter без модифікаторів → відправити повідомлення
    if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
    {
        if (DataContext is ChatPageViewModel vm &&
            !string.IsNullOrWhiteSpace(vm.NewMessageText))
        {
            vm.SendMessageCommand.Execute(null);
        }

        // кажемо TextBox'у: ми самі все обробили, не вставляй новий рядок
        e.Handled = true;
    }
    // Shift+Enter:
    // e.Key == Enter, але e.KeyModifiers == KeyModifiers.Shift
    // ми НІЧОГО не робимо → подія не Handled → TextBox сам вставляє новий рядок
}
    }
}
