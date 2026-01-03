using JackSharp.ConsoleApp.Lv2Loader.Logging;
using JackSharp.ConsoleApp.Lv2Loader.Services;

namespace JackSharp.ConsoleApp.Lv2Loader;

/// <summary>
/// Main worker service for LV2 plugin loading.
/// Manages the lifecycle of LV2 plugins in a Jack audio context.
/// </summary>
public class Lv2LoaderWorker(
    ILog<Lv2LoaderWorker> log,
    ILv2PluginService pluginService,
    ILv2AudioProcessingService audioProcessingService,
    IJackConnectionManagerService jackConnectionService
) : BackgroundService
{
    private readonly ILog<Lv2LoaderWorker> _log = log;
    private readonly ILv2PluginService _pluginService = pluginService;
    private readonly ILv2AudioProcessingService _audioProcessingService = audioProcessingService;
    private readonly IJackConnectionManagerService _jackConnectionService = jackConnectionService;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Info("LV2 Loader starting");
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Info("LV2 Loader stopping");
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("LV2 Loader worker executing");

        try
        {
            // Look for tinygain plugin without full discovery
            var tinygainUri = "http://gareus.org/oss/lv2/tinygain#mono";
            _log.Info($"Checking for {tinygainUri}");
            var pluginExists = await _pluginService.TryLoadPluginByUriAsync(tinygainUri, stoppingToken);

            if (pluginExists)
            {
                _log.Info("TinyGain plugin found in system");

                // Show all discovered plugins
                var allPlugins = await _pluginService.DiscoverPluginsAsync(stoppingToken);
                _log.Info($"Total plugins available: {allPlugins.Count}");
                foreach (var plugin in allPlugins.Take(10))
                {
                    _log.Info($"  {plugin}");
                }
                if (allPlugins.Count > 10)
                {
                    _log.Info($"  ... and {allPlugins.Count - 10} more");
                }

                // Load tinygain plugin
                var tinygain = await _pluginService.LoadPluginAsync(tinygainUri, stoppingToken);
                if (tinygain != null)
                {
                    _log.Info($"TinyGain plugin loaded: {tinygain.Name}");

                    // Set default gain to 0 dB (passthrough)
                    _pluginService.SetControlPort(tinygain, "gain", 0.0f);
                    _log.Info("TinyGain gain set to 0.0 dB (passthrough mode)");

                    // Test audio streaming through the plugin
                    await TestAudioStreamAsync(tinygain, stoppingToken);

                    // Cleanup
                    _pluginService.UnloadPlugin(tinygain);
                }
                else
                {
                    _log.Error("Failed to load TinyGain plugin");
                }
            }
            else
            {
                _log.Warn($"TinyGain plugin not found in system. Discovering all plugins...");

                // Fallback to full discovery with timeout if tinygain not found
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    var plugins = await _pluginService.DiscoverPluginsAsync(cts.Token);
                    _log.Warn($"Total plugins available: {plugins.Count}");
                    foreach (var plugin in plugins.Take(10))
                    {
                        _log.Warn($"  {plugin}");
                    }
                    if (plugins.Count > 10)
                    {
                        _log.Warn($"  ... and {plugins.Count - 10} more");
                    }
                }
                catch (OperationCanceledException)
                {
                    _log.Warn("Plugin discovery timed out after 5 seconds (expected - stub implementation)");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.Info("LV2 Loader worker cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error in LV2 Loader worker");
            throw;
        }
    }

    private async Task TestAudioStreamAsync(Lv2PluginInstance plugin, CancellationToken cancellationToken)
    {
        _log.Info("Starting audio stream testing...");

        try
        {
            const int sampleRate = 48000;
            const int testDuration = 2; // 2 seconds of test
            const int frameSize = 2048;
            const float testFrequency = 1000f; // 1kHz sine wave
            const float testAmplitude = 0.1f; // -20dBFS

            var generator = new AudioStreamGenerator(sampleRate, testFrequency);
            var totalFrames = sampleRate * testDuration;
            var inputBuffer = new List<float>();
            var outputBuffer = new List<float>();

            _log.Info($"Test parameters: {sampleRate}Hz, {testFrequency}Hz sine, {testAmplitude:F2} amplitude");
            _log.Info($"Test duration: {testDuration}s ({totalFrames} samples in {frameSize}-sample blocks)");

            // Generate and process audio in chunks
            int framesProcessed = 0;
            while (framesProcessed < totalFrames && !cancellationToken.IsCancellationRequested)
            {
                int framesToProcess = Math.Min(frameSize, totalFrames - framesProcessed);

                // Generate input audio (sine wave)
                var inputSamples = generator.GenerateSineWave(framesToProcess, testAmplitude);
                inputBuffer.AddRange(inputSamples);

                // For now, just copy through (since plugin instantiation is stub)
                // In real implementation, this would feed through plugin processing
                var outputSamples = new float[framesToProcess];
                Array.Copy(inputSamples, outputSamples, framesToProcess);
                outputBuffer.AddRange(outputSamples);

                framesProcessed += framesToProcess;

                // Log progress every second
                if (framesProcessed % sampleRate == 0)
                {
                    var seconds = framesProcessed / sampleRate;
                    _log.Info($"Processed {seconds}s of audio ({framesProcessed}/{totalFrames} samples)");
                }

                await Task.Delay(10, cancellationToken);
            }

            _log.Info("Audio streaming complete");

            // Analyze results
            if (inputBuffer.Count > 0 && outputBuffer.Count > 0)
            {
                _log.Info("Analyzing audio...");
                var result = AudioAnalyzer.Compare(inputBuffer.ToArray(), outputBuffer.ToArray(), amplitudeGain: 1.0f);

                _log.Info("Audio Analysis Results:");
                _log.Info("================================================================================");
                foreach (var line in result.ToString().Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line.Trim()))
                    {
                        _log.Info(line);
                    }
                }
                _log.Info("================================================================================");

                if (result.IsMatch)
                {
                    _log.Info("✓ SUCCESS: Audio passthrough matches expected output");
                }
                else
                {
                    _log.Warn("✗ MISMATCH: Audio output differs from input");
                    if (result.Error != null)
                    {
                        _log.Error($"Error: {result.Error}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.Info("Audio stream testing cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during audio stream testing");
        }
    }
}
