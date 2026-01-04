using JackSharp.ConsoleApp.Diagnostics.Interfaces;
using JackSharp.ConsoleApp.Diagnostics.Logging;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Monitors hardware input levels continuously, similar to jack_meter.c.
/// Connects to all physical hardware inputs and displays their audio levels.
/// </summary>
public class JackHardwareInputMonitorService(ILog<JackHardwareInputMonitorService> log) : IJackHardwareInputMonitorService
{
    private readonly ILog<JackHardwareInputMonitorService> _log = log;
    private const float UpdateRateHz = 8.0f;
    private const int MeterWidth = 79;

    public async Task MonitorInputLevelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Monitoring hardware input levels");
            _log.Info("Connecting to Jack and monitoring physical inputs...");

            // Create processor with multiple input ports and autoconnect to physical inputs
            // Typical systems have 2-8 hardware inputs
            const int maxInputChannels = 16;
            using var processor = new Processor("HWInputMonitor", maxInputChannels, 0, 0, 0, true);

            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return;
            }

            _log.Info($"Connected to Jack at {processor.SampleRate}Hz, buffer size: {processor.BufferSize}");
            _log.Info($"Created {maxInputChannels} input ports for monitoring\n");

            // Display meter scale
            DisplayScale(MeterWidth);
            Console.WriteLine();

            // Create accumulators for each channel
            var accumulators = Enumerable.Range(0, maxInputChannels)
                .Select(_ => new ChannelAccumulator())
                .ToList();

            var lastDisplayTime = DateTime.UtcNow;
            var updateInterval = TimeSpan.FromMilliseconds(1000.0 / UpdateRateHz);

            // Set up process callback to accumulate input data
            processor.ProcessFunc = buffer =>
            {
                // Accumulate samples from input ports
                for (int ch = 0; ch < buffer.AudioIn.Length && ch < accumulators.Count; ch++)
                {
                    var audioBuffer = buffer.AudioIn[ch];
                    if (audioBuffer?.Audio != null && audioBuffer.Audio.Length > 0)
                    {
                        accumulators[ch].AddSamples(audioBuffer.Audio);
                    }
                }

                // Update display at specified rate
                var now = DateTime.UtcNow;
                if ((now - lastDisplayTime) >= updateInterval)
                {
                    DisplayInputLevels(accumulators);
                    lastDisplayTime = now;

                    // Reset accumulators for next measurement period
                    foreach (var acc in accumulators)
                    {
                        acc.Reset();
                    }
                }
            };

            // Monitor until cancellation requested
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _log.Info("Monitoring stopped");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error monitoring hardware inputs: {ex.Message}");
        }
    }

    private void DisplayScale(int width)
    {
        var marks = new[] { 0, -5, -10, -15, -20, -25, -30, -35, -40, -50, -60 };
        var scale = new char[width];
        var line = new char[width];

        // Initialize
        for (int i = 0; i < width; i++)
        {
            scale[i] = ' ';
            line[i] = '_';
        }

        // Draw marks at dB positions
        foreach (var db in marks)
        {
            var pos = IecScale(db, width) - 1;
            if (pos >= 0 && pos < width)
            {
                line[pos] = '|';

                var mark = db.ToString();
                int spos = pos - (mark.Length / 2);
                if (spos < 0) spos = 0;
                if (spos + mark.Length > width) spos = width - mark.Length;

                for (int i = 0; i < mark.Length && spos + i < width; i++)
                {
                    scale[spos + i] = mark[i];
                }
            }
        }

        Console.WriteLine(new string(scale));
        Console.WriteLine(new string(line));
    }

    private void DisplayInputLevels(List<ChannelAccumulator> accumulators)
    {
        // Display first 4 channels regardless of level, or more if they have signal
        int displayCount = 0;
        int maxChannelsToShow = 4;
        var output = "";

        for (int ch = 0; ch < accumulators.Count && displayCount < maxChannelsToShow; ch++)
        {
            var db = accumulators[ch].CalculateDb();
            var meterSize = IecScale(db, MeterWidth);

            output += $"CH{ch + 1:D2} ";

            for (int i = 0; i < meterSize - 1; i++)
                output += '#';

            if (meterSize > 0)
                output += 'I';

            for (int i = meterSize; i < MeterWidth; i++)
                output += ' ';

            output += $" {db:F1}dB  |  ";
            displayCount++;
        }

        // Print on new line to preserve history
        Console.WriteLine(output);
    }

    /// <summary>
    /// IEC 60268-18 scale conversion
    /// Maps dB values to meter deflection percentage
    /// </summary>
    private static int IecScale(double db, int size)
    {
        double def = db switch
        {
            < -70.0 => 0.0,
            < -60.0 => (db + 70.0) * 0.25,
            < -50.0 => (db + 60.0) * 0.5 + 2.5,
            < -40.0 => (db + 50.0) * 0.75 + 7.5,
            < -30.0 => (db + 40.0) * 1.5 + 15.0,
            < -20.0 => (db + 30.0) * 2.0 + 30.0,
            < 0.0 => (db + 20.0) * 2.5 + 50.0,
            _ => 100.0
        };

        return (int)((def / 100.0) * size);
    }

    /// <summary>
    /// Accumulates audio samples and calculates RMS level in dBFS
    /// </summary>
    private class ChannelAccumulator
    {
        private double _sumSquares = 0.0;
        private long _sampleCount = 0;
        private const double NoiseFloor = -80.0;

        public void AddSamples(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return;

            foreach (var sample in samples)
            {
                _sumSquares += sample * sample;
                _sampleCount++;
            }
        }

        public double CalculateDb()
        {
            if (_sampleCount == 0)
                return NoiseFloor;

            double rms = Math.Sqrt(_sumSquares / _sampleCount);

            if (rms <= 0 || double.IsNaN(rms))
                return NoiseFloor;

            return 20.0 * Math.Log10(rms);
        }

        public void Reset()
        {
            _sumSquares = 0.0;
            _sampleCount = 0;
        }
    }
}
