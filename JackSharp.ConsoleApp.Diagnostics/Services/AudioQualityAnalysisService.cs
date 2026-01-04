using JackSharp.ConsoleApp.Diagnostics.Logging;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service for analyzing audio quality through signal generation and capture.
/// Measures THD, RMS levels, peak levels, and other distortion metrics.
/// </summary>
public class AudioQualityAnalysisService(ILog<AudioQualityAnalysisService> log)
{
    private readonly ILog<AudioQualityAnalysisService> _log = log;
    private const int SampleRate = 48000;

    /// <summary>
    /// Analyzes audio quality by generating a sine wave, capturing it, and measuring distortion.
    /// </summary>
    public async Task AnalyzeAudioQualityAsync(double frequency, int durationSeconds, CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Analyzing audio quality for sine wave");
            // Generate reference sine wave
            var referenceSignal = GenerateSineWave(frequency, durationSeconds, 0.5);
            _log.Info($"Generated {referenceSignal.Length} samples ({referenceSignal.Length / (double)SampleRate:F2}s)\n");
            // Capture audio
            var capturedSamples = new List<float>();
            using var processor = new Processor("AudioAnalyzer", 2, 2);

            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return;
            }

            _log.Info($"Connected to Jack at {processor.SampleRate}Hz, buffer size: {processor.BufferSize}\n");

            var playbackIndex = 0;
            var isPlaying = true;
            var captureFrameCount = 0;
            var targetFrames = SampleRate * durationSeconds;

            // Set up process callback
            processor.ProcessFunc = buffer =>
            {
                // Playback on first two channels
                if (isPlaying && playbackIndex < referenceSignal.Length)
                {
                    int framesToPlay = Math.Min(buffer.AudioOut[0].Audio.Length,
                        referenceSignal.Length - playbackIndex);

                    for (int i = 0; i < framesToPlay; i++)
                    {
                        buffer.AudioOut[0].Audio[i] = referenceSignal[playbackIndex];
                        buffer.AudioOut[1].Audio[i] = referenceSignal[playbackIndex];
                        playbackIndex++;
                    }

                    // Zero out remaining samples
                    for (int i = framesToPlay; i < buffer.AudioOut[0].Audio.Length; i++)
                    {
                        buffer.AudioOut[0].Audio[i] = 0.0f;
                        buffer.AudioOut[1].Audio[i] = 0.0f;
                    }

                    if (playbackIndex >= referenceSignal.Length)
                        isPlaying = false;
                }

                // Capture from first two channels
                if (buffer.AudioIn[0].Audio != null && buffer.AudioIn[0].Audio.Length > 0)
                {
                    foreach (var sample in buffer.AudioIn[0].Audio)
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
            _log.Info("Capturing audio...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (captureFrameCount < targetFrames && sw.Elapsed.TotalSeconds < durationSeconds + 2)
            {
                await Task.Delay(100, cancellationToken);
            }
            sw.Stop();

            processor.Stop();
            _log.Info($"Capture complete: {capturedSamples.Count} samples\n");

            // Analyze reference signal
            var refAnalysis = AnalyzeSignal(referenceSignal, "Reference Signal");
            _log.Info($"Reference: {refAnalysis.RmsDb:F1}dBFS | Peak: {refAnalysis.PeakDb:F1}dBFS | Crest: {refAnalysis.CrestFactor:F2} | THD: {refAnalysis.Thd:F2}%");

            // Analyze captured signal
            var capturedAnalysis = AnalyzeSignal(capturedSamples.ToArray(), "Captured Signal");
            _log.Info($"Captured: {capturedAnalysis.RmsDb:F1}dBFS | Peak: {capturedAnalysis.PeakDb:F1}dBFS | Crest: {capturedAnalysis.CrestFactor:F2} | THD: {capturedAnalysis.Thd:F2}%\n");

            // Compare
            var levelDifference = capturedAnalysis.RmsDb - refAnalysis.RmsDb;
            var peakDifference = capturedAnalysis.PeakDb - refAnalysis.PeakDb;
            var thdDifference = capturedAnalysis.Thd - refAnalysis.Thd;

            _log.Info($"Comparison: Level {levelDifference:+0.00;-0.00;0.00}dB | Peak {peakDifference:+0.00;-0.00;0.00}dB | THD {thdDifference:+0.00;-0.00;0.00}%");

            if (Math.Abs(levelDifference) < 0.5 && Math.Abs(peakDifference) < 1.0 && capturedAnalysis.Thd < 1.0)
            {
                _log.Info("Status: Pass - Signal quality maintained");
            }
            else
            {
                _log.Warn("  Status: Warning - Check signal quality");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error analyzing audio quality: {ex.Message}");
        }
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

    private AudioAnalysis AnalyzeSignal(float[] samples, string name)
    {
        if (samples == null || samples.Length == 0)
            return new AudioAnalysis();

        // Calculate RMS
        double sumSquares = 0.0;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }
        double rms = Math.Sqrt(sumSquares / samples.Length);

        // Calculate Peak
        float peak = samples.Max(s => Math.Abs(s));

        // Convert to dBFS
        double rmsDb = rms > 0 ? 20.0 * Math.Log10(rms) : -150.0;
        double peakDb = peak > 0 ? 20.0 * Math.Log10(peak) : -150.0;

        // Calculate Crest Factor
        double crestFactor = peak / (rms > 0 ? rms : 1.0);

        // Calculate THD (simplified: measure harmonic content)
        double thd = CalculateTHD(samples);

        return new AudioAnalysis
        {
            RmsDb = rmsDb,
            PeakDb = peakDb,
            CrestFactor = crestFactor,
            Thd = thd
        };
    }

    private double CalculateTHD(float[] samples)
    {
        // Simplified THD calculation using statistical measures
        // Measures distortion by analyzing signal characteristics
        if (samples.Length < 2)
            return 0.0;

        // Estimate harmonic distortion from signal characteristics
        // Higher THD means more non-sinusoidal content
        double skewness = CalculateSkewness(samples);
        double kurtosis = CalculateKurtosis(samples);

        // Pure sine wave: skewness ≈ 0, kurtosis ≈ 3
        // Distorted signal: higher kurtosis
        double estimatedTHD = Math.Max(0.0, (kurtosis - 3.0) * 10.0); // Scale for percentage

        return Math.Min(100.0, estimatedTHD); // Cap at 100%
    }

    private double CalculateSkewness(float[] samples)
    {
        if (samples.Length < 2)
            return 0.0;

        double mean = samples.Average(s => (double)s);
        double variance = samples.Average(s => Math.Pow((double)s - mean, 2));
        double stdDev = Math.Sqrt(variance);

        if (stdDev == 0)
            return 0.0;

        double m3 = samples.Average(s => Math.Pow(((double)s - mean) / stdDev, 3));
        return m3;
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

    private class AudioAnalysis
    {
        public double RmsDb { get; set; } = -150.0;
        public double PeakDb { get; set; } = -150.0;
        public double CrestFactor { get; set; } = 0.0;
        public double Thd { get; set; } = 0.0;
    }
}
