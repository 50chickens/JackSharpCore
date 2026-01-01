namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Service for verifying ALSA audio capture independently of Jack.
/// </summary>
public interface IAlsaVerificationService
{
    /// <summary>
    /// Verifies that ALSA is receiving audio data from the hardware.
    /// </summary>
    Task VerifyAlsaCaptureAsync(CancellationToken cancellationToken);
}
