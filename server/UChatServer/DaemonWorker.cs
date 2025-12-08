using Microsoft.AspNetCore.SignalR;

namespace UChatServer;

public class DaemonWorker : BackgroundService
{
    private readonly IHubContext<DaemonHub> _hubContext;
    private readonly ILogger<DaemonWorker> _logger;

    public DaemonWorker(IHubContext<DaemonHub> hubContext, ILogger<DaemonWorker> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Just a quiet log to show the Daemon is alive
            _logger.LogInformation("Daemon Service is healthy at: {time}", DateTimeOffset.Now);

            // Optional: Send a system message to chat every 60 seconds
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "SYSTEM", "Server is active...", stoppingToken);

            // Wait 60 seconds so we don't interrupt the chat
            await Task.Delay(60000, stoppingToken);
        }
    }
}