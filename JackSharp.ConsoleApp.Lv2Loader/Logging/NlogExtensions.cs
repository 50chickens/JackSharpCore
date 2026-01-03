using NLog.Config;
using NLog.Targets;
using System.Collections.Specialized;

namespace JackSharp.ConsoleApp.Lv2Loader.Logging;

public static class NLogExtensions
{
    /// <summary>
    /// Configure Common.Logging to use the NLog factory adapter
    /// so calls to Common.Logging.LogManager.GetLogger(...) are backed by NLog.
    /// </summary>
    public static ILoggingBuilder AddNlogFactoryAdaptor(this ILoggingBuilder builder)
    {
        // Set the Common.Logging adapter to the project's NLog adapter.
        Common.Logging.LogManager.Adapter = new NLogLoggerFactoryAdapter(new NameValueCollection());
        return builder;
    }

    /// <summary>
    /// Apply NLog configuration with console and/or file targets based on LoggingSettings.
    /// This moves programmatic logging setup out of `Program.cs` so it can be reused/tested.
    /// </summary>
    public static ILoggingBuilder AddNLogConfiguration(this ILoggingBuilder builder, LoggingSettings? settings = null)
    {
        settings ??= new LoggingSettings();

        var nlogConfig = new LoggingConfiguration();
        var layout = "${longdate}|${level:uppercase=true}|${logger:shortName=true}|${message}${onexception:${newline}${exception:format=tostring}}";
        var logLevel = settings.GetLogLevel();

        if (settings.EnableConsoleLogging)
        {
            var consoleTarget = new ColoredConsoleTarget("console")
            {
                Layout = layout
            };
            nlogConfig.AddTarget(consoleTarget);
            nlogConfig.AddRule(logLevel, NLog.LogLevel.Fatal, consoleTarget);
        }

        if (settings.EnableFileLogging)
        {
            var logFilePath = settings.GetLogFilePath();
            var archiveFolderPath = settings.GetArchiveFolderPath();
            var archiveFileName = Path.Combine(archiveFolderPath, Path.GetFileNameWithoutExtension(settings.LogFileName) + "-{#}.log");

            var fileTarget = new FileTarget("file")
            {
                FileName = logFilePath,
                Layout = layout,
                ArchiveFileName = archiveFileName,
                ArchiveSuffixFormat = "0",
                MaxArchiveFiles = settings.MaxArchiveFiles,
                ArchiveAboveSize = settings.MaxLogFileSizeMB * 1024 * 1024,
                CreateDirs = true
            };
            nlogConfig.AddTarget(fileTarget);
            nlogConfig.AddRule(logLevel, NLog.LogLevel.Fatal, fileTarget);
        }

        NLog.LogManager.Configuration = nlogConfig;
        return builder;
    }
    public static NLog.LogLevel ToNlogLogLevel(this Common.Logging.LogLevel level)
    {
        return level switch
        {
            Common.Logging.LogLevel.All => NLog.LogLevel.Trace,
            Common.Logging.LogLevel.Trace => NLog.LogLevel.Trace,
            Common.Logging.LogLevel.Debug => NLog.LogLevel.Debug,
            Common.Logging.LogLevel.Info => NLog.LogLevel.Info,
            Common.Logging.LogLevel.Warn => NLog.LogLevel.Warn,
            Common.Logging.LogLevel.Error => NLog.LogLevel.Error,
            Common.Logging.LogLevel.Fatal => NLog.LogLevel.Fatal,
            Common.Logging.LogLevel.Off => NLog.LogLevel.Off,
            _ => NLog.LogLevel.Info,
        };
    }

    public static Common.Logging.LogLevel ToCommonLoggingLevel(this NLog.LogLevel level)
    {
        return level.Name switch
        {
            "Trace" => Common.Logging.LogLevel.Trace,
            "Debug" => Common.Logging.LogLevel.Debug,
            "Info" => Common.Logging.LogLevel.Info,
            "Warn" => Common.Logging.LogLevel.Warn,
            "Error" => Common.Logging.LogLevel.Error,
            "Fatal" => Common.Logging.LogLevel.Fatal,
            "Off" => Common.Logging.LogLevel.Off,
            _ => Common.Logging.LogLevel.Info,
        };
    }
}
