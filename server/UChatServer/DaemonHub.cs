using Microsoft.AspNetCore.SignalR;

namespace UChatServer;

public class DaemonHub : Hub
{
    // This is the method the Client calls via .InvokeAsync("SendMessage", ...)
    public async Task SendMessage(string user, string message)
    {
        // Log to the Server's internal console (so you see it running as a daemon)
        Console.WriteLine($"[Daemon Log] {user} says: {message}");

        // Broadcast the message to ALL connected clients (Alice, Bob, etc.)
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}