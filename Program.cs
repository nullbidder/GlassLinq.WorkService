using GlassLinq.WorkService;

var builder = Host.CreateApplicationBuilder(args);

// This is the magic method from Microsoft.Extensions.Hosting.WindowsServices
// It tells the app: "If you are started by Windows Services, adapt to background life."
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GlassLinqWorkerAgent";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();