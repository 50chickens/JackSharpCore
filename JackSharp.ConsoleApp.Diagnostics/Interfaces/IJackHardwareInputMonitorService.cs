namespace JackSharp.ConsoleApp.Diagnostics.Interfaces;

/// <summary>
/// Service for monitoring hardware input levels, similar to jack_meter.
/// Continuously monitors physical inputs and displays their levels.
/// </summary>
public interface IJackHardwareInputMonitorService
{
    /// <summary>
    /// Start monitoring hardware inputs and display levels continuously.
    /// Blocks until StopMonitoring is called or an error occurs.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel monitoring</param>
    /// <returns>Task representing the monitoring operation</returns>
    Task MonitorInputLevelsAsync(CancellationToken cancellationToken);
}
