using Avalonia.Controls;
using Avalonia.Interactivity;
using client.ViewModels;

namespace client.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        // 1. КНОПКА "Sign in" (Вхід)
        private void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            // Отримуємо доступ до головної ViewModel
            if (DataContext is MainWindowViewModel vm)
            {
                // Викликаємо метод входу
                // Він візьме дані з полів (UsernameInput/PasswordInput), які прив'язані в XAML
                vm.OnLogin();
            }
        }

        // 2. КНОПКА "Sign up" (Перехід на реєстрацію)
        private void OnSignUpClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.GoToSignUp();
            }
        }

        // 3. КНОПКА "Forgot password?"
        private void OnForgotPasswordClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.GoToForgotPassword();
            }
        }
    }
}