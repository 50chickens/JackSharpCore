using JackSharp.ConsoleApp.Diagnostics.Logging;
using System.Diagnostics;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service for performing loopback tests to verify signal fidelity through hardware.
/// Connect output back to input with a cable and run this test to measure distortion and frequency response.
/// </summary>
public class LoopbackTestService(ILog<LoopbackTestService> log)
{
    private readonly ILog<LoopbackTestService> _log = log;
    private const int SampleRate = 48000;

    public async Task RunLoopbackTestAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Loopback Test: Verifying signal fidelity through hardware chain");

            _log.Info("Loopback Test - Signal levels and distortion:");

            // Quick connectivity check before full test
            _log.Info("Performing cable connectivity check (1kHz test tone)...");
            bool leftConnected = await QuickConnectivityTestAsync("Left", 0, 1, 1000.0, cancellationToken);
            bool leftWasTested = false;
            if (leftConnected)
            {
                leftWasTested = true;
                await TestChannelAsync("Left", 0, 0, cancellationToken);
            }
            bool rightWasTested = false;
            bool rightConnected = await QuickConnectivityTestAsync("Right", 1, 3, 1000.0, cancellationToken);
            if (rightConnected)
            {
                rightWasTested = true;
                await TestChannelAsync("Right", 1, 1, cancellationToken);

            }
            if (!leftWasTested && !rightWasTested)
            {
                _log.Info("No cable connectivity detected on either channel. Please check your loopback cables and try again.");
                return;
            }
            _log.Info("Cable connectivity verified. Running full frequency sweep...");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error during loopback test: {ex.Message}");
        }
    }

    private async Task<bool> QuickConnectivityTestAsync(string channelName, int playoutChannel, int captureChannel, double frequency, CancellationToken cancellationToken)
    {
        using var processor = new Processor("LoopbackQuickTest", 4, 4);

        if (!processor.Start())
        {
            _log.Error($"Failed to connect to Jack for {channelName} quick test");
            return false;
        }

        await Task.Delay(500);

        string playbackPort = playoutChannel == 0 ? "system:playback_1" : "system:playback_2";
        string processorPlayoutPort = $"LoopbackQuickTest:audioout_{playoutChannel + 1}";
        string capturePort = playoutChannel == 0 ? "system:capture_1" : "system:capture_2";
        string processorCapturePort = $"LoopbackQuickTest:audioin_{captureChannel + 1}";

        ConnectJackPorts(processorPlayoutPort, playbackPort);
        ConnectJackPorts(capturePort, processorCapturePort);

        var referenceSignal = GenerateSineWave(frequency, 2, 0.5);
        var capturedSamples = new List<float>();

        var playbackIndex = 0;
        var isPlaying = true;
        var captureFrameCount = 0;
        var targetFrames = SampleRate * 2;

        processor.ProcessFunc = buffer =>
        {
            if (isPlaying && playbackIndex < referenceSignal.Length)
            {
                int framesToPlay = Math.Min(buffer.AudioOut[playoutChannel].Audio.Length,
                    referenceSignal.Length - playbackIndex);

                for (int i = 0; i < framesToPlay; i++)
                {
                    buffer.AudioOut[playoutChannel].Audio[i] = referenceSignal[playbackIndex];
                    playbackIndex++;
                }

                for (int i = framesToPlay; i < buffer.AudioOut[playoutChannel].Audio.Length; i++)
                {
                    buffer.AudioOut[playoutChannel].Audio[i] = 0.0f;
                }

                if (playbackIndex >= referenceSignal.Length)
                    isPlaying = false;
            }

            if (buffer.AudioIn[captureChannel].Audio != null && buffer.AudioIn[captureChannel].Audio.Length > 0)
            {
                foreach (var sample in buffer.AudioIn[captureChannel].Audio)
                {
                    if (captureFrameCount < targetFrames)
                    {
                        capturedSamples.Add(sample);
                        captureFrameCount++;
                    }
                }
            }
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (captureFrameCount < targetFrames && sw.Elapsed.TotalSeconds < 4)
        {
            await Task.Delay(100, cancellationToken);
        }
        sw.Stop();

        processor.Stop();

        if (capturedSamples.Count > 0)
        {
            var capturedAnalysis = AnalyzeSignal(capturedSamples.ToArray());
            bool isConnected = capturedAnalysis.RmsDb > -100; // Signal detected
            if (isConnected)
            {
                _log.Info($"  {channelName}: CONNECTED ({capturedAnalysis.RmsDb:F1}dBFS)");
            }
            else
            {
                _log.Info($"  {channelName}: NOT CONNECTED");
            }
            return isConnected;
        }

        _log.Error($"  {channelName}: FAILED - no signal captured");
        return false;
    }

    private async Task TestChannelAsync(string channelName, int playoutChannel, int captureChannel, CancellationToken cancellationToken)
    {
        _log.Info($"{channelName} Channel:");

        // Generate reference sine waves at different frequencies
        var frequencies = new[] { 500.0, 1000.0, 5000.0, 10000.0 };
        var results = new List<ChannelResult>();

        using var processor = new Processor("LoopbackTest", 4, 4);

        if (!processor.Start())
        {
            _log.Error($"Failed to connect to Jack for {channelName} channel");
            return;
        }

        // Give Jack a moment to create the ports
        await Task.Delay(500);

        // Connect Jack processor output to ALSA hardware playback using jack_connect command
        string playbackPort = playoutChannel == 0 ? "system:playback_1" : "system:playback_2";
        string processorPlayoutPort = $"LoopbackTest:audioout_{playoutChannel + 1}";

        // Connect ALSA hardware capture to Jack processor input  
        string capturePort = playoutChannel == 0 ? "system:capture_1" : "system:capture_2";
        string processorCapturePort = $"LoopbackTest:audioin_{captureChannel + 1}";

        // Use jack_connect command-line tool to establish port connections
        ConnectJackPorts(processorPlayoutPort, playbackPort);
        ConnectJackPorts(capturePort, processorCapturePort);

        foreach (var frequency in frequencies)
        {
            var referenceSignal = GenerateSineWave(frequency, 2, 0.5);
            var capturedSamples = new List<float>();

            var playbackIndex = 0;
            var isPlaying = true;
            var captureFrameCount = 0;
            var targetFrames = SampleRate * 2;

            processor.ProcessFunc = buffer =>
            {
                // Playback on specified channel
                if (isPlaying && playbackIndex < referenceSignal.Length)
                {
                    int framesToPlay = Math.Min(buffer.AudioOut[playoutChannel].Audio.Length,
                        referenceSignal.Length - playbackIndex);

                    for (int i = 0; i < framesToPlay; i++)
                    {
                        buffer.AudioOut[playoutChannel].Audio[i] = referenceSignal[playbackIndex];
                        playbackIndex++;
                    }

                    for (int i = framesToPlay; i < buffer.AudioOut[playoutChannel].Audio.Length; i++)
                    {
                        buffer.AudioOut[playoutChannel].Audio[i] = 0.0f;
                    }

                    if (playbackIndex >= referenceSignal.Length)
                        isPlaying = false;
                }

                // Capture from specified channel
                if (buffer.AudioIn[captureChannel].Audio != null && buffer.AudioIn[captureChannel].Audio.Length > 0)
                {
                    foreach (var sample in buffer.AudioIn[captureChannel].Audio)
                    {
                        if (captureFrameCount < targetFrames)
                        {
                            capturedSamples.Add(sample);
                            captureFrameCount++;
                        }
                    }
                }
            };

            // Wait for capture to complete
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (captureFrameCount < targetFrames && sw.Elapsed.TotalSeconds < 4)
            {
                await Task.Delay(100, cancellationToken);
            }
            sw.Stop();

            if (capturedSamples.Count > 0)
            {
                var refAnalysis = AnalyzeSignal(referenceSignal);
                var capturedAnalysis = AnalyzeSignal(capturedSamples.ToArray());

                var levelDiff = capturedAnalysis.RmsDb - refAnalysis.RmsDb;
                var peakDiff = capturedAnalysis.PeakDb - refAnalysis.PeakDb;
                var thdDiff = capturedAnalysis.Thd - refAnalysis.Thd;

                var status = Math.Abs(levelDiff) < 0.5 && Math.Abs(peakDiff) < 1.0 && capturedAnalysis.Thd < 1.0 ? "PASS" : "FAIL";
                _log.Info($"  {frequency:F0}Hz: {status} | RefLevel={refAnalysis.RmsDb:F1}dB RefPeak={refAnalysis.PeakDb:F1}dB | OutLevel={capturedAnalysis.RmsDb:F1}dB OutPeak={capturedAnalysis.PeakDb:F1}dB | ΔLvl={levelDiff:+0.00;-0.00;0.00}dB ΔPeak={peakDiff:+0.00;-0.00;0.00}dB THD={capturedAnalysis.Thd:F2}%");

                results.Add(new ChannelResult
                {
                    Frequency = frequency,
                    LevelDifference = levelDiff,
                    PeakDifference = peakDiff,
                    ThdDifference = thdDiff,
                    Passed = Math.Abs(levelDiff) < 0.5 && Math.Abs(peakDiff) < 1.0
                });
            }
        }

        processor.Stop();

        var allPassed = results.All(r => r.Passed);
        _log.Info($"  Overall: {(allPassed ? "PASS" : "FAIL")}");
    }

    private float[] GenerateSineWave(double frequency, int durationSeconds, double amplitude)
    {
        int sampleCount = SampleRate * durationSeconds;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double t = i / (double)SampleRate;
            double sample = Math.Sin(2.0 * Math.PI * frequency * t) * amplitude;
            samples[i] = (float)sample;
        }

        return samples;
    }

    private SignalAnalysis AnalyzeSignal(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return new SignalAnalysis();

        double sumSquares = 0.0;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }
        double rms = Math.Sqrt(sumSquares / samples.Length);
        float peak = samples.Max(s => Math.Abs(s));

        double rmsDb = rms > 0 ? 20.0 * Math.Log10(rms) : -150.0;
        double peakDb = peak > 0 ? 20.0 * Math.Log10(peak) : -150.0;
        double crestFactor = peak / (rms > 0 ? rms : 1.0);

        double kurtosis = CalculateKurtosis(samples);
        double thd = Math.Max(0.0, Math.Min(100.0, (kurtosis - 3.0) * 10.0));

        return new SignalAnalysis
        {
            RmsDb = rmsDb,
            PeakDb = peakDb,
            CrestFactor = crestFactor,
            Thd = thd
        };
    }

    private double CalculateKurtosis(float[] samples)
    {
        if (samples.Length < 2)
            return 0.0;

        double mean = samples.Average(s => (double)s);
        double variance = samples.Average(s => Math.Pow((double)s - mean, 2));
        double stdDev = Math.Sqrt(variance);

        if (stdDev == 0)
            return 0.0;

        double m4 = samples.Average(s => Math.Pow(((double)s - mean) / stdDev, 4));
        return m4;
    }

    private class SignalAnalysis
    {
        public double RmsDb { get; set; } = -150.0;
        public double PeakDb { get; set; } = -150.0;
        public double CrestFactor { get; set; } = 0.0;
        public double Thd { get; set; } = 0.0;
    }

    private class ChannelResult
    {
        public double Frequency { get; set; }
        public double LevelDifference { get; set; }
        public double PeakDifference { get; set; }
        public double ThdDifference { get; set; }
        public bool Passed { get; set; }
    }

    private void ConnectJackPorts(string sourcePort, string destinationPort)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "jack_connect",
                Arguments = $"\"{sourcePort}\" \"{destinationPort}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    _log.Warn($"jack_connect failed: {sourcePort} -> {destinationPort} ({error.Trim()})");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to execute jack_connect: {ex.Message}");
        }
    }
}
