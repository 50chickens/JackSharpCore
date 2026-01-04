namespace JackSharp.ConsoleApp.Diagnostics.Interfaces;

/// <summary>
/// Service for deep inspection of Jack audio buffers at the raw level.
/// </summary>
public interface IJackRawBufferDebugService
{
    /// <summary>
    /// Runs detailed buffer inspection to debug audio data flow.
    /// </summary>
    Task InspectBuffersAsync(CancellationToken cancellationToken);
}
