using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JackSharp.ConsoleApp.Diagnostics.Logging;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service for optimizing noise floor by testing different ALSA mixer settings.
/// Tests ADC Capture Volume and PGA Gain while monitoring signal levels.
/// Requires the 0.77V RMS reference signal connected to the right channel.
/// </summary>
public class NoiseFloorOptimizationService(ILog<NoiseFloorOptimizationService> log)
{
    private readonly ILog<NoiseFloorOptimizationService> _log = log;
    private const int SampleRate = 48000;
    private const int TestDurationSeconds = 3;

    public async Task OptimizeNoiseFloorAsync(CancellationToken cancellationToken, double? targetSignalDb = null)
    {
        try
        {
            _log.Info("Optimizing noise floor by testing ALSA mixer settings");

            // Use provided target or default to -12 dBFS
            var targetDb = targetSignalDb ?? -12;
            _log.Info($"Target Signal Level: {targetDb:F1}dBFS\n");

            _log.Info("IMPORTANT: Ensure 0.77V RMS sine wave is connected to right channel!");
            _log.Info("This tool will test different ALSA mixer values and show you the results.");

            _log.Info("Baseline - Signal drift: how much signal changed from baseline");

            // Perform signal quality check before optimization
            _log.Info("Verifying input signal quality");

            var sanityCheck = await MeasureChannelsAsync(cancellationToken);
            _log.Info($"Detected Right Channel (Signal): {sanityCheck.RightDb:F1}dBFS");
            _log.Info($"Detected Left Channel (Noise Floor): {sanityCheck.LeftDb:F1}dBFS");
            _log.Info($"Detected SNR: {(sanityCheck.RightDb - sanityCheck.LeftDb):F1}dB\n");

            // Sanity checks
            if (sanityCheck.RightDb < -100)
            {
                _log.Error("ERROR: No signal detected on right channel!");
                _log.Error("Please verify:");
                _log.Error("  1. 0.77V RMS sine wave is properly connected to right channel");
                _log.Error("  2. Audio cables are securely connected");
                _log.Error("  3. Signal generator is powered on and outputting");
                return;
            }

            if ((sanityCheck.RightDb - sanityCheck.LeftDb) < 20)
            {
                _log.Error("ERROR: Signal-to-Noise Ratio is too low (<20dB)!");
                _log.Error("This suggests either:");
                _log.Error("  1. The input signal is too weak (not a strong 0.77V RMS tone)");
                _log.Error("  2. There is significant external noise/interference");
                _log.Error("  3. The audio interface is malfunctioning");
                _log.Error("Cannot optimize with unreliable signal. Please fix the signal quality first.");
                return;
            }

            // Check if signal is already at or very close to target
            var signalDrift = Math.Abs(sanityCheck.RightDb - targetDb);
            if (signalDrift < 1.0)
            {
                _log.Info($"Signal is already very close to target (within {signalDrift:F1}dB)");
                _log.Info($"  Current: {sanityCheck.RightDb:F1}dBFS, Target: {targetDb:F1}dBFS");
                _log.Info($"  No optimization needed.\n");
                return;
            }

            _log.Info($"Signal is {signalDrift:F1}dB away from target - proceeding with optimization\n");

            // Get current settings as baseline
            _log.Info("Reading current ALSA mixer settings and measuring baseline");
            var baseline = GetCurrentAlsaSettings();
            var baselineMetrics = await MeasureChannelsAsync(cancellationToken);
            _log.Info($"Current Right Channel (Signal): {baselineMetrics.RightDb:F1}dBFS");
            _log.Info($"Current Left Channel (Noise Floor): {baselineMetrics.LeftDb:F1}dBFS");
            _log.Info($"Current SNR: {(baselineMetrics.RightDb - baselineMetrics.LeftDb):F1}dB\n");

            // Test different ADC Capture Volume settings

            var adcResults = new List<(int Volume, double RightDb, double LeftDb, double SNR)>();
            var adcVolumes = new[] { 20, 22, 24, 26, 28 };

            foreach (var volume in adcVolumes)
            {
                _log.Info($"Testing ADC Capture Volume: {volume}");
                SetAlsaControl("numid=21", volume);
                await Task.Delay(300, cancellationToken);

                var metrics = await MeasureChannelsAsync(cancellationToken);
                var snr = metrics.RightDb - metrics.LeftDb;
                adcResults.Add((volume, metrics.RightDb, metrics.LeftDb, snr));
                _log.Info($"  {metrics.RightDb:F1}dBFS | drift: {(metrics.RightDb - baselineMetrics.RightDb):+0.0;-0.0;0.0}dB | THD: {metrics.RightThd:F2}% | Crest: {metrics.RightCrestFactor:F2}");
            }

            var bestAdc = adcResults.OrderByDescending(r => r.SNR).First();
            _log.Info($"Best Audio drive Capture Volume: {bestAdc.Volume} (SNR: {bestAdc.SNR:F1}dB, Signal: {bestAdc.RightDb:F1}dBFS)");

            // Restore baseline ADC before testing PGA
            SetAlsaControl("numid=21", baseline.AdcCaptureVolume);
            await Task.Delay(300, cancellationToken);

            // Test PGA Gain Right

            var pgaResults = new List<(double Gain, double RightDb, double LeftDb, double SNR)>();
            var pgaGains = new[] { -10.0, -5.0, -2.5, 0.0, 2.5, 5.0, 10.0 };

            foreach (var gain in pgaGains)
            {
                _log.Info($"Testing PGA Gain Right: {gain:F1}dB");
                SetAlsaControl("numid=26", gain);
                await Task.Delay(300, cancellationToken);

                var metrics = await MeasureChannelsAsync(cancellationToken);
                var snr = metrics.RightDb - metrics.LeftDb;
                pgaResults.Add((gain, metrics.RightDb, metrics.LeftDb, snr));
                _log.Info($"  {metrics.RightDb:F1}dBFS | drift: {(metrics.RightDb - baselineMetrics.RightDb):+0.0;-0.0;0.0}dB | THD: {metrics.RightThd:F2}% | Crest: {metrics.RightCrestFactor:F2}");
            }

            var bestPga = pgaResults.OrderByDescending(r => r.SNR).First();
            _log.Info($"Best Programmable Gain Amplifier Right: {bestPga.Gain:F1}dB (SNR: {bestPga.SNR:F1}dB, Signal: {bestPga.RightDb:F1}dBFS)");

            // Show recommendations

            // Determine if we should use the optimized settings
            bool betterAdcFound = bestAdc.SNR > (baselineMetrics.RightDb - baselineMetrics.LeftDb);
            bool betterPgaFound = bestPga.SNR > (baselineMetrics.RightDb - baselineMetrics.LeftDb);

            if (betterAdcFound || betterPgaFound)
            {
                _log.Info("Improved settings found:");
                if (betterAdcFound)
                {
                    _log.Info($"  ADC Capture Volume: {bestAdc.Volume}");
                    _log.Info($"    SNR improvement: {(bestAdc.SNR - (baselineMetrics.RightDb - baselineMetrics.LeftDb)):+0.0;-0.0;0.0}dB");
                    _log.Info($"    Signal level: {bestAdc.RightDb:F1}dBFS");
                    _log.Info($"    Noise floor: {bestAdc.LeftDb:F1}dBFS\n");
                }

                if (betterPgaFound)
                {
                    _log.Info($"  PGA Gain Right: {bestPga.Gain:F1}dB");
                    _log.Info($"    SNR improvement: {(bestPga.SNR - (baselineMetrics.RightDb - baselineMetrics.LeftDb)):+0.0;-0.0;0.0}dB");
                    _log.Info($"    Signal level: {bestPga.RightDb:F1}dBFS");
                    _log.Info($"    Noise floor: {bestPga.LeftDb:F1}dBFS");
                    
                    // Get quality metrics by re-measuring with best settings
                    SetAlsaControl("numid=26", bestPga.Gain);
                    await Task.Delay(300, cancellationToken);
                    var qualityMetrics = await MeasureChannelsAsync(cancellationToken);
                    
                    _log.Info($"    Audio Quality Check:");
                    _log.Info($"      THD: {qualityMetrics.RightThd:F2}% (target: <1%)");
                    _log.Info($"      Crest Factor: {qualityMetrics.RightCrestFactor:F2} (sine ideal: 1.414)");
                    
                    if (qualityMetrics.RightThd > 1.0)
                        _log.Warn($"      Warning: Total Harmonic Distortion above 1% indicates possible distortion");
                    else if (qualityMetrics.RightCrestFactor > 2.0)
                        _log.Warn($"      Warning: High crest factor suggests clipping or non-linear distortion");
                    else
                        _log.Info($"      Good signal quality - no apparent distortion");
                }

                _log.Info("To apply these settings manually:");
                if (betterAdcFound)
                    _log.Info($"  amixer cset numid=21 {bestAdc.Volume}");
                if (betterPgaFound)
                    _log.Info($"  amixer cset numid=26 {bestPga.Gain:F1}\n");
                //add log messages to give commands to revert back to original settings
                _log.Info("\nTo revert back to original settings, run the following commands:");
                _log.Info($"  amixer cset numid=21 {baseline.AdcCaptureVolume}");
                _log.Info($"  amixer cset numid=26 {baseline.PgaGainRight:F1}\n");
                _log.Info($"  amixer cset numid=25 {baseline.PgaGainLeft:F1}\n");

                
            }
            else
            {
                _log.Info("Current settings are already optimal!");
                _log.Info($"Baseline ADC: {baseline.AdcCaptureVolume}, PGA Right: {baseline.PgaGainRight:F1}dB");
                _log.Info($"Baseline SNR: {(baselineMetrics.RightDb - baselineMetrics.LeftDb):F1}dB");
                _log.Info($"Baseline THD: {baselineMetrics.RightThd:F2}%");
                _log.Info($"Baseline Crest Factor: {baselineMetrics.RightCrestFactor:F2}\n");
            }

            // Restore original settings
            _log.Info("Restoring original ALSA settings...");
            SetAlsaControl("numid=21", baseline.AdcCaptureVolume);
            SetAlsaControl("numid=26", baseline.PgaGainRight);
            SetAlsaControl("numid=25", baseline.PgaGainLeft);
            
            _log.Info("Optimization test complete.");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during noise floor optimization");
        }
    }

