using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using client.Views;
using client.Services; 
using System.Threading.Tasks;
using Avalonia.Threading;

namespace client.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ChatService _chatService;

        [ObservableProperty] private object _currentPage;

        // --- ЛОГІН ---
        [ObservableProperty] private string _usernameInput;
        [ObservableProperty] private string _passwordInput;

        // --- РЕЄСТРАЦІЯ ---
        [ObservableProperty] private string _regUsernameInput;
        [ObservableProperty] private string _regEmailInput;
        [ObservableProperty] private string _regPasswordInput;
        
        // --- ЗАБУЛИ ПАРОЛЬ ---
        [ObservableProperty] private string _forgotEmailInput;

        // --- СТАТУС (Помилки/Інфо) ---
        [ObservableProperty] private string _statusMessage;

        public MainWindowViewModel()
        {
            // Підключаємося до локального сервера (порт 5000 або 5001)
            _chatService = new ChatService("localhost", 5000);

            // Слухаємо успішний логін
            _chatService.OnLoggedIn += (id, nick) =>
            {
                Dispatcher.UIThread.Invoke(() => 
                {
                    StatusMessage = "Login Successful!";
                    var chatView = new ChatPage();
                    // Передаємо сервіс у ChatPage, щоб там працювали повідомлення
                    chatView.DataContext = new ChatPageViewModel(_chatService); 
                    CurrentPage = chatView;
                });
            };

            // Слухаємо системні повідомлення (логи)
            _chatService.OnLog += (msg) => 
            {
                 Dispatcher.UIThread.Invoke(() => StatusMessage = msg);
            };

            // Запускаємо підключення
            Task.Run(async () => await _chatService.ConnectAsync());

            // Показуємо екран логіну на старті
            GoToLogin(); 
        }

        // --- НАВІГАЦІЯ ---
        
        [RelayCommand]
        public void GoToLogin()
        {
            var view = new LoginView();
            view.DataContext = this;
            CurrentPage = view;
            StatusMessage = "";
        }

        [RelayCommand]
        public void GoToSignUp()
        {
            var view = new SignUpView();
            view.DataContext = this;
            CurrentPage = view;
            StatusMessage = "";
        }

        // --- ДОДАНО: Метод, якого не вистачало ---
        [RelayCommand]
        public void GoToForgotPassword()
        {
            var view = new ForgotPasswordView();
            view.DataContext = this;
            CurrentPage = view;
            StatusMessage = "";
        }

        // --- ДІЇ ---

        [RelayCommand]
        public async Task OnLogin()
        {
            if (string.IsNullOrWhiteSpace(UsernameInput) || string.IsNullOrWhiteSpace(PasswordInput))
            {
                StatusMessage = "Enter email & password";
                return;
            }
            StatusMessage = "Connecting...";
            await _chatService.LoginAsync(UsernameInput, PasswordInput);
        }

        [RelayCommand]
        public async Task OnRegister()
        {
            if (string.IsNullOrWhiteSpace(RegEmailInput) || 
                string.IsNullOrWhiteSpace(RegPasswordInput) || 
                string.IsNullOrWhiteSpace(RegUsernameInput))
            {
                StatusMessage = "Fill all fields!";
                return;
            }

            StatusMessage = "Sending registration request...";
            await _chatService.RegisterAsync(RegEmailInput, RegPasswordInput, RegUsernameInput);
        }

        // --- ДОДАНО: Метод для відновлення паролю ---
        [RelayCommand]
        public void OnRecoverPassword()
        {
            // Поки що просто повертаємо на логін
            StatusMessage = "Recovery request sent (Simulation)";
            GoToLogin();
        }
    }
}