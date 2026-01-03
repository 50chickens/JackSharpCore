namespace JackSharp.ConsoleApp.Lv2Loader.Services;

/// <summary>
/// Generates audio streams for testing (simulating real audio from soundcard).
/// </summary>
public class AudioStreamGenerator
{
    private readonly int _sampleRate;
    private readonly float _frequency;
    private float _phase = 0f;
    private const float TwoPi = 2f * MathF.PI;

    /// <summary>
    /// Create an audio stream generator with sine wave output.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="frequency">Sine wave frequency in Hz</param>
    public AudioStreamGenerator(int sampleRate, float frequency = 1000f)
    {
        _sampleRate = sampleRate;
        _frequency = frequency;
    }

    /// <summary>
    /// Generate audio samples.
    /// </summary>
    /// <param name="frames">Number of samples to generate</param>
    /// <param name="amplitude">Amplitude (0.0 to 1.0)</param>
    /// <returns>Float array of audio samples</returns>
    public float[] GenerateSineWave(int frames, float amplitude = 0.1f)
    {
        var samples = new float[frames];
        var phaseIncrement = (TwoPi * _frequency) / _sampleRate;

        for (int i = 0; i < frames; i++)
        {
            samples[i] = amplitude * MathF.Sin(_phase);
            _phase += phaseIncrement;
            
            // Keep phase in valid range to prevent numerical issues
            if (_phase > TwoPi)
            {
                _phase -= TwoPi;
            }
        }

        return samples;
    }

    /// <summary>
    /// Reset the phase for a new stream.
    /// </summary>
    public void Reset()
    {
        _phase = 0f;
    }
}

/// <summary>
/// Analyzes audio for comparison and validation.
/// </summary>
public class AudioAnalyzer
{
    /// <summary>
    /// Compare two audio buffers and return metrics.
    /// </summary>
    public static AudioComparisonResult Compare(float[] input, float[] output, float amplitudeGain = 1.0f)
    {
        if (input.Length != output.Length)
        {
            return new AudioComparisonResult
            {
                IsMatch = false,
                Error = $"Buffer length mismatch: input={input.Length}, output={output.Length}"
            };
        }

        if (input.Length == 0)
        {
            return new AudioComparisonResult
            {
                IsMatch = false,
                Error = "Empty buffers"
            };
        }

        // Calculate metrics for input and output
        var inputMetrics = CalculateMetrics(input);
        var outputMetrics = CalculateMetrics(output);

        // Adjust expected output for gain
        var expectedOutputRMS = inputMetrics.RMS * amplitudeGain;
        var expectedOutputPeak = inputMetrics.Peak * amplitudeGain;

        // Calculate differences
        var rmsDifference = Math.Abs(outputMetrics.RMS - expectedOutputRMS);
        var peakDifference = Math.Abs(outputMetrics.Peak - expectedOutputPeak);

        // Check if audio passes through with expected gain
        const float toleranceRMS = 0.001f; // 0.1% tolerance for RMS
        const float tolerancePeak = 0.001f; // 0.1% tolerance for peak

        var isMatch = rmsDifference < toleranceRMS && peakDifference < tolerancePeak;

        // Calculate cross-correlation for phase matching
        var crossCorrelation = CalculateCrossCorrelation(input, output);

        return new AudioComparisonResult
        {
            IsMatch = isMatch,
            InputRMS = inputMetrics.RMS,
            OutputRMS = outputMetrics.RMS,
            ExpectedOutputRMS = expectedOutputRMS,
            InputPeak = inputMetrics.Peak,
            OutputPeak = outputMetrics.Peak,
            ExpectedOutputPeak = expectedOutputPeak,
            RMSDifference = rmsDifference,
            PeakDifference = peakDifference,
            CrossCorrelation = crossCorrelation,
            Error = null
        };
    }

    private static AudioMetrics CalculateMetrics(float[] samples)
    {
        float sumSquares = 0f;
        float maxAbs = 0f;

        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
            var abs = Math.Abs(sample);
            if (abs > maxAbs)
            {
                maxAbs = abs;
            }
        }

        var rms = MathF.Sqrt(sumSquares / samples.Length);
        return new AudioMetrics { RMS = rms, Peak = maxAbs };
    }

    private static float CalculateCrossCorrelation(float[] input, float[] output)
    {
        float correlation = 0f;
        for (int i = 0; i < input.Length; i++)
        {
            correlation += input[i] * output[i];
        }
        return correlation / input.Length;
    }

    private class AudioMetrics
    {
        public float RMS { get; set; }
        public float Peak { get; set; }
    }
}

/// <summary>
/// Result of audio comparison.
/// </summary>
public class AudioComparisonResult
{
    public bool IsMatch { get; set; }
    public float InputRMS { get; set; }
    public float OutputRMS { get; set; }
    public float ExpectedOutputRMS { get; set; }
    public float InputPeak { get; set; }
    public float OutputPeak { get; set; }
    public float ExpectedOutputPeak { get; set; }
    public float RMSDifference { get; set; }
    public float PeakDifference { get; set; }
    public float CrossCorrelation { get; set; }
    public string? Error { get; set; }

    public override string ToString()
    {
        if (Error != null)
        {
            return $"Error: {Error}";
        }

        return $"""
            Input RMS: {InputRMS:F6} | Output RMS: {OutputRMS:F6} (Expected: {ExpectedOutputRMS:F6})
            Input Peak: {InputPeak:F6} | Output Peak: {OutputPeak:F6} (Expected: {ExpectedOutputPeak:F6})
            RMS Diff: {RMSDifference:F6} | Peak Diff: {PeakDifference:F6}
            Cross Correlation: {CrossCorrelation:F6}
            Match: {IsMatch}
            """;
    }
}
