namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Contains Jack connection details passed between services.
/// </summary>
public class JackConnectionDetails
{
    /// <summary>Jack server name</summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>Sample rate in Hz</summary>
    public int SampleRate { get; set; } = 48000;

    /// <summary>Buffer size in frames</summary>
    public int BufferSize { get; set; } = 256;

    /// <summary>Whether connection is active</summary>
    public bool IsConnected { get; set; }

    /// <summary>Connection error message if not connected</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Diagnostics type to run</summary>
    public JackDiagnosticsType DiagnosticsType { get; set; } = JackDiagnosticsType.ServerDiscovery;
}
