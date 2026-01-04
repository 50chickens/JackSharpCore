using JackSharp.ConsoleApp.Diagnostics.Interfaces;
using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Options;
using JackSharp.Ports;
using Microsoft.Extensions.Options;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Simple service for monitoring and printing hardware input dBFS levels.
/// Displays continuous audio levels in a readable format similar to jack_meter output.
/// </summary>
public class JackSimpleLevelMeterService(ILog<JackSimpleLevelMeterService> log, IOptions<SimpleLevelMeterOptions> options) : IJackSimpleLevelMeterService
{
    private readonly ILog<JackSimpleLevelMeterService> _log = log;
    private readonly SimpleLevelMeterOptions _options = options?.Value ?? new SimpleLevelMeterOptions();
    private const float UpdateRateHz = 4.0f; // Update 4 times per second for readable output
    private const int MaxDisplayChannels = 8;

    public async Task MonitorAndPrintLevelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("Hardware input level meter (dBFS)");
            _log.Info($"Monitoring hardware inputs ({_options.MeasurementDuration}s measurements, {_options.MeasurementCount} readings)...\n");

            // Create processor with multiple input ports and autoconnect to physical inputs
            const int maxInputChannels = 16;
            using var processor = new Processor("SimpleLevelMeter", maxInputChannels, 0, 0, 0, true);

            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return;
            }

            _log.Info($"Connected to Jack at {processor.SampleRate}Hz, buffer size: {processor.BufferSize}");
            _log.Info($"Created {maxInputChannels} input ports\n");

            // Create accumulators for each channel
            var accumulators = Enumerable.Range(0, maxInputChannels)
                .Select(_ => new SimpleChannelAccumulator())
                .ToList();

            // Get the connected port names for each channel
            var portNames = GetConnectedPortNames(processor, maxInputChannels);

            var lastDisplayTime = DateTime.UtcNow;
            var updateInterval = TimeSpan.FromMilliseconds(1000.0 / UpdateRateHz);
            var measurementStartTime = DateTime.UtcNow;
            var measurementDuration = TimeSpan.FromSeconds(_options.MeasurementDuration);
            var readingCount = 0;
            var completedMeasurements = false;

            // Set up process callback to accumulate input data
            processor.ProcessFunc = buffer =>
            {
                // Don't process if we're done
                if (completedMeasurements)
                    return;

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
                    // Check if measurement period is complete
                    if ((now - measurementStartTime) >= measurementDuration)
                    {
                        PrintLevels(accumulators, portNames);
                        readingCount++;

                        // Stop after specified number of readings
                        if (readingCount >= _options.MeasurementCount)
                        {
                            _log.Info($"Completed {readingCount} measurements");
                            completedMeasurements = true;
                            return;
                        }

                        // Reset for next measurement
                        measurementStartTime = now;
                        foreach (var acc in accumulators)
                        {
                            acc.Reset();
                        }
                    }

                    lastDisplayTime = now;
                }
            };

            // Monitor until completion or cancellation requested
            try
            {
                // Wait until measurements are complete or cancellation is requested
                while (!completedMeasurements && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _log.Info("Monitoring stopped by user");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error monitoring levels: {ex.Message}");
        }
    }

    private void PrintLevels(List<SimpleChannelAccumulator> accumulators, List<string> portNames)
    {
        // Print header
        var output = "Time: " + DateTime.Now.ToString("HH:mm:ss") + " | ";

        // Display only CONNECTED channels (skip unconnected ones)
        int displayCount = 0;
        for (int ch = 0; ch < accumulators.Count && displayCount < MaxDisplayChannels; ch++)
        {
            var portName = ch < portNames.Count ? portNames[ch] : $"CH{ch + 1}";

            // Skip unconnected channels from output
            if (portName.StartsWith("(unconnected:"))
                continue;

            var db = accumulators[ch].CalculateDb();
            output += $"{portName}: {db:F1}dBFS | ";
            displayCount++;
        }

        // Print on new line to preserve history
        Console.WriteLine(output);
    }

    /// <summary>
    /// Simple accumulator that calculates RMS level in dBFS
    /// </summary>
    private class SimpleChannelAccumulator
    {
        private double _sumSquares = 0.0;
        private long _sampleCount = 0;
        private const double NoiseFloor = -150.0;

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

    private List<string> GetConnectedPortNames(Processor processor, int channelCount)
    {
        var portNames = new List<string>();

        try
        {
            var audioInPorts = processor.AudioInPorts.ToList();

            for (int i = 0; i < channelCount && i < audioInPorts.Count; i++)
            {
                var port = audioInPorts[i];
                string connectedPortName = PortConnectionHelper.GetUpstreamConnectedPortName(port);

                if (!string.IsNullOrEmpty(connectedPortName))
                {
                    portNames.Add(connectedPortName);
                    _log.Info($"  Input {i + 1}: {port.Name} <- {connectedPortName}");
                }
                else
                {
                    // Not connected
                    portNames.Add($"(unconnected:{port.Name})");
                    _log.Warn($"  Input {i + 1}: {port.Name} <- NOT CONNECTED");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error getting connected port names");
            // Fall back to port names
            for (int i = 0; i < channelCount; i++)
            {
                portNames.Add($"in_{i + 1}");
            }
        }

        return portNames;
    }
}
