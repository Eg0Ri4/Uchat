using Avalonia.Controls;
using Avalonia.Interactivity;
using client.ViewModels;

namespace client.Views
{
    public partial class SignUpView : UserControl
    {
        public SignUpView() { InitializeComponent(); }

        private void OnRegisterClick(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm) vm.OnRegister();
        }

        private void OnBackToLoginClick(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm) vm.GoToLogin();
        }
    }
}