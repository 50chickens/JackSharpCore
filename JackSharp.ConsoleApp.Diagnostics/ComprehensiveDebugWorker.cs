using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Worker service for running comprehensive audio debugging including ALSA verification.
/// </summary>
public class ComprehensiveDebugWorker(
    ILog<ComprehensiveDebugWorker> log,
    IAlsaVerificationService alsaService,
    IJackRawBufferDebugService bufferDebugService,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private readonly ILog<ComprehensiveDebugWorker> _log = log;
    private readonly IAlsaVerificationService _alsaService = alsaService;
    private readonly IJackRawBufferDebugService _bufferDebugService = bufferDebugService;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _log.Info("╔══════════════════════════════════════════════════════════╗");
            _log.Info("║     COMPREHENSIVE AUDIO DEBUGGING TOOL                  ║");
            _log.Info("╚══════════════════════════════════════════════════════════╝\n");

            // Step 1: Verify ALSA layer
            _log.Info("STEP 1: ALSA Hardware Layer Verification");
            _log.Info("─────────────────────────────────────────────────────────\n");
            await _alsaService.VerifyAlsaCaptureAsync(stoppingToken);

            await Task.Delay(1000, stoppingToken);

            // Step 2: Deep Jack buffer inspection
            _log.Info("\n\nSTEP 2: Jack Audio Buffer Inspection");
            _log.Info("─────────────────────────────────────────────────────────\n");
            await _bufferDebugService.InspectBuffersAsync(stoppingToken);

            _log.Info("\n\n╔══════════════════════════════════════════════════════════╗");
            _log.Info("║     DEBUGGING COMPLETE                                   ║");
            _log.Info("╚══════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during comprehensive debugging");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
