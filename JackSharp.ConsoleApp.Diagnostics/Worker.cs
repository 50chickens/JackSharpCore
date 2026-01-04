using JackSharp.ConsoleApp.Diagnostics.Interfaces;
using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Main worker service for Jack diagnostics.
/// Discovers Jack servers, determines diagnostics type, and dispatches to appropriate service.
/// </summary>
public class DiagnosticsWorker(
    ILog<DiagnosticsWorker> log,
    IJackServerDiscoveryService discoveryService,
    IJackConnectionManagerService connectionManager,
    IJackSimpleLevelMeterService simpleLevelMeterService,
    IJackHardwareInputMonitorService hardwareInputMonitorService,
    LoopbackTestService loopbackTestService,
    IJackRawBufferDebugService rawBufferDebugService,
    IHostApplicationLifetime lifetime,
    string[] args) : BackgroundService
{
    private readonly ILog<DiagnosticsWorker> _log = log;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Determine diagnostics type from command-line arguments
            var diagnosticsType = DetermineDiagnosticsType(args);
            var connectionDetails = await EstablishConnectionAsync(diagnosticsType, stoppingToken);

            if (!connectionDetails.IsConnected && diagnosticsType != JackDiagnosticsType.ServerDiscovery)
            {
                _log.Error($"Failed to connect to Jack: {connectionDetails.ErrorMessage}");
                return;
            }

            // Dispatch to appropriate service based on diagnostics type
            switch (diagnosticsType)
            {
                case JackDiagnosticsType.ServerDiscovery:
                    await DisplayServerStatusAsync();
                    break;

                case JackDiagnosticsType.MeasureInputLevels:
                    await hardwareInputMonitorService.MonitorInputLevelsAsync(stoppingToken);
                    break;

                case JackDiagnosticsType.SimpleLevelMeter:
                    await simpleLevelMeterService.MonitorAndPrintLevelsAsync(stoppingToken);
                    break;

                case JackDiagnosticsType.AudioTest:
                    // Audio test would be called here
                    _log.Info("Audio test diagnostics would run here");
                    break;

                case JackDiagnosticsType.LoopbackTest:
                    await loopbackTestService.RunLoopbackTestAsync(stoppingToken);
                    break;

                default:
                    _log.Warn($"Unknown diagnostics type: {diagnosticsType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during diagnostics");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private JackDiagnosticsType DetermineDiagnosticsType(string[] args)
    {
        var argString = string.Join(" ", args);

        return argString switch
        {
            _ when argString.Contains("--monitor-inputs") => JackDiagnosticsType.MeasureInputLevels,
            _ when argString.Contains("--monitor-levels") => JackDiagnosticsType.SimpleLevelMeter,
            _ when argString.Contains("--audio-test") => JackDiagnosticsType.AudioTest,
            _ when argString.Contains("--audio-quality") => JackDiagnosticsType.AudioQualityAnalysis,
            _ when argString.Contains("--loopback") => JackDiagnosticsType.LoopbackTest,
            _ => JackDiagnosticsType.ServerDiscovery
        };
    }

    private async Task<JackConnectionDetails> EstablishConnectionAsync(JackDiagnosticsType type, CancellationToken cancellationToken)
    {
        var servers = await discoveryService.DiscoverServersAsync();
        var connectionStatus = await connectionManager.ConnectAsync();

        return new JackConnectionDetails
        {
            ServerName = connectionStatus.ServerName,
            SampleRate = connectionStatus.SampleRate,
            BufferSize = connectionStatus.BufferSize,
            IsConnected = connectionStatus.IsConnected,
            ErrorMessage = connectionStatus.ErrorMessage,
            DiagnosticsType = type
        };
    }

    private async Task DisplayServerStatusAsync()
    {
        var servers = await discoveryService.DiscoverServersAsync();
        var connectionStatus = await connectionManager.ConnectAsync();

        _log.Info("Jack server status:");

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
            _log.Warn("No Jack servers found");
        }

        _log.Info("Connection details:");
        _log.Info($"Connected: {connectionStatus.IsConnected}");
        _log.Info($"Server Name: {connectionStatus.ServerName}");

        if (connectionStatus.IsConnected)
        {
            _log.Info($"Sample Rate: {connectionStatus.SampleRate} Hz");
            _log.Info($"Buffer Size: {connectionStatus.BufferSize} samples");
            _log.Info("Jack server is running and accessible");
        }
        else
        {
            _log.Warn($"Connection failed: {connectionStatus.ErrorMessage}");
        }
    }
}


