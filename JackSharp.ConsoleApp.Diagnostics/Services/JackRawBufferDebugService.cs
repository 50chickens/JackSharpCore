using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.Processing;
using JackSharp.Ports;
using System.Runtime.InteropServices;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service for deep inspection of Jack audio buffers at the raw level.
/// Provides detailed debugging information about buffer contents, pointers, and data flow.
/// </summary>
public class JackRawBufferDebugService(ILog<JackRawBufferDebugService> log) : IJackRawBufferDebugService
{
    private readonly ILog<JackRawBufferDebugService> _log = log;
    private int _callbackCount = 0;
    private const int MaxCallbacksToLog = 10;

    public async Task InspectBuffersAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Info("=== Raw Buffer Debug Tool ===");
            _log.Info("Connecting to Jack to inspect audio buffers at the lowest level...\n");

            // Create processor with 2 input ports for stereo debugging
            const int debugChannels = 2;
            using var processor = new Processor("BufferDebugger", audioInPorts: debugChannels, autoconnect: true);

            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return;
            }

            _log.Info($"✓ Connected to Jack at {processor.SampleRate}Hz, buffer size: {processor.BufferSize}");
            _log.Info($"✓ Created {debugChannels} input ports (autoconnected to system capture)\n");

            // Log port connections
            var ports = processor.AudioInPorts.ToList();
            for (int i = 0; i < ports.Count; i++)
            {
                var portName = ports[i].Name;
                var connectedPort = PortConnectionHelper.GetUpstreamConnectedPortName(ports[i]);
                _log.Info($"  Port {i + 1}: {portName} <- {connectedPort}");
            }
            _log.Info("");

            var bufferStats = new BufferStatistics[debugChannels];
            for (int i = 0; i < debugChannels; i++)
            {
                bufferStats[i] = new BufferStatistics();
            }

            // Set up process callback with detailed inspection
            processor.ProcessFunc = buffer =>
            {
                _callbackCount++;

                // Only log detailed info for first few callbacks to avoid spam
                bool shouldLogDetails = _callbackCount <= MaxCallbacksToLog;

                if (shouldLogDetails)
                {
                    _log.Info($"--- Callback #{_callbackCount} (Frame count: {buffer.Frames}) ---");
                }

                // Inspect each input channel
                for (int ch = 0; ch < buffer.AudioIn.Length && ch < debugChannels; ch++)
                {
                    var audioBuffer = buffer.AudioIn[ch];
                    
                    if (audioBuffer == null)
                    {
                        if (shouldLogDetails)
                            _log.Warn($"  Channel {ch + 1}: AudioBuffer is NULL!");
                        continue;
                    }

                    if (shouldLogDetails)
                    {
                        _log.Info($"  Channel {ch + 1} ({audioBuffer.Port.Name}):");
                        _log.Info($"    Buffer Size: {audioBuffer.BufferSize}");
                        _log.Info($"    Audio Array: {(audioBuffer.Audio == null ? "NULL" : $"Length={audioBuffer.Audio.Length}")}");
                    }

                    if (audioBuffer.Audio != null)
                    {
                        var samples = audioBuffer.Audio;
                        var stats = bufferStats[ch];

                        // Analyze the buffer
                        stats.TotalSamples += samples.Length;
                        stats.BufferCount++;

                        float min = float.MaxValue;
                        float max = float.MinValue;
                        double sumSquares = 0.0;
                        int nonZeroCount = 0;
                        int zeroCount = 0;

                        for (int i = 0; i < samples.Length; i++)
                        {
                            float sample = samples[i];
                            
                            if (sample != 0.0f)
                            {
                                nonZeroCount++;
                                stats.TotalNonZeroSamples++;
                            }
                            else
                            {
                                zeroCount++;
                            }

                            if (sample < min) min = sample;
                            if (sample > max) max = sample;
                            sumSquares += sample * sample;
                        }

                        double rms = Math.Sqrt(sumSquares / samples.Length);
                        double dbFS = rms > 0 ? 20.0 * Math.Log10(rms) : -100.0;

                        stats.UpdatePeak(max);
                        stats.UpdatePeak(Math.Abs(min));

                        if (shouldLogDetails)
                        {
                            _log.Info($"    Sample Stats:");
                            _log.Info($"      - Non-zero samples: {nonZeroCount}/{samples.Length} ({100.0 * nonZeroCount / samples.Length:F2}%)");
                            _log.Info($"      - Zero samples: {zeroCount}/{samples.Length}");
                            _log.Info($"      - Range: [{min:F6}, {max:F6}]");
                            _log.Info($"      - RMS: {rms:F6}");
                            _log.Info($"      - Level: {dbFS:F1} dBFS");

                            // Show first few sample values
                            if (samples.Length > 0)
                            {
                                var preview = string.Join(", ", samples.Take(8).Select(s => $"{s:F6}"));
                                _log.Info($"      - First 8 samples: [{preview}]");
                            }
                        }
                    }
                }

                if (shouldLogDetails)
                {
                    _log.Info("");
                }
                else if (_callbackCount == MaxCallbacksToLog + 1)
                {
                    _log.Info($"(Detailed logging stopped after {MaxCallbacksToLog} callbacks, continuing silent monitoring...)\n");
                }
            };

            // Monitor for a few seconds
            _log.Info("Monitoring for 5 seconds...\n");
            await Task.Delay(5000, cancellationToken);

            // Print summary statistics
            _log.Info("\n=== SUMMARY STATISTICS ===");
            _log.Info($"Total process callbacks: {_callbackCount}");
            
            for (int ch = 0; ch < debugChannels; ch++)
            {
                var stats = bufferStats[ch];
                var portName = ports[ch].Name;
                
                _log.Info($"\nChannel {ch + 1} ({portName}):");
                _log.Info($"  Total buffers processed: {stats.BufferCount}");
                _log.Info($"  Total samples: {stats.TotalSamples:N0}");
                _log.Info($"  Non-zero samples: {stats.TotalNonZeroSamples:N0} ({100.0 * stats.TotalNonZeroSamples / Math.Max(1, stats.TotalSamples):F2}%)");
                _log.Info($"  Peak amplitude: {stats.PeakAmplitude:F6}");
                
                if (stats.TotalNonZeroSamples == 0)
                {
                    _log.Warn($"  ⚠ WARNING: ALL SAMPLES WERE ZERO - No audio detected!");
                }
                else if (stats.TotalNonZeroSamples < stats.TotalSamples * 0.01)
                {
                    _log.Warn($"  ⚠ WARNING: Very few non-zero samples - Audio may be very quiet or intermittent");
                }
                else
                {
                    _log.Info($"  ✓ Audio data detected");
                }
            }

            _log.Info("\n=== DIAGNOSIS ===");
            bool allChannelsSilent = bufferStats.All(s => s.TotalNonZeroSamples == 0);
            
            if (allChannelsSilent)
            {
                _log.Warn("All channels are completely silent (all zeros).");
                _log.Warn("Possible causes:");
                _log.Warn("  1. No physical audio input connected");
                _log.Warn("  2. Input gain/volume set too low (check: amixer)");
                _log.Warn("  3. Wrong input source selected in ALSA mixer");
                _log.Warn("  4. Audio interface not receiving power/signal");
                _log.Warn("  5. Jack/ALSA driver issue");
            }
            else
            {
                _log.Info("✓ Audio signal detected - buffers contain non-zero data");
                _log.Info("The Jack audio pipeline is working correctly.");
            }

            _log.Info("\n=== Buffer Inspection Complete ===");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error during buffer inspection: {ex.Message}");
        }
    }

    private class BufferStatistics
    {
        public long TotalSamples { get; set; }
        public long TotalNonZeroSamples { get; set; }
        public int BufferCount { get; set; }
        public float PeakAmplitude { get; private set; }

        public void UpdatePeak(float value)
        {
            if (Math.Abs(value) > PeakAmplitude)
            {
                PeakAmplitude = Math.Abs(value);
            }
        }
    }
}
