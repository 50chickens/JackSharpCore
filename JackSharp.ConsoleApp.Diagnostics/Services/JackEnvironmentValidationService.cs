using JackSharp.ConsoleApp.Diagnostics.Logging;
using Microsoft.Extensions.Options;

namespace JackSharp.ConsoleApp.Diagnostics.Services;

/// <summary>
/// Validates that required Jack environment variables are set correctly
/// </summary>
public class JackEnvironmentValidationService(ILog<JackEnvironmentValidationService> log) : IValidateOptions<JackOptions>
{
    private readonly ILog<JackEnvironmentValidationService> _log = log;

    public ValidateOptionsResult Validate(string? name, JackOptions options)
    {
        var failures = new List<string>();

        // Check JACK_PROMISCUOUS_SERVER environment variable
        var promiscuousServer = Environment.GetEnvironmentVariable("JACK_PROMISCUOUS_SERVER");
        if (string.IsNullOrEmpty(promiscuousServer))
        {
            failures.Add("JACK_PROMISCUOUS_SERVER environment variable is not set. Set it before running the application.");
            _log.Warn("JACK_PROMISCUOUS_SERVER environment variable is not set");
        }
        else
        {
            _log.Info($"Jack Promiscuous Server: {promiscuousServer}");
        }

        // Check JACK_NO_AUDIO_RESERVATION environment variable
        var noAudioReservation = Environment.GetEnvironmentVariable("JACK_NO_AUDIO_RESERVATION");
        if (string.IsNullOrEmpty(noAudioReservation))
        {
            failures.Add("JACK_NO_AUDIO_RESERVATION environment variable is not set. Set it before running the application.");
            _log.Warn("JACK_NO_AUDIO_RESERVATION environment variable is not set");
        }
        else
        {
            _log.Info($"Jack No Audio Reservation: {noAudioReservation}");
        }

        // Check Jack server name configuration
        if (string.IsNullOrEmpty(options.ServerName))
        {
            failures.Add("Jack ServerName option is not configured.");
            _log.Warn("Jack ServerName option is not configured");
        }
        else
        {
            _log.Info($"Jack Server Name: {options.ServerName}");
        }

        if (failures.Count > 0)
        {
            _log.Error($"Jack environment validation failed with {failures.Count} error(s)");
            return ValidateOptionsResult.Fail(failures);
        }

        _log.Info("Jack environment variables validated successfully");
        return ValidateOptionsResult.Success;
    }
}
