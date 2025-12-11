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

        // Кнопка "Sign in"
        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.OnLogin();
            }
        }

        // Кнопка "Sign up" (НОВЕ)
        private void OnSignUpClick(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.GoToSignUp();
            }
        }

        // Кнопка "Forgot password?" (НОВЕ)
        private void OnForgotPasswordClick(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.GoToForgotPassword();
            }
        }
    }
}