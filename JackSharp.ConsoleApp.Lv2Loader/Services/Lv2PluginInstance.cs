namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// Represents an LV2 plugin instance with its audio and control ports.
/// </summary>
public class Lv2PluginInstance
{
    public required string URI { get; set; }
    public required string Name { get; set; }
    public required IntPtr Handle { get; set; }
    public required IntPtr Descriptor { get; set; }
    public required IntPtr Feature { get; set; }
    public int AudioInputCount { get; set; }
    public int AudioOutputCount { get; set; }
    public int ControlInputCount { get; set; }
    public int ControlOutputCount { get; set; }
    public Dictionary<string, float> ControlValues { get; set; } = new();
}
