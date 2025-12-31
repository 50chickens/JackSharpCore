namespace JackSharp.ConsoleApp.Diagnostics.Logging;

public class NLogAdapter<T> : ILog<T>
{
    private readonly ILogger<T> _log;

    public NLogAdapter(ILogger<T> log)
    {
        _log = log;
    }

    public void Info(string message) => _log.LogInformation(message);
    public void Debug(string message) => _log.LogDebug(message);
    public void Warn(string message) => _log.LogWarning(message);
    public void Error(Exception ex, string? message = null) => _log.LogError(ex, message);
    public void Error(string message) => _log.LogError(message);
    public void Trace(string message) => _log.LogTrace(message);
}
