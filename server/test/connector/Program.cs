using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using connector; // Ensure this matches your namespace for CryptographyService

// --- 1. CONFIGURATION ---
string serverIp = "localhost";
int serverPort = 5000;
string hubUrl = $"http://{serverIp}:{serverPort}/hubs/daemon";

// --- 2. CLIENT STATE ---
int myId = -1;
string myNick = "Guest";
string ActiveMail = "guest@mail.com";
Dictionary<string, int> activeChats = new Dictionary<string, int>();

// --- 3. CONNECTION SETUP ---
var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect(new InfiniteRetryPolicy())
    .Build();

// --- 4. REGISTER LISTENERS ---

// A. Login Success
connection.On<int, string>("LoginSuccess", (id, nick) =>
{
    myId = id;
    myNick = nick;
    Console.WriteLine($"\n[System] Logged in as {myNick} (ID: {myId})");
    Console.Write($"{myNick}> ");
});

// B. Search Results
connection.On<List<string>>("ReceiveSearchResults", (users) =>
{
    Console.WriteLine($"\n--- Users Found ---");
    foreach (var u in users) Console.WriteLine($" - {u}");
    Console.WriteLine("-------------------");
    Console.Write($"{myNick}> ");
});

// C. Chat Created
connection.On<int, string>("ChatCreated", (chatId, targetNick) =>
{
    activeChats[targetNick] = chatId;
    Console.WriteLine($"\n[System] Secure Chat #{chatId} established with {targetNick}.");
    Console.Write($"{myNick}> ");
});

