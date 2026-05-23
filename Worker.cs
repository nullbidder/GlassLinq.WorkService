using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace GlassLinq.WorkService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private HubConnection? _connection;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GlassLinq Agent is starting up...");

        // CRITICAL: Change 7037 to match the exact port your Blazor browser URL is using!
        string orchestratorUrl = "http://glasslinq.orchestrator/jobHub"; // Use your actual VMnet8 IP here!
                                                                      // TEMPORARY FOR DEV SANDBOX: Tells the VM to trust your laptop's local self-signed certificate
        System.Net.ServicePointManager.ServerCertificateValidationCallback +=
            (sender, cert, chain, sslPolicyErrors) => true;

        _connection = new HubConnectionBuilder()
            .WithUrl(orchestratorUrl, options =>
            {
                // Allow the client and server to negotiate the handshake over HTTPS normally
                options.SkipNegotiation = false;

                // This forces the SignalR underlying HTTP client to ignore the localhost SSL mismatch
                options.HttpMessageHandlerFactory = (handler) =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => true;
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        // Register a listener for commands coming down from your Blazor Dashboard
        _connection.On<string>("StartJob", (processName) =>
        {
            _logger.LogInformation("🚨 COMMAND RECEIVED! Orchestrator told me to run: {Process}", processName);
            // This is where your custom Studio activity executor logic will be kicked off later!
        });

        // Loop that attempts to establish and maintain the socket connection
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    _logger.LogInformation("Connecting to Orchestrator at {Url}...", orchestratorUrl);
                    await _connection.StartAsync(stoppingToken);
                    _logger.LogInformation("✅ Connected successfully! Connection ID: {Id}", _connection.ConnectionId);

                    // Tell the Orchestrator our machine name
                    await _connection.InvokeAsync("RegisterMachine", Environment.MachineName, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Connection failed: {Message}. Retrying in 5 seconds...", ex.Message);
                    await Task.Delay(5000, stoppingToken);
                }
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            await _connection.StopAsync(cancellationToken);
            await _connection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}