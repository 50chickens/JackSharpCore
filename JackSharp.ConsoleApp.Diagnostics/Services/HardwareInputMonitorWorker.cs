using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Worker for monitoring hardware input levels continuously.
/// Similar to jack_meter.c - connects to all physical inputs and displays their levels.
/// </summary>
public class HardwareInputMonitorWorker(
    ILog<HardwareInputMonitorWorker> log,
    IJackHardwareInputMonitorService inputMonitorService,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private readonly ILog<HardwareInputMonitorWorker> _log = log;
    private readonly IJackHardwareInputMonitorService _inputMonitorService = inputMonitorService;
    private readonly IHostApplicationLifetime _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Run the input level monitor
            await _inputMonitorService.MonitorInputLevelsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error in hardware input monitor: {ex.Message}");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
