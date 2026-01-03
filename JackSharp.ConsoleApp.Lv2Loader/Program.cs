using JackSharp.ConsoleApp.Lv2Loader.Logging;

namespace JackSharp.ConsoleApp.Lv2Loader;

internal class Program
{
    private static void Main(string[] args)
    {
        var logger = LogManager.GetLogger<Program>();

        try
        {
            using var host = ContainerBuilder.Build(args);
            logger.Info("LV2 Loader application starting....");
            host.Run();
            logger.Info("LV2 Loader application finished.");
        }
        catch (OperationCanceledException)
        {
            logger.Info("LV2 Loader application stopped");
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