// D. Receive Secure Message (AUTO-DECRYPT)
connection.On<string, string, string, string>("ReceiveSecureMessage", (sender, cipherText, iv, myEncryptedKey) =>
{
    // Don't overwrite the prompt while typing, just print a new line
    Console.WriteLine(); 

    try 
    {
        if (!File.Exists($"{ActiveMail}.key")) 
        {
            Console.WriteLine($"[{sender}]: [LOCKED - No Private Key Found]");
        }
        else
        {
            string myPrivKey = File.ReadAllText($"{ActiveMail}.key");

            // 1. Decrypt the shared Session Key using our Private Key (RSA)
            byte[] sessionKey = CryptographyService.DecryptSessionKey(myEncryptedKey, myPrivKey);

            // 2. Decrypt the actual Message using the Session Key (AES)
            string plainText = CryptographyService.DecryptMessage(cipherText, iv, sessionKey);

            // 3. Display Cleartext
            Console.WriteLine($"[{sender} 🔒]: {plainText}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{sender}]: [Decryption Failed: {ex.Message}]");
    }
    
    // Restore prompt
    if (myId != -1) Console.Write($"{myNick}> ");
});

// E. System Messages
connection.On<string, string>("ReceiveSystem", (user, message) =>
{
    Console.WriteLine($"\r[{user}]: {message}");
    if (myId != -1) Console.Write($"{myNick}> ");
});

// F. Save Private Key (On Registration)
connection.On<string>("ReceivePrivateKey", (key) =>
{
    File.WriteAllText($"{ActiveMail}.key", key);
    Console.WriteLine($"[System] Registration Successful. Key saved to '{ActiveMail}.key'");
    Console.Write($"{myNick}> ");
});

await ConnectWithRetryAsync(connection);

// --- 5. MAIN LOOP ---
Console.WriteLine("\n--- COMMANDS ---");
Console.WriteLine("/register [email] [pass] [nick]");
Console.WriteLine("/login [email] [pass]");
Console.WriteLine("/search [partial_nick]");
Console.WriteLine("/chat [target_nick]        -> Start a room");
Console.WriteLine("/msg [target_nick] [text]  -> Send encrypted message");
Console.WriteLine("/history [target_nick]     -> Read past messages");
Console.WriteLine("----------------\n");

while (true)
{
    Console.Write($"{myNick}> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;

    string[] parts = input.Split(' ');
    string cmd = parts[0].ToLower();

    try 
    {
        if (cmd == "/login")
        {
            if (parts.Length < 3) Console.WriteLine("Usage: /login email pass");
            else await connection.InvokeAsync("LogIn", parts[1], parts[2]);
            ActiveMail = parts[1];
        }
        else if (cmd == "/register")
        {
            if (parts.Length < 4) Console.WriteLine("Usage: /register email pass nick");
            else await connection.InvokeAsync("register", parts[1], parts[2], parts[3]);
            ActiveMail = parts[1];
        }
        else if (cmd == "/search")
        {
            if (parts.Length < 2) Console.WriteLine("Usage: /search partialName");
            else await connection.InvokeAsync("SearchUsers", parts[1]);
        }
        else if (cmd == "/chat")
        {
            if (myId == -1) { Console.WriteLine("Login first."); continue; }
            if (parts.Length < 2) Console.WriteLine("Usage: /chat targetNick");
            else await connection.InvokeAsync("InitPrivateChat", parts[1], myId);
        }
        else if (cmd == "/msg")
        { 
            if (myId == -1) { Console.WriteLine("Login first."); continue; }
            if (parts.Length < 3) { Console.WriteLine("Usage: /msg targetNick Hello World"); continue; }

            string targetNick = parts[1];
            string messageText = string.Join(" ", parts.Skip(2));

            if (!activeChats.ContainsKey(targetNick))
            {
                Console.WriteLine($"[Error] You haven't started a chat with {targetNick} yet.");
                Console.WriteLine($"Type '/chat {targetNick}' to initialize the session.");
                continue;
            }

            int chatId = activeChats[targetNick];
            
            var recipients = new List<string> { targetNick, myNick };

            Console.WriteLine("[System] Encrypting...");

            // 2. Generate a One-Time Session Key (AES)
            byte[] sessionKey = CryptographyService.GenerateSessionKey();

            // 3. Encrypt the Message Body with this Session Key
            var encryptedBody = CryptographyService.EncryptMessage(messageText, sessionKey);

            // 4. Encrypt the Session Key for EACH recipient using their Public Key
            var keyBundle = new Dictionary<string, string>();
            
            foreach (var user in recipients)
            {
                string pk = await connection.InvokeAsync<string>("GetPublicKey", user);
                
                if (pk != "NOT_FOUND")
                {
                    // Encrypt AES key with RSA Public Key
                    string encryptedSessionKey = CryptographyService.EncryptSessionKey(sessionKey, pk);
                    keyBundle[user] = encryptedSessionKey;
                }
            }
            
            await connection.InvokeAsync("SendSecureMessage", chatId, myId, myNick, encryptedBody.CipherText, encryptedBody.IV, keyBundle);
            
            Console.WriteLine($"[System] Message sent to chat #{chatId}.");
            // --- FIX END ---
        }
        else if (cmd == "/history")
        {
             if (myId == -1) { Console.WriteLine("Login first."); continue; }
             if (parts.Length < 2) { Console.WriteLine("Usage: /history targetNick"); continue; }
             
             string targetNick = parts[1];
             if (!activeChats.ContainsKey(targetNick)) { Console.WriteLine("Chat not initialized."); continue; }

             int chatId = activeChats[targetNick];
             var history = await connection.InvokeAsync<List<HistoryItem>>("GetChatHistory", chatId, myId);

             Console.WriteLine($"\n--- History with {targetNick} ---");
             foreach(var item in history)
             {
                 try {
                     if(File.Exists($"{ActiveMail}.key")) {
                        string myPriv = File.ReadAllText($"{ActiveMail}.key");
                        // Decrypt Key -> Decrypt Message
                        var sessKey = CryptographyService.DecryptSessionKey(item.MyEncryptedKey, myPriv);
                        var txt = CryptographyService.DecryptMessage(item.CipherText, item.IV, sessKey);
                        
                        Console.WriteLine($"[{item.Timestamp.ToShortTimeString()}] {item.Sender}: {txt}");
                     } else {
                        Console.WriteLine($"[{item.Timestamp.ToShortTimeString()}] {item.Sender}: [LOCKED]");
                     }
                 } catch {
                     Console.WriteLine($"[{item.Timestamp.ToShortTimeString()}] {item.Sender}: [Decryption Error]");
                 }
             }
             Console.WriteLine("-------------------------------");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] {ex.Message}");
    }
}

async Task ConnectWithRetryAsync(HubConnection connection)
{
    while (true)
    {
        try
        {
            await connection.StartAsync();
            Console.WriteLine("Connected to Server!");
            return;
        }
        catch
        {
            Console.WriteLine($"Server unavailable. Retrying in 3s...");
            await Task.Delay(3000);
        }
    }
}

public class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext) => TimeSpan.FromSeconds(5);
}

public class HistoryItem
{
    public long MessageId { get; set; }
    public string Sender { get; set; }
    public string CipherText { get; set; }
    public string IV { get; set; }
    public string MyEncryptedKey { get; set; }
    public DateTime Timestamp { get; set; }
}
