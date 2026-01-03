namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// Service interface for loading and managing LV2 plugins.
/// </summary>
public interface ILv2PluginService
{
    /// <summary>
    /// Discover available LV2 plugins.
    /// </summary>
    Task<List<string>> DiscoverPluginsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Load an LV2 plugin by URI.
    /// </summary>
    Task<Lv2PluginInstance?> LoadPluginAsync(string uri, CancellationToken cancellationToken);

    /// <summary>
    /// Check if a plugin exists without loading all plugins first.
    /// </summary>
    Task<bool> TryLoadPluginByUriAsync(string uri, CancellationToken cancellationToken);

    /// <summary>
    /// Get plugin information including ports.
    /// </summary>
    Task<List<Lv2Port>> GetPluginPortsAsync(string uri, CancellationToken cancellationToken);

    /// <summary>
    /// Set a control port value.
    /// </summary>
    void SetControlPort(Lv2PluginInstance plugin, string symbol, float value);

    /// <summary>
    /// Get a control port value.
    /// </summary>
    float GetControlPort(Lv2PluginInstance plugin, string symbol);

    /// <summary>
    /// Unload and cleanup a plugin instance.
    /// </summary>
    void UnloadPlugin(Lv2PluginInstance plugin);
}
