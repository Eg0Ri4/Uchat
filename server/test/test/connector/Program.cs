using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using connector; 

// --- 1. CONFIGURATION ---
string serverIp = "localhost";
int serverPort = 5000;
if (args.Length > 0) serverIp = args[0];
if (args.Length > 1 && int.TryParse(args[1], out int p)) serverPort = p;
string hubUrl = $"http://{serverIp}:{serverPort}/hubs/daemon";

// --- 2. CLIENT STATE ---
long myId = -1; 
string myNick = "Guest";
string ActiveMail = "guest@mail.com";
Dictionary<string, long> activeChats = new Dictionary<string, long>(); // Map Nick -> ChatID

// --- 3. CONNECTION ---
var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect(new InfiniteRetryPolicy())
    .Build();

// --- 4. LISTENERS ---

// A. Login Success
connection.On<long, string>("LoginSuccess", (id, nick) =>
{
    myId = id;
    myNick = nick;
    Console.WriteLine($"\n[System] Logged in as {myNick} (ID: {myId})");
    Console.Write($"{myNick}> ");
});

// B. Group Init
connection.On<long, string, List<string>>("ReceiveGroupInit", (chatId, groupName, participants) =>
{
    activeChats[groupName] = chatId;
    Console.WriteLine($"\n[System] Group '{groupName}' (ID: {chatId}) created.");
    Console.WriteLine($"[System] Members: {string.Join(", ", participants)}");
    Console.Write($"{myNick}> ");
});

// C. Chat Created (Private)
connection.On<long, string>("ChatCreated", (chatId, targetNick) =>
{
    activeChats[targetNick] = chatId;
    Console.WriteLine($"\n[System] Chat #{chatId} established with {targetNick}.");
    Console.Write($"{myNick}> ");
});

