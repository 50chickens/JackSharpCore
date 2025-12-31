using JackSharp.ConsoleApp.Diagnostics.Logging;

namespace JackSharp.ConsoleApp.Diagnostics.Logging
{
    /// <summary>
    /// Static log manager for retrieving loggers without DI.
    /// Provides LogManager.GetLogger&lt;T&gt;() to match requirements.
    /// </summary>
    public static class LogManager
    {
        public static ILog<T> GetLogger<T>()
        {
            return new NLogLoggerCore<T>();
        }
    }
}
