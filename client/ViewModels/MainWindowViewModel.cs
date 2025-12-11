using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using client.Views;
using System.IO;

namespace client.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty] private object _currentPage;

        // --- ЗМІННІ ДЛЯ ЛОГІНУ ---
        [ObservableProperty] private string _usernameInput;
        [ObservableProperty] private string _passwordInput;

        // --- ЗМІННІ ДЛЯ РЕЄСТРАЦІЇ ---
        [ObservableProperty] private string _regUsernameInput;
        [ObservableProperty] private string _regEmailInput;
        [ObservableProperty] private string _regPasswordInput;

        // --- ЗМІННІ ДЛЯ ВІДНОВЛЕННЯ ---
        [ObservableProperty] private string _forgotEmailInput;

        public MainWindowViewModel()
        {
            GoToLogin(); 
        }

        // --- НАВІГАЦІЯ ---
        public void GoToLogin()
        {
            var view = new LoginView();
            view.DataContext = this;
            CurrentPage = view;
        }

        public void GoToSignUp()
        {
            var view = new SignUpView();
            view.DataContext = this;
            CurrentPage = view;
        }

        public void GoToForgotPassword()
        {
            var view = new ForgotPasswordView();
            view.DataContext = this;
            CurrentPage = view;
        }

        // --- ДІЇ ---
        [RelayCommand]
        public void OnLogin()
        {
            // Логіка входу
            string data = $"LOGIN: {UsernameInput} | {PasswordInput}";
            File.WriteAllText("login_log.txt", data);

            var chatView = new ChatPage();
            chatView.DataContext = new ChatPageViewModel(UsernameInput);
            CurrentPage = chatView;
        }

        [RelayCommand]
        public void OnRegister()
        {
            var chatView = new ChatPage();
            chatView.DataContext = new ChatPageViewModel(RegUsernameInput);
            CurrentPage = chatView;
        }

        [RelayCommand]
        public void OnRecoverPassword()
        {
            GoToLogin();
        }
    }
}