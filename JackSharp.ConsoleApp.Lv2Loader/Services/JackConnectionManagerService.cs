using JackSharp.ConsoleApp.Lv2Loader.Logging;
using Microsoft.Extensions.Options;

namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// Options for Jack connection configuration
/// </summary>
public class JackOptions
{
    public const string SettingsKey = "Jack";
    public string ServerName { get; set; } = "default";
}

/// <summary>
/// Implementation of Jack connection manager
/// </summary>
public class JackConnectionManagerService(
    ILog<JackConnectionManagerService> log,
    IOptions<JackOptions> options,
    IJackServerDiscoveryService discoveryService) : IJackConnectionManagerService, IDisposable
{
    private readonly ILog<JackConnectionManagerService> _log = log;
    private readonly JackOptions _options = options.Value;
    private readonly IJackServerDiscoveryService _discoveryService = discoveryService;
    private JackSharp.Controller? _client;
    private string _connectedServerName = string.Empty;
    private readonly object _lockObject = new();

    public bool IsConnected
    {
        get
        {
            lock (_lockObject)
            {
                return _client?.IsConnectedToJack ?? false;
            }
        }
    }

    /// <summary>
    /// Gets the current connection status
    /// </summary>
    public Task<JackConnectionStatus> GetStatusAsync()
    {
        lock (_lockObject)
        {
            if (_client?.IsConnectedToJack ?? false)
            {
                return Task.FromResult(new JackConnectionStatus
                {
                    IsConnected = true,
                    ServerName = _connectedServerName,
                    SampleRate = _client.SampleRate,
                    BufferSize = _client.BufferSize
                });
            }

            return Task.FromResult(new JackConnectionStatus
            {
                IsConnected = false,
                ServerName = _options.ServerName,
                ErrorMessage = "Not connected to Jack server"
            });
        }
    }

    /// <summary>
    /// Attempts to connect to Jack server
    /// </summary>
    public async Task<JackConnectionStatus> ConnectAsync()
    {
        // Check if already connected
        lock (_lockObject)
        {
            if (_client?.IsConnectedToJack ?? false)
            {
                var status = new JackConnectionStatus
                {
                    IsConnected = true,
                    ServerName = _connectedServerName,
                    SampleRate = _client.SampleRate,
                    BufferSize = _client.BufferSize
                };
                _log.Info("Already connected to Jack server");
                return status;
            }
        }

        // If configured server name is specified, try it first
        if (!string.IsNullOrEmpty(_options.ServerName) && _options.ServerName != "default")
        {
            _log.Info($"Attempting to connect to configured Jack server '{_options.ServerName}'...");
            var status = await TryConnectAsync(_options.ServerName);
            if (status.IsConnected)
                return status;
        }

        // Try to find a running server
        _log.Info("Attempting to discover and connect to a running Jack server...");
        var runningServer = await _discoveryService.FindRunningServerAsync();

        if (runningServer?.IsRunning ?? false)
        {
            return await TryConnectAsync(runningServer.Name);
        }

        // If no running server found, try the configured server as fallback
        _log.Warn("No running Jack server found. Attempting connection to configured server...");
        return await TryConnectAsync(_options.ServerName);
    }

    /// <summary>
    /// Attempts to connect to a specific Jack server
    /// </summary>
    private Task<JackConnectionStatus> TryConnectAsync(string serverName)
    {
        var status = new JackConnectionStatus { ServerName = serverName };

        try
        {
            lock (_lockObject)
            {
                // Create new client
                _client = new JackSharp.Controller("diagnostics");
                _log.Info($"Attempting to connect to Jack server '{serverName}'...");

                if (_client.Start(false))
                {
                    _connectedServerName = serverName;
                    status.IsConnected = true;
                    status.SampleRate = _client.SampleRate;
                    status.BufferSize = _client.BufferSize;
                    _log.Info($"Successfully connected to Jack server '{serverName}' - Sample Rate: {status.SampleRate}Hz, Buffer Size: {status.BufferSize} samples");
                }
                else
                {
                    status.ErrorMessage = $"Failed to connect to Jack server '{serverName}'";
                    _log.Warn($"Failed to connect to Jack server '{serverName}'");
                    _client?.Dispose();
                    _client = null;
                }
            }
        }
        catch (Exception ex)
        {
            status.ErrorMessage = $"Connection error: {ex.Message}";
            _log.Error(ex, $"Error connecting to Jack server '{serverName}'");
            lock (_lockObject)
            {
                _client?.Dispose();
                _client = null;
            }
        }

        return Task.FromResult(status);
    }

    /// <summary>
    /// Disconnects from Jack server
    /// </summary>
    public Task<bool> DisconnectAsync()
    {
        lock (_lockObject)
        {
            try
            {
                if (_client == null)
                {
                    return Task.FromResult(true);
                }

                _client.Stop();
                _client.Dispose();
                _client = null;
                _log.Info("Disconnected from Jack server");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error disconnecting from Jack server");
                return Task.FromResult(false);
            }
        }
    }

    public void Dispose()
    {
        lock (_lockObject)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
