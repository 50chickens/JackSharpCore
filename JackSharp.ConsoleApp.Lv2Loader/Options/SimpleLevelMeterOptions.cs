namespace JackSharp.ConsoleApp.Lv2Loader.Options;

/// <summary>
/// Options for the simple level meter service.
/// Controls measurement duration and count for continuous monitoring.
/// </summary>
public class SimpleLevelMeterOptions
{
    public const string Settings = "SimpleLevelMeter";

    // Parameterless constructor required for configuration binder / OptionsFactory
    public SimpleLevelMeterOptions()
    {
        MeasurementDuration = 3;
        MeasurementCount = 5;
    }

    // Optional convenience constructor
    public SimpleLevelMeterOptions(int measurementDuration, int measurementCount)
    {
        MeasurementDuration = measurementDuration;
        MeasurementCount = measurementCount;
    }

    /// <summary>
    /// Duration of each measurement in seconds (default: 3)
    /// </summary>
    public int MeasurementDuration { get; set; }

    /// <summary>
    /// Number of measurements to take (default: 5)
    /// </summary>
    public int MeasurementCount { get; set; }
}
