using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using client.Models;
using client.Services; // <--- ОБОВ'ЯЗКОВО: Доступ до ChatService
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading; // <--- ОБОВ'ЯЗКОВО: Для оновлення UI з іншого потоку
using System.Threading.Tasks;

namespace client.ViewModels
{
    public class ChatPageViewModel : ViewModelBase
    {
        // --- ЗБЕРІГАЄМО СЕРВІС ---
        private readonly ChatService _chatService;

        // ----------------- TITLE -----------------
        private string _welcomeTitle = "Chat";
        public string WelcomeTitle
        {
            get => _welcomeTitle;
            set => SetProperty(ref _welcomeTitle, value);
        }

        // ----------------- ACCOUNTS & CHATS -----------------
        public ObservableCollection<AccountInfo> Accounts { get; } = new();
        public ObservableCollection<ChatDialog> Chats { get; } = new();
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        private AccountInfo? _selectedAccount;
        public AccountInfo? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (SetProperty(ref _selectedAccount, value))
                {
                    // Тут можна додати логіку завантаження чатів
                }
            }
        }

        private ChatDialog? _selectedChat;
        public ChatDialog? SelectedChat
        {
            get => _selectedChat;
            set
            {
                if (SetProperty(ref _selectedChat, value))
                {
                    Messages.Clear();
                    if (_selectedChat != null)
                    {
                        foreach (var msg in _selectedChat.Messages)
                            Messages.Add(msg);
                    }
                }
            }
        }

        // ----------------- INPUTS -----------------
        private ChatMessage? _editingMessage;
        public ChatMessage? EditingMessage
        {
            get => _editingMessage;
            set => SetProperty(ref _editingMessage, value);
        }

        private string _newMessageText = string.Empty;
        public string NewMessageText
        {
            get => _newMessageText;
            set => SetProperty(ref _newMessageText, value);
        }

        // ----------------- COMMANDS -----------------
        public ICommand SendMessageCommand { get; }
        public ICommand EditLastMessageCommand { get; }
        public ICommand DeleteLastMessageCommand { get; }
        public ICommand EditMessageCommand { get; }
        public ICommand DeleteMessageCommand { get; }

        // ----------------- КОНСТРУКТОР (ОСНОВНИЙ) -----------------
        public ChatPageViewModel(ChatService chatService)
        {
            _chatService = chatService;
            
            // Відображаємо нік, під яким зайшли
            WelcomeTitle = $"Logged in as: {_chatService.MyNick}";

            // 1. ПІДПИСКА НА ВХІДНІ ПОВІДОМЛЕННЯ
            _chatService.OnMessageReceived += OnMessageReceived;

            // Ініціалізація команд
            SendMessageCommand = new AsyncRelayCommand(SendCurrentMessage); 
            
            EditLastMessageCommand = new RelayCommand(_ => EditLastMessage());
            DeleteLastMessageCommand = new RelayCommand(_ => DeleteLastMessage());
            EditMessageCommand = new RelayCommand(msg => StartEdit(msg as ChatMessage));
            DeleteMessageCommand = new RelayCommand(msg => DeleteMessage(msg as ChatMessage));

            // Завантажуємо тестові чати або історію (якщо є)
            LoadFakeChats();
        }
        
        public ChatPageViewModel() 
        {
            WelcomeTitle = "Design Mode";
        }

        // ----------------- ЛОГІКА ПРИЙОМУ (REALTIME) -----------------
        private void OnMessageReceived(string sender, string text)
        {
            // SignalR викликає це з фонового потоку -> перемикаємо на UI
            Dispatcher.UIThread.Invoke(() => 
            {
                // 1. Шукаємо чат з цим відправником (по ніку)
                var chat = Chats.FirstOrDefault(c => c.Title == sender);
                
                // 2. Якщо чату немає - створюємо новий
                if (chat == null)
                {
                    chat = new ChatDialog { Id = new Random().Next(1, 99999), Title = sender };
                    Chats.Add(chat);
                }

                // 3. Формуємо повідомлення
                var msg = new ChatMessage 
                { 
                    MessageText = text, 
                    IsIncoming = true,
                    Time = DateTime.Now.ToShortTimeString()
                };

                // 4. Додаємо в пам'ять чату
                chat.Messages.Add(msg);

                // 5. Якщо цей чат зараз відкритий - показуємо на екрані
                if (SelectedChat == chat)
                {
                    Messages.Add(msg);
                }
            });
        }

        // ----------------- ЛОГІКА ВІДПРАВКИ (REALTIME) -----------------
        private async Task SendCurrentMessage()
        {
            if (string.IsNullOrWhiteSpace(NewMessageText) || SelectedChat == null)
                return;

            string textToSend = NewMessageText;
            string targetNick = SelectedChat.Title; // Нік отримувача
            
            NewMessageText = string.Empty; // Очищаємо поле

            try 
            {
                // 1. Додаємо візуально собі (як відправлене)
                var myMessage = new ChatMessage
                {
                    MessageText = textToSend,
                    IsIncoming = false,
                    Time = DateTime.Now.ToShortTimeString()
                };
                SelectedChat.Messages.Add(myMessage);
                Messages.Add(myMessage);

                // 2. ВІДПРАВЛЯЄМО ЧЕРЕЗ СЕРВІС
                // Сервіс сам знайде ID, зашифрує і відправить
                await _chatService.SendMessageAsync(targetNick, textToSend);
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage { MessageText = $"[Error]: {ex.Message}", IsIncoming = true });
            }
        }

        // ----------------- ІНШІ МЕТОДИ (Load, Edit, Delete) -----------------
        private void LoadFakeChats()
        {
            // Створюємо стартовий чат (наприклад, з EchoBot або іншим юзером)
            // Важливо: Title має співпадати з ніком, щоб працювала відправка
            var chat1 = new ChatDialog { Id = 1, Title = "EchoBot" }; 
            chat1.Messages.Add(new ChatMessage { MessageText = "Type something...", IsIncoming = true });
            Chats.Add(chat1);
            
            SelectedChat = chat1;
        }

        private void EditLastMessage()
        {
            var msg = Messages.LastOrDefault(m => !m.IsIncoming && !m.IsDeleted);
            if (msg != null)
            {
                EditingMessage = msg;
                NewMessageText = msg.MessageText;
            }
        }

        private void DeleteLastMessage()
        {
            var msg = Messages.LastOrDefault(m => !m.IsIncoming && !m.IsDeleted);
            if (msg != null)
            {
                msg.IsDeleted = true;
                msg.MessageText = string.Empty;
            }
        }

        public void StartEdit(ChatMessage? message)
        {
            if (message != null && !message.IsIncoming)
            {
                EditingMessage = message;
                NewMessageText = message.MessageText;
            }
        }

        public void DeleteMessage(ChatMessage? message)
        {
            if (message != null && !message.IsIncoming)
            {
                message.IsDeleted = true;
                message.MessageText = string.Empty;
            }
        }
    }
}