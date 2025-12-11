using Microsoft.AspNetCore.SignalR.Client;
using connector; // Ensure this matches your namespace
using System.IO;

// --- 1. CONFIGURATION ---
string serverIp = "localhost";
int serverPort = 5000;
if (args.Length > 0) serverIp = args[0];
if (args.Length > 1 && int.TryParse(args[1], out int p)) serverPort = p;
string ActiveMail = "example@mail.com";

string hubUrl = $"http://{serverIp}:{serverPort}/hubs/daemon";
Console.WriteLine($"Target Server: {hubUrl}");

// --- 2. BUILD CONNECTION ---
var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect(new InfiniteRetryPolicy())
    .Build();

// --- 3. REGISTER LISTENERS ---

// A. Standard Plaintext/System Messages
connection.On<string, string>("ReceiveSystem", (user, message) =>
{
    Console.WriteLine($"\r[{user}]: {message}");
});

// B. Secure Messages (THE TEST TARGET)
connection.On<string, string, string, string>("ReceiveSecureMessage", (sender, cipherText, iv, myEncryptedKey) =>
{
    try 
    {
        if (!File.Exists($"{ActiveMail}.key")) 
        {
            Console.WriteLine($"\r[{sender}]: [LOCKED - No Private Key Found]");
            Console.Write("You: ");
            return;
        }

        string myPrivKey = File.ReadAllText($"{ActiveMail}.key");

        // 1. Unlock the Session Key (RSA)
        byte[] sessionKey = CryptographyService.DecryptSessionKey(myEncryptedKey, myPrivKey);

        // 2. Unlock the Message (AES)
        string plainText = CryptographyService.DecryptMessage(cipherText, iv, sessionKey);

        Console.WriteLine($"\r[{sender} 🔒]: {plainText}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\r[{sender}]: [Decryption Failed: {ex.Message}]");
    }
    Console.Write("You: ");
});

// C. Private Key Receipt
connection.On<string>("ReceivePrivateKey", (key) =>
{
    Console.WriteLine($"\n[System] REGISTRATION SUCCESS!");
    File.WriteAllText($"{ActiveMail}.key", key);
    Console.WriteLine($"[System] Private Key saved to '{ActiveMail}.key'");
    Console.Write("You: ");
});

// D. Connection Lifecycle
connection.Reconnecting += error => { Console.WriteLine($"\nReconnecting..."); return Task.CompletedTask; };
connection.Reconnected += id => { Console.WriteLine($"\nRestored!"); Console.Write("You: "); return Task.CompletedTask; };

await ConnectWithRetryAsync(connection);

// --- 4. MAIN COMMAND LOOP ---
Console.WriteLine("\n--- COMMANDS ---");
Console.WriteLine("/register [email] [password] [nickname]");
Console.WriteLine("/login [email] [password]");
Console.WriteLine("/msg [nickname] [message]");
Console.WriteLine("----------------\n");

string currentUser = "Guest";

while (true)
{
    Console.Write("You: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    
    // --- REGISTER ---
    if (input.StartsWith("/register"))
    {
        var parts = input.Split(' ');
        if (parts.Length < 4) { Console.WriteLine("Usage: /register email pass nick"); continue; }
        await connection.InvokeAsync("Register", parts[3], parts[1], parts[2]); // nick, mail, pass
        currentUser = parts[3];
        ActiveMail = parts[1];
    }
    // --- LOGIN ---
    else if (input.StartsWith("/login"))
    {
        var parts = input.Split(' ');
        if (parts.Length < 3) { Console.WriteLine("Usage: /login email pass"); continue; }
        await connection.InvokeAsync("LogIn", parts[1], parts[2]);
        ActiveMail = parts[1];
        
        // Optional: Fetch History here
    }
    // --- SECURE MESSAGE ---
    else if (input.StartsWith("/msg"))
    {
        // Usage: /msg Bob Hello World
        var parts = input.Split(' ', 3);
        if (parts.Length < 3) { Console.WriteLine("Usage: /msg Bob Hello World"); continue; }

        string target = parts[1];
        string messageText = parts[2];
        var recipients = new List<string> { target, currentUser }; // Add self for history

        Console.WriteLine("[System] Encrypting...");

        try 
        {
            // 1. Get Public Keys
            var keys = await connection.InvokeAsync<Dictionary<string, string>>("GetPublicKeys", recipients);

            // 2. Encrypt
            byte[] sessionKey = CryptographyService.GenerateSessionKey();
            var encryptedBody = CryptographyService.EncryptMessage(messageText, sessionKey);

            var keyBundle = new Dictionary<string, string>();
            foreach (var user in keys)
            {
                keyBundle[user.Key] = CryptographyService.EncryptSessionKey(sessionKey, user.Value);
            }

            // 3. Send
            await connection.InvokeAsync("SendSecureMessage", currentUser, encryptedBody.CipherText, encryptedBody.IV, keyBundle);
            Console.WriteLine($"[System] Sent secure message.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ex.Message}");
        }
    }
    // --- PLAIN MESSAGE ---
    else
    {
        if (connection.State == HubConnectionState.Connected)
            await connection.InvokeAsync("SendMessage", currentUser, input);
        else
            Console.WriteLine("[System] Disconnected.");
    }
}

// --- HELPERS ---
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
            Console.WriteLine($"Server unavailable. Retrying in 5s...");
            await Task.Delay(5000);
        }
    }
}

public class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext) => TimeSpan.FromSeconds(5);
}

// Simple DTO for history if you use it later
public class HistoryItem 
{
    public string Sender { get; set; }
    public string CipherText { get; set; }
    public string IV { get; set; }
    public string MyEncryptedKey { get; set; }
    public DateTime Timestamp { get; set; }
}