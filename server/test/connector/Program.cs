using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using connector; // Ensure this namespace matches your project

class Program
{
    // --- CONFIGURATION ---
    static string serverIp = "localhost";
    static int serverPort = 5000;
    static string hubUrl = $"http://{serverIp}:{serverPort}/hubs/daemon";

    // --- STATE ---
    static long myId = -1;
    static string myNick = "Guest";
    static string ActiveMail = "guest@mail.com";
    
    // Maps: "Alice" -> 10, "MyGroup" -> 25
    static Dictionary<string, long> activeChats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    static async Task Main(string[] args)
    {
        if (args.Length > 0) serverIp = args[0];
        if (args.Length > 1 && int.TryParse(args[1], out int p)) serverPort = p;

        // 1. BUILD CONNECTION
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new InfiniteRetryPolicy())
            .Build();

        // 2. REGISTER LISTENERS

        // Login Success
        connection.On<long, string>("LoginSuccess", (id, nick) =>
        {
            myId = id;
            myNick = nick;
            Console.WriteLine($"\n[System] Logged in as {myNick} (ID: {myId})");
            PrintPrompt();
        });

        // Group Created / Initialized
        connection.On<long, string, List<string>>("ReceiveGroupInit", (chatId, groupName, participants) =>
        {
            activeChats[groupName] = chatId;
            Console.WriteLine($"\n[System] Group '{groupName}' (ID: {chatId}) joined.");
            Console.WriteLine($"[System] Members: {string.Join(", ", participants)}");
            PrintPrompt();
        });

        // Private Chat Created
        connection.On<long, string>("ChatCreated", (chatId, targetNick) =>
        {
            // If I created it, targetNick is the other person.
            // If I received it, targetNick might be "Someone" or the initiator. 
            // Better logic: Store it if it's not generic.
            if(targetNick != "Someone") activeChats[targetNick] = chatId;
            
            Console.WriteLine($"\n[System] Private Chat #{chatId} ready.");
            PrintPrompt();
        });

        // INCOMING MESSAGE (Fix: Added messageId parameter)
        connection.On<string, string, string, string, long, long>("ReceiveSecureMessage", 
            (sender, cipherText, iv, myEncryptedKey, chatId, messageId) =>
        {
            Console.WriteLine(); // Clear current line
            try 
            {
                string decrypted = DecryptIncoming(cipherText, iv, myEncryptedKey);
                Console.WriteLine($"[Chat {chatId}] {sender}: {decrypted}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chat {chatId}] {sender}: [Decryption Failed: {ex.Message}]");
            }
            PrintPrompt();
        });

        // System Messages
        connection.On<string, string>("ReceiveSystem", (user, message) =>
        {
            Console.WriteLine($"\n[{user}]: {message}");
            PrintPrompt();
        });

        // Search Results
        connection.On<List<string>>("ReceiveSearchResults", (users) =>
        {
            Console.WriteLine($"\n--- Users Found ---");
            foreach (var u in users) Console.WriteLine($" - {u}");
            Console.WriteLine("-------------------");
            PrintPrompt();
        });

        // Private Key Retrieval (Upon Registration)
        connection.On<string>("ReceivePrivateKey", (key) =>
        {
            File.WriteAllText($"{ActiveMail}.key", key);
            Console.WriteLine($"[System] Private Key saved to {ActiveMail}.key");
            PrintPrompt();
        });

        // 3. CONNECT
        await ConnectWithRetryAsync(connection);

        // 4. MAIN UI LOOP
        PrintHelp();