// D. Receive Secure Message
connection.On<string, string, string, string, long>("ReceiveSecureMessage", (sender, cipherText, iv, myEncryptedKey, chatId) =>
{
    Console.WriteLine(); 
    try 
    {
        if (!File.Exists($"{ActiveMail}.key")) 
        {
            Console.WriteLine($"[Chat {chatId}] {sender}: [LOCKED]");
        }
        else
        {
            string myPrivKey = File.ReadAllText($"{ActiveMail}.key");
            byte[] sessionKey = CryptographyService.DecryptSessionKey(myEncryptedKey, myPrivKey);
            string plainText = CryptographyService.DecryptMessage(cipherText, iv, sessionKey);
            Console.WriteLine($"[Chat {chatId}] {sender} 🔒: {plainText}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Chat {chatId}] {sender}: [Error: {ex.Message}]");
    }
    if(myId != -1) Console.Write($"{myNick}> ");
});

// E. Search/System/Key
connection.On<List<string>>("ReceiveSearchResults", (users) =>
{
    Console.WriteLine($"\n--- Users Found ---");
    foreach (var u in users) Console.WriteLine($" - {u}");
    Console.WriteLine("-------------------");
    Console.Write($"{myNick}> ");
});

connection.On<string, string>("ReceiveSystem", (user, message) =>
{
    Console.WriteLine($"\r[{user}]: {message}");
    if (myId != -1) Console.Write($"{myNick}> ");
});

connection.On<string>("ReceivePrivateKey", (key) =>
{
    File.WriteAllText($"{ActiveMail}.key", key);
    Console.WriteLine($"[System] Key saved.");
    Console.Write($"{myNick}> ");
});

await ConnectWithRetryAsync(connection);

// --- 5. MAIN LOOP ---
Console.WriteLine("\n--- COMMANDS ---");
Console.WriteLine("/register [email] [pass] [nick]");
Console.WriteLine("/login [email] [pass]");
Console.WriteLine("/search [query]");
Console.WriteLine("/chat [target]                -> Init Private Chat");
Console.WriteLine("/create [Group] [User1] ...   -> Create Group");
Console.WriteLine("/msg [target/Group] [text]    -> Send Message (Private or Group Name)");
Console.WriteLine("/history [target]             -> Read past messages");
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
            else { await connection.InvokeAsync("LogIn", parts[1], parts[2]); ActiveMail = parts[1]; }
        }
        else if (cmd == "/register")
        {
            if (parts.Length < 4) Console.WriteLine("Usage: /register email pass nick");
            else { await connection.InvokeAsync("register", parts[1], parts[2], parts[3]); ActiveMail = parts[1]; }
        }
        else if (cmd == "/search")
        {
            if (parts.Length < 2) Console.WriteLine("Usage: /search query");
            else await connection.InvokeAsync("SearchUsers", parts[1]);
        }
        else if (cmd == "/chat")
        {
            if (myId == -1) { Console.WriteLine("Login first."); continue; }
            if (parts.Length < 2) Console.WriteLine("Usage: /chat target");
            else await connection.InvokeAsync("InitPrivateChat", parts[1], (int)myId); 
        }
        else if (cmd == "/create")
        {
            // Usage: /create MyGroup Bob Alice
            if (myId == -1) { Console.WriteLine("Login first."); continue; }
            if (parts.Length < 3) { Console.WriteLine("Usage: /create Name User1 User2..."); continue; }
            
            string groupName = parts[1];
            var participants = parts.Skip(2).ToList();
            if(!participants.Contains(myNick)) participants.Add(myNick);

            // FIX: Pass myNick as the creator so server marks you as Owner
            await connection.InvokeAsync("CreateGroup", groupName, myNick, participants);
        }
        else if (cmd == "/msg")
        { 
            if (myId == -1) { Console.WriteLine("Login first."); continue; }
            if (parts.Length < 3) { Console.WriteLine("Usage: /msg target text"); continue; }

            string target = parts[1];
            string messageText = string.Join(" ", parts.Skip(2));

            if (!activeChats.ContainsKey(target))
            {
                Console.WriteLine($"[Error] No active chat ID found for '{target}'.");
                Console.WriteLine("Use '/chat target' for private or '/create' for groups.");
                continue;
            }

            long chatId = activeChats[target];
            List<string> participants;

            // Simplified: Ask server for participants of this ChatID
            participants = await connection.InvokeAsync<List<string>>("GetChatParticipants", chatId);

            // Encryption Sequence
            var keys = await connection.InvokeAsync<Dictionary<string, string>>("GetPublicKeys", participants);
            byte[] sessionKey = CryptographyService.GenerateSessionKey();
            var encryptedBody = CryptographyService.EncryptMessage(messageText, sessionKey);

            var keyBundle = new Dictionary<string, string>();
            foreach (var user in keys)
            {
                keyBundle[user.Key] = CryptographyService.EncryptSessionKey(sessionKey, user.Value);
            }
            
            // Send
            await connection.InvokeAsync("SendSecureMessage", chatId, myId, myNick, encryptedBody.CipherText, encryptedBody.IV, keyBundle);
            Console.WriteLine($"[System] Sent.");
        }
        else if (cmd == "/history")
        {
            if (myId == -1) { Console.WriteLine("Login first."); continue; }
            if (parts.Length < 2) { Console.WriteLine("Usage: /history target_nick_or_group"); continue; }
            
            string target = parts[1];
            
            if (!activeChats.ContainsKey(target)) 
            { 
                Console.WriteLine($"Chat with '{target}' is not active locally."); 
                Console.WriteLine("Try /chat or /create first to sync the ID.");
                continue; 
            }

            long chatId = activeChats[target];
            
            try 
            {
                // Call server to get history items
                var history = await connection.InvokeAsync<List<HistoryItem>>("GetChatHistory", chatId, myId);

                Console.WriteLine($"\n--- History for {target} (Chat {chatId}) ---");
                
                string myPrivKey = "";
                if (File.Exists($"{ActiveMail}.key")) myPrivKey = File.ReadAllText($"{ActiveMail}.key");

                foreach(var item in history)
                {
                    string displayMsg = "[LOCKED]";
                    if (!string.IsNullOrEmpty(myPrivKey))
                    {
                        try 
                        {
                            var sessKey = CryptographyService.DecryptSessionKey(item.MyEncryptedKey, myPrivKey);
                            displayMsg = CryptographyService.DecryptMessage(item.CipherText, item.IV, sessKey);
                        }
                        catch 
                        {
                            displayMsg = "[Decryption Error]";
                        }
                    }
                    Console.WriteLine($"[{item.Timestamp.ToShortTimeString()}] {item.Sender}: {displayMsg}");
                }
                Console.WriteLine("--------------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error fetching history] {ex.Message}");
            }
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

public class HistoryItem
{
    public long MessageId { get; set; }
    public string Sender { get; set; }
    public string CipherText { get; set; }
    public string IV { get; set; }
    public string MyEncryptedKey { get; set; }
    public DateTime Timestamp { get; set; }
}

public class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext) => TimeSpan.FromSeconds(5);
}
