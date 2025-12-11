using Avalonia.Controls;
using Avalonia.Interactivity;
using client.ViewModels;

namespace client.Views
{
    public partial class SignUpView : UserControl
    {
        public SignUpView()
        {
            InitializeComponent();
        }

        // Цей метод спрацює, коли ти натиснеш кнопку
        private void OnRegisterClick(object? sender, RoutedEventArgs e)
        {
            // Ми "дістаємо" ViewModel, яка прив'язана до цього вікна
            if (DataContext is MainWindowViewModel vm)
            {
                // І вручну викликаємо метод реєстрації
                vm.OnRegister();
            }
        }

        // Цей метод для кнопки "Sign In" (повернутися назад)
        private void OnBackToLoginClick(object? sender, RoutedEventArgs e)
        {
             if (DataContext is MainWindowViewModel vm)
            {
                vm.GoToLogin();
            }
        }
    }
}