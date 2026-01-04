namespace JackSharp.ConsoleApp.Diagnostics.Interfaces;

/// <summary>
/// Service for continuously monitoring and printing hardware input dBFS levels.
/// Simple output format showing current dBFS for each channel.
/// </summary>
public interface IJackSimpleLevelMeterService
{
    /// <summary>
    /// Monitor and print hardware input levels continuously.
    /// Blocks until StopMonitoring is called or an error occurs.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel monitoring</param>
    /// <returns>Task representing the monitoring operation</returns>
    Task MonitorAndPrintLevelsAsync(CancellationToken cancellationToken);
}
