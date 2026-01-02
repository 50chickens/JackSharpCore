using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.Processing;
using System.Threading;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Measures audio input levels from Jack server, similar to SNRReduction pattern.
/// Works with loopback cable: outputs → inputs for verification.
/// </summary>
public interface IJackAudioLevelMeterService
{
    /// <summary>
    /// Measure input levels for specified duration.
    /// Returns (ChannelDbfs, ChannelRms) tuples for each channel.
    /// </summary>
    (List<double> ChannelDbfs, List<double> ChannelRms) MeasureInputLevels(
        int captureDurationMs = 3000,
        int numChannels = 2);
}

/// <summary>
/// Jack-based audio level meter that captures input from Jack ports and calculates dBFS.
/// Uses the same measurement pattern as Example.SNRReduction but with Jack PCM data.
/// </summary>
public class JackAudioLevelMeterService(ILog<JackAudioLevelMeterService> log) : IJackAudioLevelMeterService
{
    private const double NoiseFloor = -150.0; // silence is ~90 dBFS, use -150 dBFS as noise floor
    private readonly ILog<JackAudioLevelMeterService> _log = log ?? throw new ArgumentNullException(nameof(log));

    public (List<double> ChannelDbfs, List<double> ChannelRms) MeasureInputLevels(
        int captureDurationMs = 3000,
        int numChannels = 2)
    {
        try
        {
            _log.Info($"Connecting to Jack to measure {numChannels} input channels for {captureDurationMs}ms...");

            // Create a processor with input ports and autoconnect to physical inputs
            using var processor = new JackSharp.Processor("AudioLevelMeter", numChannels, 0, 0, 0, true);
            
            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return (new List<double>(), new List<double>());
            }

            _log.Info($"Connected to Jack at {processor.SampleRate}Hz, buffer size: {processor.BufferSize}");
            _log.Info($"Created input ports: AudioLevelMeter:in_1, AudioLevelMeter:in_2 (autoconnect enabled)");

            // Accumulator for input levels
            var accumulator = new AudioAccumulator(numChannels);
            var startTime = DateTime.UtcNow;
            var captureDuration = TimeSpan.FromMilliseconds(captureDurationMs);
            var isCapturing = true;
            long totalFrames = 0;

            // Set up process callback to accumulate input data
            processor.ProcessFunc = buffer =>
            {
                if (isCapturing && (DateTime.UtcNow - startTime) < captureDuration)
                {
                    // Read audio from input ports
                    for (int ch = 0; ch < buffer.AudioIn.Length && ch < numChannels; ch++)
                    {
                        var audioBuffer = buffer.AudioIn[ch];
                        if (audioBuffer?.Audio != null && audioBuffer.Audio.Length > 0)
                        {
                            accumulator.AddSamples(ch, audioBuffer.Audio);
                        }
                    }
                    totalFrames += buffer.Frames;
                }
            };

            // Wait for the specified capture duration
            Task.Delay(captureDurationMs).Wait();
            isCapturing = false;

            _log.Info($"Captured {totalFrames} frames of audio data");

            // Calculate levels
            var (dbfsList, rmsList) = accumulator.CalculateLevels();

            _log.Info($"Captured audio data, calculated levels");

            // Log results
            for (int ch = 0; ch < dbfsList.Count; ch++)
            {
                _log.Info($"Channel {ch + 1}: {dbfsList[ch]:F2} dBFS (RMS: {rmsList[ch]:F4})");
            }

            return (dbfsList, rmsList);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error measuring input levels: {ex.Message}");
            return (new List<double>(), new List<double>());
        }
    }

    private class AudioAccumulator
    {
        private readonly List<long> _sumSq; // Sum of squares per channel
        private long _sampleCount; // Samples per channel
        private const int BitsPerSample = 32; // Jack uses 32-bit floats

        public AudioAccumulator(int channelCount)
        {
            _sumSq = Enumerable.Repeat(0L, channelCount).ToList();
            _sampleCount = 0;
        }

        public void AddSamples(int channel, float[] samples)
        {
            if (channel >= _sumSq.Count || samples == null || samples.Length == 0)
                return;

            foreach (var sample in samples)
            {
                // Convert float (-1.0 to 1.0) to fixed point representation
                // Jack uses 32-bit floats in range [-1.0, 1.0]
                long fixedPoint = (long)(sample * 2147483647.0); // max int32
                _sumSq[channel] += fixedPoint * fixedPoint;
            }

            _sampleCount += samples.Length;
        }

        public (List<double> DbfsList, List<double> RmsList) CalculateLevels()
        {
            var dbfsList = new List<double>();
            var rmsList = new List<double>();

            if (_sampleCount == 0)
            {
                // No data captured
                for (int i = 0; i < _sumSq.Count; i++)
                {
                    dbfsList.Add(NoiseFloor);
                    rmsList.Add(0.0);
                }
                return (dbfsList, rmsList);
            }

            double maxAmp = Math.Pow(2.0, 31) - 1.0; // Max for 32-bit signed

            for (int ch = 0; ch < _sumSq.Count; ch++)
            {
                double rms = Math.Sqrt((double)_sumSq[ch] / _sampleCount) / maxAmp;
                rmsList.Add(rms);
                
                // Check for NaN (occurs when _sumSq[ch] is 0)
                if (double.IsNaN(rms) || rms <= 0)
                {
                    dbfsList.Add(NoiseFloor);
                }
                else
                {
                    dbfsList.Add(20.0 * Math.Log10(rms));
                }
            }

            return (dbfsList, rmsList);
        }
    }
}

