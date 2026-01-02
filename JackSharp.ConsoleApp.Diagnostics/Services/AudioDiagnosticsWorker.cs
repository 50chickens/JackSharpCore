using JackSharp.ConsoleApp.Diagnostics.Logging;
using Microsoft.Extensions.Options;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Orchestrates audio diagnostics test: generates test tone and measures input levels.
/// Pattern from SNRReduction example but using Jack instead of ALSA.
/// </summary>
public class AudioDiagnosticsWorker(
    ILog<AudioDiagnosticsWorker> log,
    IJackAudioLevelMeterService levelMeterService,
    IJackTestToneService testToneService,
    IOptions<JackOptions> jackOptions) : BackgroundService
{
    private readonly ILog<AudioDiagnosticsWorker> _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IJackAudioLevelMeterService _levelMeterService = levelMeterService ?? throw new ArgumentNullException(nameof(levelMeterService));
    private readonly IJackTestToneService _testToneService = testToneService ?? throw new ArgumentNullException(nameof(testToneService));
    private readonly JackOptions _jackOptions = jackOptions.Value ?? new JackOptions();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _log.Info("Audio Diagnostics Worker starting...");

            // Step 1: Generate test tone
            _log.Info("Generating 1000Hz test tone at -6dBFS for 5 seconds on BOTH channels...");
            
            _testToneService.PlayTestTone(1000, -6.0, 5000);

            await Task.Delay(1000, stoppingToken); // Wait for tone to settle

            // Step 2: Measure input levels during silence
            _log.Info("Recording input levels during silence (3 seconds)...");
            
            var (silenceDbfs, silenceRms) = _levelMeterService.MeasureInputLevels(3000, 2);

            _log.Info("Silence levels recorded:");
            for (int ch = 0; ch < silenceDbfs.Count; ch++)
            {
                _log.Info($"  Channel {ch + 1}: {silenceDbfs[ch]:F2} dBFS (RMS: {silenceRms[ch]:F4})");
            }

            await Task.Delay(500, stoppingToken);

            // Step 3: Play concurrent test tone and measure
            _log.Info("Playing 1000Hz test tone WHILE measuring input (loopback test)...");
            
            // Play tone in background
            var toneTask = Task.Run(() =>
            {
                _testToneService.PlayTestTone(1000, -6.0, 5000);
            }, stoppingToken);

            // Measure while tone plays
            await Task.Delay(1000, stoppingToken); // Let tone start and stabilize
            var (toneDbfs, toneRms) = _levelMeterService.MeasureInputLevels(4000, 2);

            _log.Info("Levels with tone:");
            for (int ch = 0; ch < toneDbfs.Count; ch++)
            {
                _log.Info($"  Channel {ch + 1}: {toneDbfs[ch]:F2} dBFS (RMS: {toneRms[ch]:F4})");
            }

            await toneTask;

            // Step 4: Summary and diagnosis
            _log.Info("Diagnostics complete - analyzing results...");
            
            bool hasInputSignal = false;
            if (toneDbfs.Any() && silenceDbfs.Any())
            {
                double signalToNoise = toneDbfs[0] - silenceDbfs[0];
                _log.Info($"Signal-to-Noise Ratio (Channel 1): {signalToNoise:F2} dB");

                if (signalToNoise > 20.0)
                {
                    _log.Info("Loopback verified - audio signal detected at input");
                    _log.Info("  → Outputs are sending audio");
                    _log.Info("  → Inputs are receiving audio");
                    hasInputSignal = true;
                }
                else
                {
                    _log.Warn("Loopback issue - weak or no signal at input");
                    _log.Warn("  → Check loopback cable connections");
                    _log.Warn("  → Check input levels (trim/gain)");
                    _log.Warn("  → Check Jack port connectivity");
                }
            }

            if (!hasInputSignal)
            {
                _log.Error("Audio diagnostics failed: No input signal detected");
                _log.Error("Recommended troubleshooting:");
                _log.Error("1. Check physical loopback cable is connected to in/out jacks");
                _log.Error("2. Run: jack_lsp -c to see all port connections");
                _log.Error("3. Run: jack_monitor to see audio activity");
                _log.Error("4. Check alsamixer levels for input/output trim");
                _log.Error("5. Verify Jack ports are active: jack_lsp");
            }
            else
            {
                _log.Info("AUDIO DIAGNOSTICS PASSED: System audio path is functional");
                _log.Info("Next: Check mod-host and mod-ui plugin chain");
            }
        }
        catch (OperationCanceledException)
        {
            _log.Info("Audio diagnostics cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Audio diagnostics error: {ex.Message}");
        }
    }
}
