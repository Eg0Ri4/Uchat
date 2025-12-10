using Avalonia.Controls;

namespace client.Views // 👈 ПРАВИЛЬНИЙ NAMESPACE
{
    // 'partial' обов'язковий для Avalonia
    public partial class ForgotPasswordView : UserControl // 👈 ПРАВИЛЬНА НАЗВА КЛАСУ
    {
        public ForgotPasswordView()
        {
            InitializeComponent(); // Ця функція запрацює
        }
    }
}
