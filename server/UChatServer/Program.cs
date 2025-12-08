using UChatServer;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// 1. Systemd Integration
builder.Services.AddSystemd();

// 2. Add SignalR
builder.Services.AddSignalR();

// 3. Add the Background Worker
builder.Services.AddHostedService<DaemonWorker>();

// 4. Configure Port 5000
builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(5000));

var app = builder.Build();

// 5. Route the Hub
app.MapHub<DaemonHub>("/hubs/daemon");

app.Run();