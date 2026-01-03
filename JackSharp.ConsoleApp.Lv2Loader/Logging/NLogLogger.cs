using Common.Logging.Factory;

namespace JackSharp.ConsoleApp.Lv2Loader.Logging
{
    public class NLogLogger : AbstractLogger
    {
        private readonly NLog.Logger _log;
        public NLogLogger(NLog.Logger log)
        {
            _log = log;
        }

        public override bool IsTraceEnabled => true;
        public override bool IsDebugEnabled => true;

        public override bool IsInfoEnabled => true;

        public override bool IsWarnEnabled => true;

        public override bool IsErrorEnabled => true;

        public override bool IsFatalEnabled => true;

        protected override void WriteInternal(Common.Logging.LogLevel level, object message, Exception exception)
        {
            var logEventInfo = new NLog.LogEventInfo
            {
                Level = level.ToNlogLogLevel(),
                Message = message?.ToString() ?? string.Empty,
                Exception = exception
            };
            _log.Log(logEventInfo);
        }
    }
}
