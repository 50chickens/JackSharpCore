namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// Represents a port in an LV2 plugin.
/// </summary>
public class Lv2Port
{
    public required int Index { get; set; }
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // "audio", "control"
    public required string Direction { get; set; } // "input", "output"
    public float Default { get; set; }
    public float Minimum { get; set; }
    public float Maximum { get; set; }
}
