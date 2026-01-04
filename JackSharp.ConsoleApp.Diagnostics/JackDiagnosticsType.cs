namespace JackSharp.ConsoleApp.Diagnostics;

/// <summary>
/// Enum defining the different types of Jack diagnostics operations.
/// </summary>
public enum JackDiagnosticsType
{
    /// <summary>Default: Server discovery and connection status</summary>
    ServerDiscovery,

    /// <summary>Measure and display input levels continuously</summary>
    MeasureInputLevels,

    /// <summary>Simple level meter with periodic updates</summary>
    SimpleLevelMeter,

    /// <summary>Audio test diagnostics</summary>
    AudioTest,

    /// <summary>Audio quality analysis with THD and signal metrics</summary>
    AudioQualityAnalysis,

    /// <summary>Loopback test - verify signal fidelity through hardware</summary>
    LoopbackTest,

    /// <summary>Comprehensive debug with ALSA and buffer inspection</summary>
    ComprehensiveDebug
}
