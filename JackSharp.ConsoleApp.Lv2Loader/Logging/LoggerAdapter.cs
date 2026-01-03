namespace JackSharp.ConsoleApp.Lv2Loader.Logging
{
    /// <summary>
    /// Adapter that implements the project's ILog<T> by delegating to Microsoft.Extensions.Logging.ILogger<T>.
    /// This allows components to migrate to ILogger<T> while preserving existing ILog<T> usages.
    /// </summary>
    public class LoggerAdapter<T> : ILog<T>
    {
        private readonly ILogger<T> _log;

        public LoggerAdapter(ILogger<T> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Debug(string message) => _log.LogDebug(message);
        public void Info(string message) => _log.LogInformation(message);
        public void Warn(string message) => _log.LogWarning(message);
        public void Error(string message) => _log.LogError(message);
        public void Error(Exception ex, string message) => _log.LogError(ex, message);
        public void Trace(string message) => _log.LogTrace(message);

    }
}
