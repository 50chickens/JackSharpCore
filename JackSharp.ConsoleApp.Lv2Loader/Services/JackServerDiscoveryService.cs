using JackSharp.ConsoleApp.Lv2Loader.Logging;

namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// Implementation of Jack server discovery service
/// </summary>
public class JackServerDiscoveryService(ILog<JackServerDiscoveryService> log) : IJackServerDiscoveryService
{
    private readonly ILog<JackServerDiscoveryService> _log = log;

    /// <summary>
    /// Discovers all available Jack servers
    /// </summary>
    public Task<List<JackServerInfo>> DiscoverServersAsync()
    {
        _log.Info("Discovering available Jack servers...");
        var servers = new List<JackServerInfo>();

        try
        {
            // Start with the default server
            var defaultServer = CheckServer("default");
            if (defaultServer != null)
            {
                servers.Add(defaultServer);
                _log.Info($"Found Jack server: {defaultServer.Name} - Running: {defaultServer.IsRunning}");
            }

            // Try some other common server names
            var commonServers = new[] { "jack", "jackserver", "alsa_pcm", "pipewire" };
            foreach (var serverName in commonServers)
            {
                var server = CheckServer(serverName);
                if (server != null && !servers.Any(s => s.Name == server.Name))
                {
                    servers.Add(server);
                    _log.Info($"Found Jack server: {server.Name} - Running: {server.IsRunning}");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Error during server discovery: {ex.Message}");
        }

        if (servers.Count == 0)
        {
            _log.Warn("No Jack servers found");
        }
        else
        {
            _log.Info($"Discovery complete: found {servers.Count} server(s)");
        }

        return Task.FromResult(servers);
    }

    /// <summary>
    /// Gets information about a specific Jack server
    /// </summary>
    public Task<JackServerInfo?> GetServerInfoAsync(string serverName)
    {
        _log.Info($"Checking Jack server: {serverName}");
        var server = CheckServer(serverName);

        if (server != null)
        {
            _log.Info($"Server '{serverName}' - Running: {server.IsRunning}");
        }
        else
        {
            _log.Warn($"Could not get info for server '{serverName}'");
        }

        return Task.FromResult(server);
    }

    /// <summary>
    /// Finds the first running Jack server
    /// </summary>
    public async Task<JackServerInfo?> FindRunningServerAsync()
    {
        _log.Info("Looking for a running Jack server...");
        var servers = await DiscoverServersAsync();

        var runningServer = servers.FirstOrDefault(s => s.IsRunning);
        if (runningServer != null)
        {
            _log.Info($"Found running Jack server: {runningServer.Name}");
        }
        else
        {
            _log.Warn("No running Jack servers found");
        }

        return runningServer;
    }

    /// <summary>
    /// Checks if a specific server is running by attempting to connect
    /// </summary>
    private JackServerInfo? CheckServer(string serverName)
    {
        var serverInfo = new JackServerInfo { Name = serverName };

        try
        {
            using var tempClient = new JackSharp.Controller($"diag_check_{serverName}");

            if (tempClient.Start(false))
            {
                serverInfo.IsRunning = true;
                serverInfo.SampleRate = tempClient.SampleRate;
                serverInfo.BufferSize = tempClient.BufferSize;
                tempClient.Stop();
            }
            else
            {
                serverInfo.IsRunning = false;
                serverInfo.Error = "Failed to connect";
            }
        }
        catch (Exception ex)
        {
            serverInfo.IsRunning = false;
            serverInfo.Error = ex.Message;
        }

        return serverInfo;
    }
}
