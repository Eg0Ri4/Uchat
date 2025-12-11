using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Потрібно для списків
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace client.Services
{
    public class ChatService
    {
        private HubConnection _connection;
        private string _activeMail;
        private string _activeKeyPath => $"{_activeMail}.key";

        // Словник: Нікнейм -> ID Чату (як у консолі activeChats)
        private Dictionary<string, long> _activeChats = new Dictionary<string, long>();

        // Події для UI
        public event Action<string> OnLog; 
        public event Action<string, string> OnMessageReceived; // (Sender, PlainText)
        public event Action<long, string> OnLoggedIn;

        public long MyId { get; private set; } = -1;
        public string MyNick { get; private set; } = "Guest";

        public ChatService(string ip, int port)
        {
            string hubUrl = $"http://{ip}:{port}/hubs/daemon";
            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            RegisterHandlers();
        }

        public async Task ConnectAsync()
        {
            try {
                await _connection.StartAsync();
                OnLog?.Invoke("Connected to Server!");
            } catch (Exception ex) {
                OnLog?.Invoke($"Connection failed: {ex.Message}");
            }
        }

        private void RegisterHandlers()
        {
            // 1. Успішний вхід
            _connection.On<long, string>("LoginSuccess", (id, nick) =>
            {
                MyId = id;
                MyNick = nick;
                OnLoggedIn?.Invoke(id, nick);
                OnLog?.Invoke($"Logged in as {nick}");
            });

            // 2. Отримання приватного ключа (при реєстрації)
            _connection.On<string>("ReceivePrivateKey", (key) => {
                File.WriteAllText(_activeKeyPath, key);
                OnLog?.Invoke("Registration Successful. Key saved.");
            });

            // 3. Чат створено (зберігаємо ID)
            _connection.On<long, string>("ChatCreated", (chatId, targetNick) =>
            {
                _activeChats[targetNick] = chatId;
                OnLog?.Invoke($"Chat #{chatId} started with {targetNick}");
            });

            // 4. ОТРИМАННЯ ПОВІДОМЛЕННЯ (Розшифровка)
            _connection.On<string, string, string, string, long>("ReceiveSecureMessage", (sender, cipherText, iv, myEncryptedKey, chatId) =>
            {
                try 
                {
                    if (!File.Exists(_activeKeyPath)) 
                    {
                        OnMessageReceived?.Invoke(sender, "[LOCKED - No Key]");
                        return;
                    }

                    string myPrivKey = File.ReadAllText(_activeKeyPath);
                    
                    // А. Розшифровуємо ключ сесії (RSA)
                    byte[] sessionKey = CryptographyService.DecryptSessionKey(myEncryptedKey, myPrivKey);
                    
                    // Б. Розшифровуємо текст повідомлення (AES)
                    string plainText = CryptographyService.DecryptMessage(cipherText, iv, sessionKey);

                    // В. Віддаємо чистий текст у UI
                    OnMessageReceived?.Invoke(sender, plainText);
                }
                catch (Exception ex)
                {
                    // Це та помилка, яку ти бачив на скріншоті. 
                    // Тепер ми її ловимо і показуємо текст помилки.
                    OnMessageReceived?.Invoke(sender, $"[Decryption Error: {ex.Message}]");
                }
            });
        }

        // --- МЕТОДИ ДЛЯ UI ---

        public async Task LoginAsync(string email, string password)
        {
            _activeMail = email;
            await _connection.InvokeAsync("LogIn", email, password);
        }

        public async Task RegisterAsync(string email, string password, string nick)
        {
            _activeMail = email;
            await _connection.InvokeAsync("register", email, password, nick);
        }

        // ГОЛОВНИЙ МЕТОД: ВІДПРАВКА (Шифрування)
        public async Task SendMessageAsync(string targetNick, string messageText)
        {
            // 1. Перевіряємо, чи є у нас ID чату з цим юзером
            if (!_activeChats.ContainsKey(targetNick))
            {
                // Якщо немає - пробуємо ініціалізувати (створити) чат
                await _connection.InvokeAsync("InitPrivateChat", targetNick, MyId);
                // Даємо серверу трохи часу відповісти (костиль, але надійно для початку)
                await Task.Delay(200); 
                
                if (!_activeChats.ContainsKey(targetNick))
                {
                    throw new Exception($"Could not start chat with {targetNick}. Try again.");
                }
            }

            long chatId = _activeChats[targetNick];

            // 2. Отримуємо учасників чату (щоб взяти їх ключі)
            var participants = await _connection.InvokeAsync<List<string>>("GetChatParticipants", chatId);

            // 3. Отримуємо публічні ключі всіх учасників
            var keys = await _connection.InvokeAsync<Dictionary<string, string>>("GetPublicKeys", participants);

            // 4. Генеруємо одноразовий AES ключ
            byte[] sessionKey = CryptographyService.GenerateSessionKey();

            // 5. Шифруємо саме повідомлення (AES)
            var encryptedBody = CryptographyService.EncryptMessage(messageText, sessionKey);

            // 6. Шифруємо AES-ключ для кожного учасника (RSA)
            var keyBundle = new Dictionary<string, string>();
            foreach (var user in keys)
            {
                // user.Key = нік, user.Value = публічний ключ XML
                keyBundle[user.Key] = CryptographyService.EncryptSessionKey(sessionKey, user.Value);
            }

            // 7. Відправляємо на сервер
            await _connection.InvokeAsync("SendSecureMessage", chatId, MyId, MyNick, encryptedBody.CipherText, encryptedBody.IV, keyBundle);
        }
    }
}
