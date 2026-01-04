using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics.Workers;

/// <summary>
/// Worker for audio quality analysis via command-line option.
/// Runs the audio quality analysis and exits.
/// </summary>
public class AudioQualityAnalysisWorker(
    IHostApplicationLifetime lifetime,
    AudioQualityAnalysisService analysisService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Run analysis with 1kHz sine wave for 5 seconds
            await analysisService.AnalyzeAudioQualityAsync(1000.0, 5, stoppingToken);

            // Stop the application
            lifetime.StopApplication();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            lifetime.StopApplication();
        }
    }
}
