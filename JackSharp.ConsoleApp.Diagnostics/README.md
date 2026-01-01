# Jack Audio Diagnostics Application

This console application provides comprehensive diagnostics for Jack Audio Server, including server discovery, connection verification, and hardware input level monitoring.

## Features

- **Server Discovery**: Detects running Jack servers and their configuration
- **Connection Status**: Verifies Jack server connectivity and retrieves session info
- **Hardware Input Monitoring**: Real-time monitoring of hardware input levels (similar to `jack_meter`)
- **Audio Diagnostics**: Capture and analyze audio from system inputs

## Building

```bash
cd JackSharpCore
dotnet build
```

## Running

### Default Diagnostics (Server Discovery & Connection Status)

```bash
cd JackSharp.ConsoleApp.Diagnostics
JACK_NO_AUDIO_RESERVATION=1 JACK_PROMISCUOUS_SERVER=jack dotnet run
```

This performs a basic server discovery and connection test, displaying:
- Available Jack servers
- Server configuration (sample rate, buffer size)
- Connection status

### Hardware Input Level Monitoring

```bash
cd JackSharp.ConsoleApp.Diagnostics
JACK_NO_AUDIO_RESERVATION=1 JACK_PROMISCUOUS_SERVER=jack dotnet run --monitor-inputs
```

This starts a real-time hardware input level monitor similar to `jack_meter`. It:
- Connects to up to 16 hardware input channels
- Displays an IEC 60268-18 scale meter for each input with visual bar graph
- Shows audio levels in dBFS continuously
- Updates 8 times per second

Example output:
```
=== Hardware Input Level Monitor ===
Connecting to Jack and monitoring physical inputs...

Connected to Jack at 48000Hz, buffer size: 256
Created 16 input ports for monitoring

-60-50   -40   -35   -30     -25     -20       -15       -10       -5         0
|___|_____|_____|_____|_______|_______|_________|_________|_________|_________|

CH01                                                                                 -80.0dB  |  
CH02                                                                                 -80.0dB  |  
CH03                                                                                 -80.0dB  |  
CH04                                                                                 -80.0dB  |  
```

### Simple dBFS Level Meter

```bash
cd JackSharp.ConsoleApp.Diagnostics
JACK_NO_AUDIO_RESERVATION=1 JACK_PROMISCUOUS_SERVER=jack dotnet run --monitor-levels
```

This starts a simple continuous monitor that prints dBFS levels for hardware inputs. It:
- Connects to up to 16 hardware input channels
- Displays first 8 channels with their dBFS values in a simple text format
- Updates 4 times per second
- Shows timestamp and current dBFS for each channel

Example output:
```
Time: 08:58:28 | CH1: -100.0dBFS | CH2: -100.0dBFS | CH3: -100.0dBFS | CH4: -100.0dBFS | CH5: -100.0dBFS | CH6: -100.0dBFS | CH7: -100.0dBFS | CH8: -100.0dBFS |
```

### Audio Test Diagnostics

```bash
cd JackSharp.ConsoleApp.Diagnostics
JACK_NO_AUDIO_RESERVATION=1 JACK_PROMISCUOUS_SERVER=jack dotnet run --audio-test
```

This captures and analyzes audio from hardware inputs, measuring:
- RMS levels for each channel
- dBFS values
- Total captured frames

## Environment Variables

- `JACK_NO_AUDIO_RESERVATION=1`: Disable audio reservation (allows multiple Jack clients)
- `JACK_PROMISCUOUS_SERVER=jack`: Connect to the default Jack server

## Implementation Details

### Hardware Input Monitoring (jack_meter equivalent)

The `--monitor-inputs` mode is implemented in:
- `Services/IJackHardwareInputMonitorService.cs`: Interface definition
- `Services/JackHardwareInputMonitorService.cs`: Implementation using IEC 60268-18 scale
- `Services/HardwareInputMonitorWorker.cs`: Background worker integration

Key features:
- Uses the JackSharp `Processor` class to create input ports and autoconnect to hardware
- Accumulates audio samples in real-time
- Calculates RMS and dBFS levels using proper audio metering standards
- Updates display at 8 Hz for readable visual feedback
- Supports up to 16 simultaneous input channels

### Architecture

The application uses dependency injection with Microsoft.Extensions.Hosting:
- `ContainerBuilder.cs`: Centralizes service registration
- `Worker.cs`: Default diagnostics background service
- `HardwareInputMonitorWorker.cs`: Input level monitoring service
- `AudioDiagnosticsWorker.cs`: Audio test service

Each worker can be selected via command-line flags in the `ContainerBuilder.ConfigureServices()` method.

## Similar Tools

This diagnostics suite is inspired by JACK's built-in utilities:
- `jack_meter`: Real-time level monitoring (equivalent to `--monitor-inputs`)
- `jack_meter` options are partially replicated (fixed 8 Hz update rate, IEC scale)

## Stopping

Press `Ctrl+C` to stop the monitoring or tests.
