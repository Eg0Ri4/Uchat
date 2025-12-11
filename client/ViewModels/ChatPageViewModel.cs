using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using client.Models;
using CommunityToolkit.Mvvm.Input; // Переконайся, що цей using є, якщо ні - додай

namespace client.ViewModels
{
    public class ChatPageViewModel : ViewModelBase
    {
        // ----------------- NEW: TITLE (ПРИВІТАННЯ) -----------------
        private string _welcomeTitle = "Chat";
        public string WelcomeTitle
        {
            get => _welcomeTitle;
            set => SetProperty(ref _welcomeTitle, value);
        }

        // ----------------- ACCOUNTS -----------------
        public ObservableCollection<AccountInfo> Accounts { get; } = new();

        private AccountInfo? _selectedAccount;
        public AccountInfo? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (_selectedAccount == value) return;
                _selectedAccount = value;
                LoadChatsForAccount(_selectedAccount);
                OnPropertyChanged(nameof(SelectedAccount)); // На всяк випадок
            }
        }

        // ----------------- CHATS -----------------
        public ObservableCollection<ChatDialog> Chats { get; } = new();

        private ChatDialog? _selectedChat;
        public ChatDialog? SelectedChat
        {
            get => _selectedChat;
            set
            {
                if (_selectedChat == value) return;
                _selectedChat = value;
                
                Messages.Clear();
                if (_selectedChat != null)
                {
                    foreach (var msg in _selectedChat.Messages)
                        Messages.Add(msg);
                }
                OnPropertyChanged(nameof(SelectedChat));
            }
        }

        // ----------------- MESSAGES -----------------
        public ObservableCollection<ChatMessage> Messages { get; } = new();

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


        // ----------------- CONSTRUCTORS (ОСЬ ТУТ БУЛА ПОМИЛКА) -----------------

        // 1. Головний конструктор (ініціалізує дані)
        public ChatPageViewModel()
        {
            // Тестові акаунти
            Accounts.Add(new AccountInfo { Username = "vira",      IsActive = true });
            Accounts.Add(new AccountInfo { Username = "testUser",  IsActive = false });

            SelectedAccount = Accounts[0];

            // Команди
            SendMessageCommand       = new RelayCommand(_ => SendCurrentMessage());
            EditLastMessageCommand   = new RelayCommand(_ => EditLastMessage());
            DeleteLastMessageCommand = new RelayCommand(_ => DeleteLastMessage());
            
            // Тут я роз'єднав рядки, які злиплися
            EditMessageCommand       = new RelayCommand(msg => StartEdit(msg as ChatMessage));
            DeleteMessageCommand     = new RelayCommand(msg => DeleteMessage(msg as ChatMessage));
            
            WelcomeTitle = "Preview Mode";
        }

        // 2. ДОДАНИЙ КОНСТРУКТОР: Приймає ім'я (виправляє CS1729)
        // : this() означає "спочатку виконай код з конструктора вище, потім цей"
        public ChatPageViewModel(string username) : this()
        {
            if (!string.IsNullOrEmpty(username))
            {
                WelcomeTitle = $"Привіт, {username}!";
            }
            else
            {
                WelcomeTitle = "Привіт, Користувач!";
            }
        }

        // ----------------- LOADING CHATS -----------------
        private void LoadChatsForAccount(AccountInfo? account)
        {
            Chats.Clear();
            Messages.Clear();

            if (account == null) return;

            // Чат 1
            var chat1 = new ChatDialog { Id = 1, Title = "@snake126" };
            chat1.Messages.Add(new ChatMessage { MessageText = "Hello!",            IsIncoming = true  });
            chat1.Messages.Add(new ChatMessage { MessageText = "Hi! How are you?",  IsIncoming = false });
            chat1.Messages.Add(new ChatMessage { MessageText = "I'm fine, thanks!", IsIncoming = true  });
            chat1.Messages.Add(new ChatMessage { MessageText = "Nice to hear 😊",    IsIncoming = false });

            // Чат 2 (група)
            var chat2 = new ChatDialog { Id = 2, Title = "Group: Lab Team" };
            chat2.Messages.Add(new ChatMessage { MessageText = "Hi everyone!",      IsIncoming = true  });
            chat2.Messages.Add(new ChatMessage { MessageText = "Hi! 👋",            IsIncoming = false });
            chat2.Messages.Add(new ChatMessage { MessageText = "Ready for lab?",    IsIncoming = true  });
            chat2.Messages.Add(new ChatMessage { MessageText = "Almost 😅",         IsIncoming = false });

            // Чат 3
            var chat3 = new ChatDialog { Id = 3, Title = "@miniuser" };
            chat3.Messages.Add(new ChatMessage { MessageText = "Yo!",               IsIncoming = true  });
            chat3.Messages.Add(new ChatMessage { MessageText = "Working on uchat",  IsIncoming = false });

            Chats.Add(chat1);
            Chats.Add(chat2);
            Chats.Add(chat3);

            SelectedChat = chat1;
        }

        // ----------------- ACTIONS -----------------
        private void SendCurrentMessage()
        {
            if (string.IsNullOrWhiteSpace(NewMessageText) || SelectedChat == null)
                return;

            // Редагування
            if (EditingMessage != null)
            {
                EditingMessage.MessageText = NewMessageText;
                EditingMessage.Time        = DateTime.Now.ToShortTimeString();
                EditingMessage.IsEdited    = true;

                EditingMessage = null;
                NewMessageText = string.Empty;
                return;
            }

            // Нове повідомлення
            var myMessage = new ChatMessage
            {
                MessageText = NewMessageText,
                IsIncoming  = false   // моє
            };

            SelectedChat.Messages.Add(myMessage);
            Messages.Add(myMessage);

            // Авто-відповідь
            var reply = new ChatMessage
            {
                MessageText = "Auto-reply: got it ✅",
                IsIncoming  = true
            };

            SelectedChat.Messages.Add(reply);
            Messages.Add(reply);

            NewMessageText = string.Empty;
        }

        private void EditLastMessage()
        {
            var msg = Messages.LastOrDefault(m => !m.IsIncoming && !m.IsDeleted);
            if (msg == null) return;

            EditingMessage = msg;
            NewMessageText = msg.MessageText;
        }

        private void DeleteLastMessage()
        {
            var msg = Messages.LastOrDefault(m => !m.IsIncoming && !m.IsDeleted);
            if (msg == null) return;

            msg.IsDeleted   = true;
            msg.MessageText = string.Empty;
        }

        public void StartEdit(ChatMessage? message)
        {
            if (message == null) return;
            if (message.IsIncoming) return;

            EditingMessage = message;
            NewMessageText = message.MessageText;
        }

        public void DeleteMessage(ChatMessage? message)
        {
            if (message == null) return;
            if (message.IsIncoming) return;

            message.IsDeleted   = true;
            message.MessageText = string.Empty;
        }
    }
}