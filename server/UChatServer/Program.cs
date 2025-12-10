using UChatServer;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Get & Print PID ---
int pid = Environment.ProcessId;
Console.WriteLine($"[System] Process ID: {pid}");

// --- 2. Handle Port Argument 
int port = 5000; 
if (args.Length > 0 && int.TryParse(args[0], out int customPort))
{
    port = customPort;
}
Console.WriteLine($"[System] Listening on Port: {port}");

// --- 3. Setup Services ---
builder.Services.AddSystemd();
builder.Services.AddSignalR();
builder.Services.AddHostedService<DaemonWorker>();

builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(port));

var app = builder.Build();

app.MapHub<DaemonHub>("/hubs/daemon");

app.Run();

