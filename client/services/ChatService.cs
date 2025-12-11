using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;


namespace client.Services // Зміни на свій namespace
{
    public class ChatService
    {
        private HubConnection _connection;
        private string _activeMail;
        private string _activeKeyPath => $"{_activeMail}.key";
        
        // Події, щоб повідомляти UI про зміни (замість Console.WriteLine)
        public event Action<string> OnLog; // Логи системи
        public event Action<string, string> OnMessageReceived; // (Sender, Message)
        public event Action<int, string> OnLoggedIn;
        public event Action<List<string>> OnSearchResults;

        public int MyId { get; private set; } = -1;
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
            // A. Login Success
            _connection.On<int, string>("LoginSuccess", (id, nick) =>
            {
                MyId = id;
                MyNick = nick;
                OnLoggedIn?.Invoke(id, nick);
                OnLog?.Invoke($"Logged in as {nick}");
            });

            // B. Secure Message (AUTO-DECRYPT)
            _connection.On<string, string, string, string>("ReceiveSecureMessage", (sender, cipherText, iv, myEncryptedKey) =>
            {
                try 
                {
                    if (!File.Exists(_activeKeyPath)) 
                    {
                        OnMessageReceived?.Invoke(sender, "[LOCKED - No Key]");
                        return;
                    }

                    string myPrivKey = File.ReadAllText(_activeKeyPath);
                    byte[] sessionKey = CryptographyService.DecryptSessionKey(myEncryptedKey, myPrivKey);
                    string plainText = CryptographyService.DecryptMessage(cipherText, iv, sessionKey);

                    // ВІДПРАВЛЯЄМО В UI РОЗШИФРОВАНИЙ ТЕКСТ
                    OnMessageReceived?.Invoke(sender, plainText);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Decryption error: {ex.Message}");
                }
            });

            // Інші хендлери (Search, ChatCreated, ReceivePrivateKey) додаються аналогічно...
             _connection.On<string>("ReceivePrivateKey", (key) => {
                File.WriteAllText(_activeKeyPath, key);
                OnLog?.Invoke("Registration Successful. Key saved.");
            });
        }

        // --- МЕТОДИ, ЯКІ БУДУТЬ ВИКЛИКАТИСЯ КНОПКАМИ ---

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

        public async Task SendMessageAsync(string targetNick, string message, int chatId)
        {
            // Тут твоя логіка шифрування з консолі (/msg)
            // 1. Generate Session Key
            byte[] sessionKey = CryptographyService.GenerateSessionKey();
            // 2. Encrypt Body
            var encryptedBody = CryptographyService.EncryptMessage(message, sessionKey);
            
            // 3. Get Key Bundle
            var keyBundle = new Dictionary<string, string>();
            var recipients = new List<string> { targetNick, MyNick };

            foreach (var user in recipients)
            {
                string pk = await _connection.InvokeAsync<string>("GetPublicKey", user);
                if (pk != "NOT_FOUND")
                {
                    string encryptedSessionKey = CryptographyService.EncryptSessionKey(sessionKey, pk);
                    keyBundle[user] = encryptedSessionKey;
                }
            }

            await _connection.InvokeAsync("SendSecureMessage", chatId, MyId, MyNick, encryptedBody.CipherText, encryptedBody.IV, keyBundle);
        }
    }
}
