using NLog.Config;

namespace JackSharp.ConsoleApp.Lv2Loader.Logging
{
    public class LogBuilder
    {
        private List<LogRegistration> _logRegistrations = new List<LogRegistration>();
        public readonly LogBuilderContext Context;

        public LogBuilder(IConfiguration configuration)
        {
            Context = new LogBuilderContext(configuration);
        }
        public void Build()
        {
            //initialize Nlog adaptor for Common.Logging. this configures common logging to use Nlog as the backend
            Common.Logging.LogManager.Adapter = new NLogLoggerFactoryAdapter(new System.Collections.Specialized.NameValueCollection());
            var nlogManagerConfiguration = NLog.LogManager.Configuration;
            var nlogConfiguration = nlogManagerConfiguration ?? new LoggingConfiguration();
            //register each LogRegistration With NLog
            foreach (var logRegistration in _logRegistrations)
            {
                // add target using its configured name (or fallback to a generated name)
                if (logRegistration.Target != null)
                {
                    var targetName = !string.IsNullOrEmpty(logRegistration.Target.Name) ? logRegistration.Target.Name : "target";
                    nlogConfiguration.AddTarget(targetName, logRegistration.Target);
                }
                // add any logging rules configured for this registration
                if (logRegistration.LoggingRules != null)
                {
                    foreach (var rule in logRegistration.LoggingRules)
                    {
                        nlogConfiguration.LoggingRules.Add(rule);
                    }
                }
            }
            NLog.LogManager.Configuration = nlogConfiguration;
        }
        public void AddRegistration(LogRegistration registration)
        {
            _logRegistrations.Add(registration);
        }
    }
}
