using JackSharp.ConsoleApp.Diagnostics.Interfaces;
using JackSharp.ConsoleApp.Diagnostics.Logging;
using JackSharp.ConsoleApp.Diagnostics.Services;

namespace JackSharp.ConsoleApp.Diagnostics
{
    internal static class DiagnosticsWorkerBuilder
    {
        public static DiagnosticsWorker Build(IServiceProvider sp, string[]? args)
        {
            return BuildDiagnosticsWorker(sp, args);
        }

        private static DiagnosticsWorker BuildDiagnosticsWorker(IServiceProvider sp, string[]? args)
        {
            var log = sp.GetRequiredService<ILog<DiagnosticsWorker>>();
            var discoveryService = sp.GetRequiredService<IJackServerDiscoveryService>();
            var connectionManager = sp.GetRequiredService<IJackConnectionManagerService>();
            var simpleLevelMeterService = sp.GetRequiredService<IJackSimpleLevelMeterService>();
            var hardwareInputMonitorService = sp.GetRequiredService<IJackHardwareInputMonitorService>();
            var audioQualityAnalysisService = sp.GetRequiredService<AudioQualityAnalysisService>();
            var loopbackTestService = sp.GetRequiredService<LoopbackTestService>();
            var rawBufferDebugService = sp.GetRequiredService<IJackRawBufferDebugService>();
            var lifetime = sp.GetRequiredService<IHostApplicationLifetime>();

            return new DiagnosticsWorker(log, discoveryService, connectionManager, simpleLevelMeterService, hardwareInputMonitorService, loopbackTestService, lifetime, args);
        }
    }
}