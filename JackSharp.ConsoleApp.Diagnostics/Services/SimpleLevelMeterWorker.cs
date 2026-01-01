using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Worker for simple continuous hardware input level monitoring.
/// Prints dBFS levels for each channel in a simple readable format.
/// </summary>
public class SimpleLevelMeterWorker(
    ILog<SimpleLevelMeterWorker> log,
    IJackSimpleLevelMeterService levelMeterService,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private readonly ILog<SimpleLevelMeterWorker> _log = log;
    private readonly IJackSimpleLevelMeterService _levelMeterService = levelMeterService;
    private readonly IHostApplicationLifetime _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Run the simple level meter
            await _levelMeterService.MonitorAndPrintLevelsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error in simple level meter: {ex.Message}");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
