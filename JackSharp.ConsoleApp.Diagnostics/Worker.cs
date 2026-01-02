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
    AudioDiagnosticsWorker audioDiagnosticsWorker,
    AudioQualityAnalysisService audioQualityAnalysisService,
    LoopbackTestService loopbackTestService,
    IAlsaVerificationService alsaVerificationService,
    IJackRawBufferDebugService rawBufferDebugService,
    NoiseFloorOptimizationService noiseFloorOptimizationService,
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

                case JackDiagnosticsType.AudioQualityAnalysis:
                    await audioQualityAnalysisService.AnalyzeAudioQualityAsync(1000.0, 5, stoppingToken);
                    break;

                case JackDiagnosticsType.LoopbackTest:
                    await loopbackTestService.RunLoopbackTestAsync(stoppingToken);
                    break;

                case JackDiagnosticsType.ComprehensiveDebug:
                    await ComprehensiveDebugAsync(alsaVerificationService, rawBufferDebugService, stoppingToken);
                    break;

                case JackDiagnosticsType.RawBufferDebug:
                    await rawBufferDebugService.InspectBuffersAsync(stoppingToken);
                    break;

                case JackDiagnosticsType.NoiseFloorOptimization:
                    // Parse optional target dBFS level from arguments
                    double? targetDb = null;
                    if (args.Length > 1)
                    {
                        foreach (var arg in args)
                        {
                            if (arg.StartsWith("--target-db=") && double.TryParse(arg.Substring("--target-db=".Length), out var db))
                            {
                                targetDb = db;
                                break;
                            }
                        }
                    }
                    await noiseFloorOptimizationService.OptimizeNoiseFloorAsync(stoppingToken, targetDb);
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
            _ when argString.Contains("--noise-floor") => JackDiagnosticsType.NoiseFloorOptimization,
            _ when argString.Contains("--debug") => JackDiagnosticsType.ComprehensiveDebug,
            _ when argString.Contains("--raw-buffer") => JackDiagnosticsType.RawBufferDebug,
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

    private async Task ComprehensiveDebugAsync(IAlsaVerificationService alsaService, IJackRawBufferDebugService bufferDebugService, CancellationToken cancellationToken)
    {
        _log.Info("COMPREHENSIVE AUDIO DEBUGGING TOOL");

        _log.Info("STEP 1: ALSA Hardware Layer Verification");
        await alsaService.VerifyAlsaCaptureAsync(cancellationToken);

        await Task.Delay(1000, cancellationToken);

        _log.Info("Inspecting Jack audio buffers");
        await bufferDebugService.InspectBuffersAsync(cancellationToken);

        _log.Info("Debugging complete");
    }
}


