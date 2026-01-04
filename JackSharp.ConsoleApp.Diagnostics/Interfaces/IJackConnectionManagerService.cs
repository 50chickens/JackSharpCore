namespace JackSharp.ConsoleApp.Diagnostics.Interfaces;

/// <summary>
/// Connection status information for Jack server
/// </summary>
public class JackConnectionStatus
{
    public bool IsConnected { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public int SampleRate { get; set; }
    public int BufferSize { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service for managing Jack server connections
/// </summary>
public interface IJackConnectionManagerService
{
    /// <summary>
    /// Gets the current connection status
    /// </summary>
    Task<JackConnectionStatus> GetStatusAsync();

    /// <summary>
    /// Attempts to connect to Jack server
    /// </summary>
    Task<JackConnectionStatus> ConnectAsync();

    /// <summary>
    /// Disconnects from Jack server
    /// </summary>
    Task<bool> DisconnectAsync();

    /// <summary>
    /// Checks if currently connected to Jack
    /// </summary>
    bool IsConnected { get; }
}
