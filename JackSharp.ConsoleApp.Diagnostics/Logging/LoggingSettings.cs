
namespace JackSharp.ConsoleApp.Diagnostics.Logging
{
    public class LoggingSettings()
    {
        public bool EnableConsoleLogging { get; set; } = true;
        public bool EnableFileLogging { get; set; } = true;

        // File logging configuration
        public string LogFolder { get; set; } = "logs"; // Relative to current working directory, or absolute path
        public string LogFileName { get; set; } = "JackSharp.ConsoleApp.Diagnostics-${shortdate}.log";
        public int MaxLogFileSizeMB { get; set; } = 10;
        public int MaxArchiveFiles { get; set; } = 30;
        public string ArchiveFolder { get; set; } = "archives";

        private string _minimumLogLevel = "Info";
        public string MinimumLogLevel
        {
            get => _minimumLogLevel;
            set => _minimumLogLevel = value;
        }

        public NLog.LogLevel GetLogLevel() => NLog.LogLevel.FromString(_minimumLogLevel);

        /// <summary>
        /// Gets the full path to the log file, resolving relative paths based on current working directory.
        /// </summary>
        public string GetLogFilePath()
        {
            var folder = LogFolder.StartsWith('/') || LogFolder.Contains(':')
                ? LogFolder // Absolute path
                : Path.Combine(Directory.GetCurrentDirectory(), LogFolder); // Relative path

            return Path.Combine(folder, LogFileName);
        }

        /// <summary>
        /// Gets the full path to the archive folder.
        /// </summary>
        public string GetArchiveFolderPath()
        {
            var folder = LogFolder.StartsWith('/') || LogFolder.Contains(':')
                ? LogFolder // Absolute path
                : Path.Combine(Directory.GetCurrentDirectory(), LogFolder); // Relative path

            return Path.Combine(folder, ArchiveFolder);
        }
    }
}
