using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Worker service for diagnostics.
/// </summary>
public class DiagnosticsWorker(
    ILog<DiagnosticsWorker> log, 
    IJackServerDiscoveryService discoveryService,
    IJackConnectionManagerService connectionManager, 
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private readonly ILog<DiagnosticsWorker> _log = log;
    private readonly IJackServerDiscoveryService _discoveryService = discoveryService;
    private readonly IJackConnectionManagerService _connectionManager = connectionManager;
    private readonly IHostApplicationLifetime _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _log.Info("=== Jack Diagnostics Starting ===");
            
            // Discover available servers
            _log.Info("Phase 1: Server Discovery");
            var servers = await _discoveryService.DiscoverServersAsync();
            
            if (servers.Count > 0)
            {
                _log.Info($"Found {servers.Count} server(s):");
                foreach (var server in servers)
                {
                    _log.Info($"  - {server.Name}: Running={server.IsRunning}");
                    if (server.IsRunning && server.SampleRate.HasValue)
                    {
                        _log.Info($"    Sample Rate: {server.SampleRate} Hz, Buffer: {server.BufferSize} samples");
                    }
                }
            }
            else
            {
                _log.Warn("No Jack servers found during discovery");
            }

            // Attempt to connect
            _log.Info("Phase 2: Connection Attempt");
            var connectionStatus = await _connectionManager.ConnectAsync();
            
            _log.Info("=== Jack Connection Status ===");
            _log.Info($"Connected: {connectionStatus.IsConnected}");
            _log.Info($"Server Name: {connectionStatus.ServerName}");
            
            if (connectionStatus.IsConnected)
            {
                _log.Info($"Sample Rate: {connectionStatus.SampleRate} Hz");
                _log.Info($"Buffer Size: {connectionStatus.BufferSize} samples");
                _log.Info("✓ Jack server is running and accessible!");
            }
            else
            {
                _log.Warn($"✗ Connection failed: {connectionStatus.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during Jack diagnostics");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}