    private async Task<ChannelMetrics> MeasureChannelsAsync(CancellationToken cancellationToken)
    {
        var processor = new Processor("NoiseFloorMeter", 2, 0);
        try
        {
            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return new ChannelMetrics { RightDb = -160, LeftDb = -160 };
            }

            await Task.Delay(300, cancellationToken);

            // Connect Jack inputs to ALSA hardware
            ConnectJackPorts("system:capture_1", "NoiseFloorMeter:audioin_1");  // Left (noise floor)
            ConnectJackPorts("system:capture_2", "NoiseFloorMeter:audioin_2");  // Right (signal)

            var leftBuffer = new List<float>();
            var rightBuffer = new List<float>();
            var frameCount = 0;
            var targetFrames = SampleRate * TestDurationSeconds;

            processor.ProcessFunc = buffer =>
            {
                if (frameCount >= targetFrames)
                    return;

                if (buffer.AudioIn[0].Audio != null)
                {
                    foreach (var sample in buffer.AudioIn[0].Audio)
                    {
                        if (frameCount < targetFrames)
                        {
                            leftBuffer.Add(sample);
                            frameCount++;
                        }
                    }
                }

                if (buffer.AudioIn[1].Audio != null && rightBuffer.Count < targetFrames)
                {
                    foreach (var sample in buffer.AudioIn[1].Audio)
                    {
                        if (rightBuffer.Count < targetFrames)
                            rightBuffer.Add(sample);
                    }
                }
            };

            // Wait for capture
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (frameCount < targetFrames && sw.Elapsed.TotalSeconds < TestDurationSeconds + 2)
            {
                await Task.Delay(100, cancellationToken);
            }
            sw.Stop();

            var leftAnalysis = AnalyzeSignal(leftBuffer.ToArray());
            var rightAnalysis = AnalyzeSignal(rightBuffer.ToArray());

            return new ChannelMetrics
            {
                LeftDb = leftAnalysis.RmsDb,
                LeftThd = leftAnalysis.Thd,
                RightDb = rightAnalysis.RmsDb,
                RightThd = rightAnalysis.Thd,
                RightCrestFactor = rightAnalysis.CrestFactor
            };
        }
        finally
        {
            processor?.Dispose();
        }
    }

    private void ConnectJackPorts(string source, string dest)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "jack_connect",
                Arguments = $"\"{source}\" \"{dest}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                process?.WaitForExit();
            }
        }
        catch
        {
            // Silently fail - port may already be connected
        }
    }

    private void SetAlsaControl(string numid, int intValue)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "amixer",
                Arguments = $"cset {numid} {intValue}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                process?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to set ALSA control {numid}: {ex.Message}");
        }
    }

    private void SetAlsaControl(string numid, double doubleValue)
    {
        try
        {
            // For enumerated controls like PGA Gain, convert dB value to item index
            // Item #0 = -12.0dB, Item #24 = 0.0dB, Item #104 = 40.0dB
            // Formula: itemIndex = (dBValue + 12.0) / 0.5
            int itemIndex = (int)Math.Round((doubleValue + 12.0) / 0.5);
            itemIndex = Math.Max(0, Math.Min(104, itemIndex)); // Clamp to valid range
            
            var psi = new ProcessStartInfo
            {
                FileName = "amixer",
                Arguments = $"cset {numid} {itemIndex}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                process?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to set ALSA control {numid}: {ex.Message}");
        }
    }

    private AlsaSettings GetCurrentAlsaSettings()
    {
        var settings = new AlsaSettings();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "amixer",
                Arguments = "cget numid=21",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                var output = process?.StandardOutput.ReadToEnd() ?? "";
                if (output.Contains("values="))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"values=(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var val))
                        settings.AdcCaptureVolume = val;
                }
                process?.WaitForExit();
            }

            psi.Arguments = "cget numid=26";
            using (var process = Process.Start(psi))
            {
                var output = process?.StandardOutput.ReadToEnd() ?? "";
                if (output.Contains("value="))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"value=(-?\d+\.?\d*)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out var val))
                        settings.PgaGainRight = val;
                }
                process?.WaitForExit();
            }

            psi.Arguments = "cget numid=25";
            using (var process = Process.Start(psi))
            {
                var output = process?.StandardOutput.ReadToEnd() ?? "";
                if (output.Contains("value="))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"value=(-?\d+\.?\d*)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out var val))
                        settings.PgaGainLeft = val;
                }
                process?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Could not read ALSA settings: {ex.Message}");
        }
        return settings;
    }

    private AudioMetrics AnalyzeSignal(float[] samples)
    {
        if (samples.Length == 0)
            return new AudioMetrics { RmsDb = -160, PeakDb = -160, Thd = 0, CrestFactor = 0 };

        var rms = Math.Sqrt(samples.Sum(s => s * s) / samples.Length);
        var peak = samples.Max(s => Math.Abs(s));
        var rmsDb = rms > 0 ? 20 * Math.Log10(rms) : -160;
        var peakDb = peak > 0 ? 20 * Math.Log10(peak) : -160;
        
        // Calculate crest factor (peak to RMS ratio)
        var crestFactor = rms > 0 ? peak / rms : 0;
        
        // Calculate THD via FFT-based harmonic analysis
        var thd = CalculateTHD(samples);

        return new AudioMetrics
        {
            RmsDb = rmsDb,
            PeakDb = peakDb,
            Thd = thd,
            CrestFactor = crestFactor
        };
    }

    private double CalculateTHD(float[] samples)
    {
        if (samples.Length < 2) return 0;

        // Simple FFT-based THD calculation
        // Analyze the first 1000 samples for efficiency
        var analysisLength = Math.Min(samples.Length, 1024);
        var window = new float[analysisLength];
        Array.Copy(samples, window, analysisLength);

        // Apply Hann window to reduce spectral leakage
        for (int i = 0; i < analysisLength; i++)
        {
            var w = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (analysisLength - 1)));
            window[i] *= (float)w;
        }

        // Simple peak detection in frequency domain (approximate THD)
        // Look for dominant frequency and compare energy in harmonics
        var fft = SimpleFFT(window);
        
        // Find fundamental (peak in first quarter of spectrum)
        double fundamentalMagnitude = 0;
        int fundamentalBin = 0;
        for (int i = 1; i < fft.Length / 4; i++)
        {
            if (fft[i] > fundamentalMagnitude)
            {
                fundamentalMagnitude = fft[i];
                fundamentalBin = i;
            }
        }

        if (fundamentalMagnitude == 0)
            return 0;

        // Sum harmonic energy (2x, 3x, 4x, 5x the fundamental)
        double harmonicEnergy = 0;
        for (int harmonic = 2; harmonic <= 5 && fundamentalBin * harmonic < fft.Length; harmonic++)
        {
            int bin = fundamentalBin * harmonic;
            harmonicEnergy += fft[bin] * fft[bin];
        }

        // THD% = sqrt(harmonics) / fundamental * 100
        var thd = Math.Sqrt(harmonicEnergy) / fundamentalMagnitude * 100;
        return Math.Min(thd, 100); // Cap at 100% for very distorted signals
    }

    private double[] SimpleFFT(float[] input)
    {
        // Simple DFT for harmonic analysis (not optimal speed, but accurate)
        int n = input.Length;
        var output = new double[n / 2];

        for (int k = 0; k < n / 2; k++)
        {
            double real = 0, imag = 0;
            for (int t = 0; t < n; t++)
            {
                double angle = -2 * Math.PI * k * t / n;
                real += input[t] * Math.Cos(angle);
                imag += input[t] * Math.Sin(angle);
            }
            output[k] = Math.Sqrt(real * real + imag * imag) / n;
        }

        return output;
    }

    private class ChannelMetrics
    {
        public double LeftDb { get; set; }
        public double RightDb { get; set; }
        public double LeftThd { get; set; }
        public double RightThd { get; set; }
        public double RightCrestFactor { get; set; }
    }

    private class AlsaSettings
    {
        public int AdcCaptureVolume { get; set; } = 24;
        public double PgaGainRight { get; set; } = 0.0;
        public double PgaGainLeft { get; set; } = 0.0;
    }

    private class AudioMetrics
    {
        public double RmsDb { get; set; }
        public double PeakDb { get; set; }
        public double Thd { get; set; }
        public double CrestFactor { get; set; }
    }
}

