using Microsoft.AspNetCore.SignalR.Client;
using connector;

//input demo
/*
Console.Write("Enter your username: ");
string username = Console.ReadLine() ?? "Unknown";
Console.WriteLine($"--- Connecting as {username} ---");
*/

// Configure connection with our custom "Forever Retry" policy
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/hubs/daemon")
    .WithAutomaticReconnect(new InfiniteRetryPolicy()) 
    .Build();

// Setup listeners
connection.Reconnecting += error =>
{
    Console.WriteLine($"\n[System] Connection lost. Reconnecting... (Error: {error?.Message})");
    return Task.CompletedTask;
};

connection.Reconnected += connectionId =>
{
    Console.WriteLine($"\n[System] Connection restored! (ID: {connectionId})");
    Console.Write("You: ");
    return Task.CompletedTask;
};

connection.Closed += async (error) => 
{
    Console.WriteLine($"\n[System] Connection closed completely. Restarting loop...");
    await ConnectWithRetryAsync(connection);
};

connection.On<string, string>("ReceiveMessage", (user, message) =>
{
    Console.WriteLine($"\r[{user}]: {message}");
    Console.Write("You: "); 
});

// Initial Connect
await ConnectWithRetryAsync(connection);

//demo logIn
/*
try
{ await connection.InvokeAsync("LogIn", "hui", "hui".Trim()); }
catch
{ Console.WriteLine("Failed to send."); }
*/

//demo register
/*
try
{ await connection.InvokeAsync("register", "hui", "hui", "hui"); }
catch
{ Console.WriteLine("Failed to send."); }
*/


//demo
/*while (true)
{
    string? message = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(message))
    {
        if (connection.State != HubConnectionState.Connected)
        {
            Console.WriteLine("Server is down.");
        }
        else
        {
            try { await connection.InvokeAsync("SendMessage", username, message); }
            catch { Console.WriteLine("Failed to send."); }
            Console.Write("You: ");
        }
    }
}*/

// --- PART 2: HELPER METHODS & CLASSES (Must be at the bottom) ---

// Helper method to loop the initial connection until success
async Task ConnectWithRetryAsync(HubConnection connection)
{
    while (true)
    {
        try
        {
            await connection.StartAsync();
            Console.WriteLine("Connected to Server!");
            Console.Write("You: ");
            return;
        }
        catch
        {
            Console.WriteLine("Server unavailable. Retrying in 5s...");
            await Task.Delay(5000);
        }
    }
}

// Custom Policy Class
public class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Return 5 seconds forever. Never return null.
        return TimeSpan.FromSeconds(5);
    }
}