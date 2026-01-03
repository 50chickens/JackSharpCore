using NLog.Config;
using NLog.Targets;

namespace JackSharp.ConsoleApp.Lv2Loader.Logging
{
    public class LogRegistration
    {
        public LogRegistration()
        {

        }
        public bool ShouldLogHeaderOnStartup { get; set; } = true;
        public Target? Target { get; set; }
        public IList<LoggingRule> LoggingRules { get; set; } = new List<LoggingRule>();
    }
}
