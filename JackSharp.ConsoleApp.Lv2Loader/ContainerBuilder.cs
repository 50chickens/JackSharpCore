using JackSharp.ConsoleApp.Lv2Loader.Logging;
using JackSharp.ConsoleApp.Lv2Loader.Services;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;

namespace JackSharp.ConsoleApp.Lv2Loader;

/// <summary>
/// Encapsulates all dependency injection container configuration and service registration.
/// This class centralizes DI setup so it can be reused across the application and tests.
/// </summary>
public static class ContainerBuilder
{
    /// <summary>
    /// Builds and returns the configured host with all registered services.
    /// </summary>
    /// <param name="args">Optional command-line arguments (defaults to empty array if not provided)</param>
    /// <returns>A configured IHost with all services registered</returns>
    public static IHost Build(string[]? args = null)
    {
        args ??= [];

        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder, args);
        return builder.Build();
    }

    /// <summary>
    /// Configures all services and logging in the provided HostApplicationBuilder.
    /// This method is used by both the main application and tests.
    /// </summary>
    /// <param name="builder">The HostApplicationBuilder to configure</param>
    /// <param name="args">Command-line arguments for feature detection</param>
    public static void ConfigureServices(HostApplicationBuilder builder, string[]? args = null)
    {
        args ??= [];

        // Add configuration for environment
        builder.Configuration.AddEnvironmentVariables("JACK_");

        // Register options
        builder.Services.AddOptions<JackOptions>()
            .Bind(builder.Configuration.GetSection(JackOptions.SettingsKey));

        // Register logger first so it's available for all other services
        builder.Services.AddSingleton(typeof(ILog<>), typeof(NLogAdapter<>));

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Logging.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Lifetime", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Host", LogLevel.Warning);

        builder.Logging.AddNLog().AddNLogConfiguration().AddNlogFactoryAdaptor();

        // Register validation service for Jack environment variables
        builder.Services.AddSingleton<IValidateOptions<JackOptions>, JackEnvironmentValidationService>();

        // Register Jack services
        builder.Services.AddSingleton<IJackServerDiscoveryService, JackServerDiscoveryService>();
        builder.Services.AddSingleton<IJackConnectionManagerService, JackConnectionManagerService>();

        // Register LV2 plugin services
        builder.Services.AddSingleton<ILv2PluginService, Lv2PluginService>();
        builder.Services.AddSingleton<ILv2AudioProcessingService, Lv2AudioProcessingService>();

        // Register worker
        builder.Services.AddSingleton<Lv2LoaderWorker>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Lv2LoaderWorker>());
    }
}
