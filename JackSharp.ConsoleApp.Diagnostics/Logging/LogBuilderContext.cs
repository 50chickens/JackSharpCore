namespace JackSharp.ConsoleApp.Diagnostics.Logging
{
    public class LogBuilderContext
    {
        private readonly IConfiguration _configuration;
        public LoggingSettings Settings { get; }
        public Common.Logging.LogLevel LogLevel { get; internal set; }

        public LogBuilderContext(IConfiguration configuration)
        {
            _configuration = configuration;
            Settings = configuration.GetSection("Logging").Get<LoggingSettings>() ?? new LoggingSettings();
            LogLevel = Settings.GetLogLevel().ToCommonLoggingLevel();
        }
    }
}
