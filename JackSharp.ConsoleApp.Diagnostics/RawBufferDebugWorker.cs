using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Worker service for running raw buffer debugging.
/// </summary>
public class RawBufferDebugWorker(
    ILog<RawBufferDebugWorker> log,
    IJackRawBufferDebugService debugService,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private readonly ILog<RawBufferDebugWorker> _log = log;
    private readonly IJackRawBufferDebugService _debugService = debugService;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _debugService.InspectBuffersAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during raw buffer debugging");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
