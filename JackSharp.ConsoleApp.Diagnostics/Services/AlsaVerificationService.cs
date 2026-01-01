using JackSharp.ConsoleApp.Diagnostics.Logging;
using System.Diagnostics;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service for verifying ALSA audio capture independently of Jack.
/// Uses arecord and other ALSA tools to verify the hardware layer.
/// </summary>
public class AlsaVerificationService(ILog<AlsaVerificationService> log) : IAlsaVerificationService
{
    private readonly ILog<AlsaVerificationService> _log = log;

    public async Task VerifyAlsaCaptureAsync(CancellationToken cancellationToken)
    {
        _log.Info("=== ALSA Hardware Verification ===\n");

        // Check if Jack is using the device
        await CheckJackStatus();

        // Show ALSA device info
        await ShowAlsaDeviceInfo();

        // Show mixer settings
        await ShowMixerSettings();

        // Attempt to record a short sample
        await AttemptTestRecording(cancellationToken);

        _log.Info("\n=== ALSA Verification Complete ===");
    }

    private async Task CheckJackStatus()
    {
        try
        {
            _log.Info("Checking Jack status...");
            var result = await RunCommandAsync("systemctl", "is-active jack");
            
            if (result.exitCode == 0 && result.output.Trim() == "active")
            {
                _log.Info("✓ Jack service is running");
                _log.Info("  Note: Jack has exclusive access to the audio device");
                _log.Info("  ALSA tools may report 'device busy' - this is expected\n");
            }
            else
            {
                _log.Info("✗ Jack service is not running\n");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error checking Jack status");
        }
    }

    private async Task ShowAlsaDeviceInfo()
    {
        try
        {
            _log.Info("ALSA Device Information:");
            
            // Show sound cards
            var cardsResult = await RunCommandAsync("cat", "/proc/asound/cards");
            if (cardsResult.exitCode == 0)
            {
                _log.Info("Sound Cards:");
                foreach (var line in cardsResult.output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    _log.Info($"  {line}");
                }
            }

            // Show PCM devices
            _log.Info("\nPCM Devices:");
            var pcmResult = await RunCommandAsync("aplay", "-l");
            if (pcmResult.exitCode == 0)
            {
                foreach (var line in pcmResult.output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    _log.Info($"  {line}");
                }
            }

            _log.Info("");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error showing ALSA device info");
        }
    }

    private async Task ShowMixerSettings()
    {
        try
        {
            _log.Info("ALSA Mixer Settings (ADC Capture):");
            
            var result = await RunCommandAsync("amixer", "-c 0 sget ADC");
            if (result.exitCode == 0)
            {
                foreach (var line in result.output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    _log.Info($"  {line}");
                }

                // Parse and analyze the output
                if (result.output.Contains("Capture") && result.output.Contains("[") && result.output.Contains("%"))
                {
                    var lines = result.output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("Capture") && line.Contains("[") && line.Contains("%"))
                        {
                            // Extract percentage
                            var percentStart = line.LastIndexOf('[');
                            var percentEnd = line.LastIndexOf('%');
                            if (percentStart > 0 && percentEnd > percentStart)
                            {
                                var percentStr = line.Substring(percentStart + 1, percentEnd - percentStart - 1);
                                if (int.TryParse(percentStr, out int percent))
                                {
                                    if (percent < 10)
                                    {
                                        _log.Warn($"\n  ⚠ WARNING: ADC capture level is very low ({percent}%)");
                                        _log.Warn("  This may result in weak or no audio signal");
                                        _log.Warn("  Consider increasing with: amixer -c 0 set ADC 50%");
                                    }
                                    else if (percent < 30)
                                    {
                                        _log.Warn($"\n  ⚠ Note: ADC capture level is relatively low ({percent}%)");
                                    }
                                    else
                                    {
                                        _log.Info($"\n  ✓ ADC capture level: {percent}%");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                _log.Warn($"Could not read mixer settings (exit code: {result.exitCode})");
            }

            _log.Info("");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error showing mixer settings");
        }
    }

    private async Task AttemptTestRecording(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Attempting ALSA test recording...");
            _log.Info("(This will likely fail if Jack is running - that's OK, it's just a test)");
            
            var tempFile = Path.GetTempFileName() + ".wav";
            
            // Try to record 1 second
            var result = await RunCommandAsync(
                "arecord",
                $"-D hw:0 -f S32_LE -r 48000 -c 2 -d 1 {tempFile}",
                timeoutMs: 3000
            );

            if (result.exitCode == 0)
            {
                _log.Info("✓ Successfully recorded test file via ALSA");
                
                // Check file size
                if (File.Exists(tempFile))
                {
                    var fileInfo = new FileInfo(tempFile);
                    _log.Info($"  File size: {fileInfo.Length:N0} bytes");
                    
                    if (fileInfo.Length > 1000)
                    {
                        _log.Info("  ✓ File contains data - ALSA capture is working");
                    }
                    else
                    {
                        _log.Warn("  ⚠ File is very small - may not contain valid audio");
                    }
                    
                    // Cleanup
                    try { File.Delete(tempFile); } catch { }
                }
            }
            else
            {
                _log.Info($"✗ Could not record via ALSA (exit code: {result.exitCode})");
                
                if (result.output.Contains("busy") || result.output.Contains("Device or resource busy"))
                {
                    _log.Info("  This is expected when Jack is running");
                    _log.Info("  Jack has exclusive access to the audio device");
                }
                else
                {
                    _log.Warn($"  Error: {result.output}");
                }
            }

            _log.Info("");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during test recording");
        }
    }

    private async Task<(int exitCode, string output)> RunCommandAsync(
        string command, 
        string arguments, 
        int timeoutMs = 5000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new System.Text.StringBuilder();
            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return (process.ExitCode, outputBuilder.ToString());
        }
        catch (Exception ex)
        {
            return (-1, $"Exception: {ex.Message}");
        }
    }
}
