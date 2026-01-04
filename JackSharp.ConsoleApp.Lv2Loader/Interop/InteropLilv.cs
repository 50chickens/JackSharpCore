using JackSharp.ConsoleApp.Lv2Loader.Logging;
using System.Runtime.InteropServices;

namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// P/Invoke declarations for lilv library (LV2 plugin discovery and instantiation).
/// </summary>
internal static class InteropLilv
{
    private const string LilvLibName = "lilv-0";

    // World management
    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_world_new();

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void lilv_world_free(IntPtr world);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void lilv_world_load_all(IntPtr world);

    // Plugin set
    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_world_get_all_plugins(IntPtr world);

    // Plugin iteration
    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugins_begin(IntPtr plugins);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugins_get(IntPtr plugins, IntPtr iterator);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugins_next(IntPtr plugins, IntPtr iterator);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool lilv_plugins_is_end(IntPtr plugins, IntPtr iterator);

    // Plugin properties
    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugin_get_uri(IntPtr plugin);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugin_get_name(IntPtr plugin);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugin_get_library_uri(IntPtr plugin);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint lilv_plugin_get_num_ports(IntPtr plugin);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_plugin_get_port_by_index(IntPtr plugin, uint index);

    // Port properties
    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_port_get_symbol(IntPtr port);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_port_get_name(IntPtr port);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_port_get_range(IntPtr port, out IntPtr deflt, out IntPtr min, out IntPtr max);

    // Node operations
    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lilv_node_as_string(IntPtr node);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float lilv_node_as_float(IntPtr node);

