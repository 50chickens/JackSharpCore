namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Information about a discovered Jack server
/// </summary>
public class JackServerInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int? SampleRate { get; set; }
    public int? BufferSize { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Service for discovering available Jack servers
/// </summary>
public interface IJackServerDiscoveryService
{
    /// <summary>
    /// Discovers all available Jack servers
    /// </summary>
    Task<List<JackServerInfo>> DiscoverServersAsync();

    /// <summary>
    /// Gets information about a specific Jack server
    /// </summary>
    Task<JackServerInfo?> GetServerInfoAsync(string serverName);

    /// <summary>
    /// Finds the first running Jack server
    /// </summary>
    Task<JackServerInfo?> FindRunningServerAsync();
}
