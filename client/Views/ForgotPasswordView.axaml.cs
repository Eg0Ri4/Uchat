using Avalonia.Controls;
using Avalonia.Interactivity;
using client.ViewModels;

namespace client.Views
{
    public partial class ForgotPasswordView : UserControl
    {
        public ForgotPasswordView()
        {
            InitializeComponent();
        }

        private void OnRecoverClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.OnRecoverPassword();
            }
        }

        private void OnBackToLoginClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.GoToLogin();
            }
        }
    }
}