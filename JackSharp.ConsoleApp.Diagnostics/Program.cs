using JackSharp.ConsoleApp.Diagnostics.Logging;

namespace JackSharp.ConsoleApp.Diagnostics;

internal class Program
{
    private static void Main(string[] args)
    {
        var logger = LogManager.GetLogger<Program>();

        try
        {
            using var host = ContainerBuilder.Build(args);
            logger.Info("Application starting....");
            host.Run();
            logger.Info("Application finished.");
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopped via Ctrl+C or host shutdown
            logger.Info("Diagnostics application stopped");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Application exited with an error: {ex.GetType().Name}");
            Environment.Exit(1);
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}

