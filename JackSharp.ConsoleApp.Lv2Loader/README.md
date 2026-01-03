# JackSharp.ConsoleApp.Lv2Loader

A .NET console application for loading and managing LV2 (Linux Audio Plug-In Standard Version 2) plugins within a Jack audio server environment.

## Overview

This application provides a framework for loading LV2 plugins into a Jack audio context, enabling audio processing through plugin chains. It uses the same dependency injection and logging patterns as the Diagnostics application for consistency and maintainability.

## Architecture

### Key Components

- **Lv2LoaderWorker**: Main worker service managing the plugin lifecycle
- **ContainerBuilder**: Dependency injection configuration
- **Logging System**: Structured logging using NLog
- **Jack Integration**: Connection management with Jack audio server

### Service Structure

```
JackSharp.ConsoleApp.Lv2Loader/
├── Logging/           # Shared logging infrastructure
├── Services/          # Jack-related services
│   ├── IJackConnectionManagerService.cs
│   ├── JackConnectionManagerService.cs
│   ├── IJackServerDiscoveryService.cs
│   ├── JackServerDiscoveryService.cs
│   └── JackEnvironmentValidationService.cs
├── Options/           # Configuration options
├── Program.cs         # Entry point
├── ContainerBuilder.cs # DI configuration
├── Worker.cs          # Lv2LoaderWorker implementation
└── appsettings.json   # Configuration file
```

## Building

```bash
dotnet build JackSharp.ConsoleApp.Lv2Loader -c Release
```

## Running

```bash
dotnet run --project JackSharp.ConsoleApp.Lv2Loader
```

## Dependencies

- .NET 9.0 or later
- Jack Audio Connection Kit
- NLog logging framework
- Microsoft.Extensions.Hosting

## Environment Variables

The application requires the following Jack environment variables:

- `JACK_PROMISCUOUS_SERVER`: Enable promiscuous server mode
- `JACK_NO_AUDIO_RESERVATION`: Disable audio reservation
- `JACK_SERVER_NAME`: Jack server name (default: "default")

## Configuration

Configuration is loaded from `appsettings.json` and environment variables. The Jack server connection is automatically validated on startup.

## Future Development

- LV2 plugin discovery and enumeration
- Plugin instantiation and parameter management
- Audio port connection and routing
- Plugin chain serialization/deserialization
- Real-time parameter control