        while (true)
        {
            PrintPrompt();
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            string[] parts = input.Split(' ');
            string cmd = parts[0].ToLower();

            try 
            {
                switch (cmd)
                {
                    case "/register":
                        if (parts.Length < 4) Console.WriteLine("Usage: /register [email] [pass] [nick]");
                        else 
                        {
                            ActiveMail = parts[1];
                            await connection.InvokeAsync("register", parts[1], parts[2], parts[3]);
                        }
                        break;

                    case "/login":
                        if (parts.Length < 3) Console.WriteLine("Usage: /login [email] [pass]");
                        else 
                        {
                            ActiveMail = parts[1];
                            await connection.InvokeAsync("LogIn", parts[1], parts[2]);
                        }
                        break;

                    case "/search":
                        if (parts.Length < 2) Console.WriteLine("Usage: /search [query]");
                        else await connection.InvokeAsync("SearchUsers", parts[1]);
                        break;

                    case "/chat":
                        if (myId == -1) { Console.WriteLine("Login first."); break; }
                        if (parts.Length < 2) Console.WriteLine("Usage: /chat [target_nick]");
                        else await connection.InvokeAsync("InitPrivateChat", parts[1], (int)myId); 
                        break;

                    case "/create":
                        // Usage: /create MyGroup Bob Alice
                        if (myId == -1) { Console.WriteLine("Login first."); break; }
                        if (parts.Length < 3) { Console.WriteLine("Usage: /create [GroupName] [User1] [User2]..."); break; }
                        
                        string groupName = parts[1];
                        var groupParticipants = parts.Skip(2).ToList();
                        
                        // Ensure I am in the list so I get the key request logic handled correctly server-side
                        if(!groupParticipants.Contains(myNick)) groupParticipants.Add(myNick);

                        await connection.InvokeAsync("CreateGroup", groupName, myNick, groupParticipants);
                        break;

                    case "/msg":
                        if (myId == -1) { Console.WriteLine("Login first."); break; }
                        if (parts.Length < 3) { Console.WriteLine("Usage: /msg [Target/Group] [Message]"); break; }

                        string target = parts[1];
                        string messageText = string.Join(" ", parts.Skip(2));

                        if (!activeChats.ContainsKey(target))
                        {
                            Console.WriteLine($"[Error] Unknown chat '{target}'. Use /chat or /create first.");
                            break;
                        }

                        long chatId = activeChats[target];

                        // A. Get Participants
                        var participants = await connection.InvokeAsync<List<string>>("GetChatParticipants", chatId);

                        // B. Get Public Keys for ALL participants
                        var keys = await connection.InvokeAsync<Dictionary<string, string>>("GetPublicKeys", participants);

                        // C. Generate ONE symmetric Session Key
                        byte[] sessionKey = CryptographyService.GenerateSessionKey();

                        // D. Encrypt the Message body ONCE
                        var encryptedBody = CryptographyService.EncryptMessage(messageText, sessionKey);

                        // E. Encrypt the Session Key SEPARATELY for each recipient
                        var keyBundle = new Dictionary<string, string>();
                        foreach (var user in keys)
                        {
                            // user.Key = Nickname, user.Value = PublicKey
                            keyBundle[user.Key] = CryptographyService.EncryptSessionKey(sessionKey, user.Value);
                        }
                        
                        // F. Send Bundle
                        await connection.InvokeAsync("SendSecureMessage", chatId, myId, myNick, encryptedBody.CipherText, encryptedBody.IV, keyBundle);
                        Console.WriteLine($"[System] Message Sent.");
                        break;

                    case "/history":
                        if (myId == -1) { Console.WriteLine("Login first."); break; }
                        if (parts.Length < 2) { Console.WriteLine("Usage: /history [Target/Group]"); break; }
                        
                        string histTarget = parts[1];
                        if (!activeChats.ContainsKey(histTarget)) 
                        { 
                            Console.WriteLine($"Unknown target '{histTarget}'.");
                            break; 
                        }

                        long histChatId = activeChats[histTarget];
                        var history = await connection.InvokeAsync<List<HistoryItem>>("GetChatHistory", histChatId, myId);

                        Console.WriteLine($"\n--- History: {histTarget} ---");
                        foreach(var item in history)
                        {
                            string msg = "[LOCKED]";
                            try { msg = DecryptIncoming(item.CipherText, item.IV, item.MyEncryptedKey); } catch {}
                            Console.WriteLine($"[{item.Timestamp.ToShortTimeString()}] {item.Sender}: {msg}");
                        }
                        Console.WriteLine("-----------------------------");
                        break;

                    default:
                        Console.WriteLine("Unknown command.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
            }
        }
    }

    // --- HELPER METHODS ---

    static string DecryptIncoming(string cipherText, string iv, string myEncryptedKey)
    {
        if (!File.Exists($"{ActiveMail}.key")) return "[NO PRIVATE KEY]";

        string myPrivKey = File.ReadAllText($"{ActiveMail}.key");
        
        // 1. Decrypt AES Session Key using RSA Private Key
        byte[] sessionKey = CryptographyService.DecryptSessionKey(myEncryptedKey, myPrivKey);
        
        // 2. Decrypt Message using AES Session Key
        return CryptographyService.DecryptMessage(cipherText, iv, sessionKey);
    }

    static void PrintPrompt()
    {
        if(myId != -1) Console.Write($"{myNick}> ");
        else Console.Write("Guest> ");
    }

    static void PrintHelp()
    {
        Console.WriteLine("\n--- COMMANDS ---");
        Console.WriteLine("/register [email] [pass] [nick]");
        Console.WriteLine("/login [email] [pass]");
        Console.WriteLine("/search [query]");
        Console.WriteLine("/chat [target_nick]           -> Start Private Chat");
        Console.WriteLine("/create [Group] [User1] ...   -> Start Group Chat");
        Console.WriteLine("/msg [target/Group] [text]    -> Send Encrypted Message");
        Console.WriteLine("/history [target/Group]       -> View History");
        Console.WriteLine("----------------\n");
    }

    static async Task ConnectWithRetryAsync(HubConnection connection)
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
}

// Helper Class for History
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