    [DllImport(LilvLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void lilv_node_free(IntPtr node);
}

/// <summary>
/// Implementation of ILv2PluginService that loads and manages LV2 plugins via lilv.
/// </summary>
public class Lv2PluginService : ILv2PluginService
{
    private readonly ILog<Lv2PluginService> _log;
    private IntPtr _world = IntPtr.Zero;
    private Dictionary<string, Lv2PluginInstance> _loadedPlugins = new();

    public Lv2PluginService(ILog<Lv2PluginService> log)
    {
        _log = log;
        InitializeWorld();
    }

    private void InitializeWorld()
    {
        try
        {
            _world = InteropLilv.lilv_world_new();
            if (_world == IntPtr.Zero)
            {
                _log.Error("Failed to create lilv world");
                return;
            }
            _log.Debug("Loading all LV2 plugins from system...");
            InteropLilv.lilv_world_load_all(_world);
            _log.Debug("LV2 plugin world initialized");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error initializing lilv world");
        }
    }

    public async Task<List<string>> DiscoverPluginsAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var plugins = new List<string>();

            if (_world == IntPtr.Zero)
            {
                _log.Warn("World not initialized");
                return plugins;
            }

            try
            {
                _log.Debug("Starting plugin discovery...");
                var pluginsPtr = InteropLilv.lilv_world_get_all_plugins(_world);

                if (pluginsPtr == IntPtr.Zero)
                {
                    _log.Warn("No plugins found");
                    return plugins;
                }

                var iterator = InteropLilv.lilv_plugins_begin(pluginsPtr);

                while (!InteropLilv.lilv_plugins_is_end(pluginsPtr, iterator))
                {
                    var plugin = InteropLilv.lilv_plugins_get(pluginsPtr, iterator);

                    if (plugin != IntPtr.Zero)
                    {
                        var uriNode = InteropLilv.lilv_plugin_get_uri(plugin);
                        if (uriNode != IntPtr.Zero)
                        {
                            var uriStr = Marshal.PtrToStringAnsi(InteropLilv.lilv_node_as_string(uriNode));
                            if (!string.IsNullOrEmpty(uriStr))
                            {
                                plugins.Add(uriStr);
                                _log.Debug($"Found plugin: {uriStr}");
                            }
                        }
                    }

                    iterator = InteropLilv.lilv_plugins_next(pluginsPtr, iterator);
                }

                _log.Debug($"Discovery complete: found {plugins.Count} plugin(s)");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error discovering plugins");
            }

            return plugins;
        }, cancellationToken);
    }

    public async Task<Lv2PluginInstance?> LoadPluginAsync(string uri, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            _log.Debug($"Loading plugin: {uri}");

            if (_world == IntPtr.Zero)
            {
                _log.Error("World not initialized");
                return null;
            }

            try
            {
                var pluginsPtr = InteropLilv.lilv_world_get_all_plugins(_world);
                if (pluginsPtr == IntPtr.Zero)
                {
                    _log.Error("Could not get plugin list");
                    return null;
                }

                // Find the plugin by URI
                IntPtr foundPlugin = IntPtr.Zero;
                var iterator = InteropLilv.lilv_plugins_begin(pluginsPtr);

                while (!InteropLilv.lilv_plugins_is_end(pluginsPtr, iterator))
                {
                    var plugin = InteropLilv.lilv_plugins_get(pluginsPtr, iterator);

                    if (plugin != IntPtr.Zero)
                    {
                        var uriNode = InteropLilv.lilv_plugin_get_uri(plugin);
                        if (uriNode != IntPtr.Zero)
                        {
                            var uriStr = Marshal.PtrToStringAnsi(InteropLilv.lilv_node_as_string(uriNode));
                            if (uriStr == uri)
                            {
                                foundPlugin = plugin;
                                break;
                            }
                        }
                    }

                    iterator = InteropLilv.lilv_plugins_next(pluginsPtr, iterator);
                }

                if (foundPlugin == IntPtr.Zero)
                {
                    _log.Error($"Plugin not found: {uri}");
                    return null;
                }

                // Get plugin metadata
                var nameNode = InteropLilv.lilv_plugin_get_name(foundPlugin);
                var name = nameNode != IntPtr.Zero ? Marshal.PtrToStringAnsi(InteropLilv.lilv_node_as_string(nameNode)) : "Unknown";

                var portCount = InteropLilv.lilv_plugin_get_num_ports(foundPlugin);
                _log.Info($"Plugin loaded: {name}, Ports: {portCount}");

                // Create plugin instance
                var instance = new Lv2PluginInstance
                {
                    URI = uri,
                    Name = name ?? "Unknown",
                    Handle = foundPlugin,
                    Descriptor = IntPtr.Zero,
                    Feature = IntPtr.Zero,
                    ControlValues = new Dictionary<string, float>()
                };

                _loadedPlugins[uri] = instance;
                return instance;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error loading plugin");
                return null;
            }
        }, cancellationToken);
    }

    public async Task<bool> TryLoadPluginByUriAsync(string uri, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            _log.Debug($"Checking if plugin exists: {uri}");

            if (_world == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var pluginsPtr = InteropLilv.lilv_world_get_all_plugins(_world);
                if (pluginsPtr == IntPtr.Zero)
                {
                    return false;
                }

                var iterator = InteropLilv.lilv_plugins_begin(pluginsPtr);

                while (!InteropLilv.lilv_plugins_is_end(pluginsPtr, iterator))
                {
                    var plugin = InteropLilv.lilv_plugins_get(pluginsPtr, iterator);

                    if (plugin != IntPtr.Zero)
                    {
                        var uriNode = InteropLilv.lilv_plugin_get_uri(plugin);
                        if (uriNode != IntPtr.Zero)
                        {
                            var uriStr = Marshal.PtrToStringAnsi(InteropLilv.lilv_node_as_string(uriNode));
                            if (uriStr == uri)
                            {
                                _log.Debug($"Plugin found: {uri}");
                                return true;
                            }
                        }
                    }

                    iterator = InteropLilv.lilv_plugins_next(pluginsPtr, iterator);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error checking plugin existence");
            }

            return false;
        }, cancellationToken);
    }

    public async Task<List<Lv2Port>> GetPluginPortsAsync(string uri, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            _log.Debug($"Getting ports for plugin: {uri}");
            var ports = new List<Lv2Port>();

            if (!_loadedPlugins.TryGetValue(uri, out var plugin))
            {
                _log.Warn($"Plugin not loaded: {uri}");
                return ports;
            }

            try
            {
                var pluginPtr = plugin.Handle;
                if (pluginPtr == IntPtr.Zero)
                {
                    return ports;
                }

                var portCount = InteropLilv.lilv_plugin_get_num_ports(pluginPtr);

                for (uint i = 0; i < portCount; i++)
                {
                    var portPtr = InteropLilv.lilv_plugin_get_port_by_index(pluginPtr, i);
                    if (portPtr != IntPtr.Zero)
                    {
                        var symbolNode = InteropLilv.lilv_port_get_symbol(portPtr);
                        var nameNode = InteropLilv.lilv_port_get_name(portPtr);

                        var symbol = symbolNode != IntPtr.Zero ? Marshal.PtrToStringAnsi(InteropLilv.lilv_node_as_string(symbolNode)) : $"port_{i}";
                        var name = nameNode != IntPtr.Zero ? Marshal.PtrToStringAnsi(InteropLilv.lilv_node_as_string(nameNode)) : symbol;

                        var port = new Lv2Port
                        {
                            Index = (int)i,
                            Symbol = symbol ?? "unknown",
                            Name = name ?? "unknown",
                            Type = "unknown",
                            Direction = "unknown",
                            Default = 0f,
                            Minimum = 0f,
                            Maximum = 1f
                        };

                        ports.Add(port);
                        _log.Debug($"  Port {i}: {symbol} ({name})");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error getting plugin ports");
            }

            return ports;
        }, cancellationToken);
    }

    public void SetControlPort(Lv2PluginInstance plugin, string symbol, float value)
    {
        _log.Debug($"Setting control port {symbol} = {value}");
        if (plugin.ControlValues.ContainsKey(symbol))
        {
            plugin.ControlValues[symbol] = value;
        }
        else
        {
            plugin.ControlValues.Add(symbol, value);
        }
    }

    public float GetControlPort(Lv2PluginInstance plugin, string symbol)
    {
        _log.Debug($"Getting control port {symbol}");
        if (plugin.ControlValues.TryGetValue(symbol, out var value))
        {
            return value;
        }
        return 0f;
    }

    public void UnloadPlugin(Lv2PluginInstance plugin)
    {
        _log.Debug($"Unloading plugin: {plugin.URI}");
        _loadedPlugins.Remove(plugin.URI);
    }

    ~Lv2PluginService()
    {
        if (_world != IntPtr.Zero)
        {
            InteropLilv.lilv_world_free(_world);
            _world = IntPtr.Zero;
        }
    }
}
