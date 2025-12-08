using Microsoft.AspNetCore.SignalR;

namespace connector;

public class DaemonHub : Hub
{
    // The Client calls this method
    public async Task SendMessage(string user, string message)
    {
        // The Server broadcasts it back to everyone (including the sender)
        // "ReceiveMessage" matches the string in the Client's .On() method
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}