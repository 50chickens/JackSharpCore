using JackSharp.ConsoleApp.Diagnostics.Logging;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service to generate and play test tones through Jack audio outputs.
/// Supports sine wave generation at specified frequency and amplitude.
/// </summary>
public interface IJackTestToneService
{
    /// <summary>
    /// Generate and play a sine wave test tone on the output ports.
    /// </summary>
    void PlayTestTone(
        int frequencyHz = 1000,
        double amplitudeDbfs = -6.0,
        int durationMs = 5000,
        int? leftChannelDurationMs = null,
        int? rightChannelDurationMs = null);
}

/// <summary>
/// Jack-based test tone generator using sine wave synthesis.
/// Similar pattern to SNRReduction TestToneService but using Jack audio output.
/// </summary>
public class JackTestToneService(ILog<JackTestToneService> log) : IJackTestToneService
{
    private readonly ILog<JackTestToneService> _log = log ?? throw new ArgumentNullException(nameof(log));

    public void PlayTestTone(
        int frequencyHz = 1000,
        double amplitudeDbfs = -6.0,
        int durationMs = 5000,
        int? leftChannelDurationMs = null,
        int? rightChannelDurationMs = null)
    {
        try
        {
            _log.Info($"Generating test tone: {frequencyHz}Hz @ {amplitudeDbfs}dBFS for {durationMs}ms");

            using var processor = new JackSharp.Processor("TestToneGenerator", audioOutPorts: 2, autoconnect: true);
            if (!processor.Start())
            {
                _log.Error("Failed to connect to Jack");
                return;
            }

            _log.Info($"Connected to Jack at {processor.SampleRate}Hz, buffer size: {processor.BufferSize}");

            // Convert dBFS to linear amplitude (0.0 to 1.0)
            double amplitude = Math.Pow(10.0, amplitudeDbfs / 20.0);
            _log.Info($"Tone amplitude: {amplitude:F4} linear ({amplitudeDbfs}dBFS)");

            int sampleRate = processor.SampleRate;
            int totalSamples = (int)(sampleRate * durationMs / 1000.0);
            double phaseIncrement = 2.0 * Math.PI * frequencyHz / sampleRate;

            _log.Info($"Sample rate: {sampleRate}Hz, Total samples: {totalSamples}, Phase increment: {phaseIncrement:F6}");

            var phase = new[] { 0.0, 0.0 }; // One phase per channel
            long totalWritten = 0;
            var startTime = DateTime.UtcNow;

            // Set up process callback to generate and write tone
            processor.ProcessFunc = buffer =>
            {
                if ((DateTime.UtcNow - startTime).TotalMilliseconds < durationMs && totalWritten < totalSamples)
                {
                    for (int ch = 0; ch < buffer.AudioOut.Length && ch < phase.Length; ch++)
                    {
                        var audioBuffer = buffer.AudioOut[ch];
                        var samples = audioBuffer.Audio;
                        
                        for (int i = 0; i < samples.Length; i++)
                        {
                            samples[i] = (float)(amplitude * Math.Sin(phase[ch]));
                            phase[ch] += phaseIncrement;
                            if (phase[ch] > 2.0 * Math.PI)
                                phase[ch] -= 2.0 * Math.PI;
                        }
                    }
                    
                    Interlocked.Add(ref totalWritten, buffer.Frames);
                }
            };

            // Wait for tone generation to complete
            Task.Delay(durationMs + 500).Wait();

            _log.Info($"Test tone generation complete. Generated {totalWritten} samples in {(DateTime.UtcNow - startTime).TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error generating test tone: {ex.Message}");
        }
    }
}
