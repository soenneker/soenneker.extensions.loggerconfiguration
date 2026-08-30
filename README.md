[![](https://img.shields.io/nuget/v/Soenneker.Extensions.LoggerConfiguration.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.LoggerConfiguration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.loggerconfiguration/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.loggerconfiguration/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Extensions.LoggerConfiguration.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.LoggerConfiguration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.loggerconfiguration/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.loggerconfiguration/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.LoggerConfiguration
Configures Soenneker's shared Serilog level switch, rolling file output, optional console output, and an early global bootstrap logger.

## Installation

```bash
dotnet add package Soenneker.Extensions.LoggerConfiguration
```

## Configure the application logger

```csharp
using Soenneker.Extensions.LoggerConfiguration;

LoggerConfiguration loggerConfiguration =
    await new LoggerConfiguration().ConfigureLogger(configuration);

Log.Logger = loggerConfiguration.CreateLogger();
```

`ConfigureLogger()`:

- Sets the shared minimum-level switch from `Log:Levels:Default`, falling back to `Log:DefaultLogLevel` and then `Information`.
- Enriches events from `LogContext`.
- Adds a daily rolling file sink with file-size rolling enabled.
- Adds an asynchronous console sink only when `Log:Console` is `true`.
- Returns the same `LoggerConfiguration` instance for final composition and logger creation.

It does not assign the finished logger to `Log.Logger`; the caller does that by calling `CreateLogger()` as shown above. Call it once per configuration instance—repeated calls add duplicate sinks.

Example configuration:

```json
{
  "Log": {
    "Console": true,
    "Levels": {
      "Default": "Information"
    }
  }
}
```

The level name is parsed case-insensitively as a Serilog `LogEventLevel`. An unsupported value throws `InvalidOperationException` during startup.

## Create an early bootstrap logger

```csharp
await LoggerConfigurationExtension.BuildBootstrapLoggerAndSetGlobally(
    DeployEnvironment.Development);
```

`BuildBootstrapLoggerAndSetGlobally()` creates a verbose logger with asynchronous console output and rolling file output, assigns it to `Log.Logger`, and writes a bootstrap event. Use it before the application's complete configuration is available. The `DeployEnvironment` argument is accepted for API compatibility; this method does not branch on its value.

Because it replaces the global logger, call it once. The caller remains responsible for closing and flushing Serilog during application shutdown.

## Log file location

The file name is `log-.log`; the file sink inserts the rolling date. Set the `LOG_PATH` environment variable to choose its directory. Without an override, the shared log-path utility selects a writable location for Azure App Service, GitHub Actions, containers, or the application base directory. The resolved path is cached for the process and its directory is created before the sink is configured.

Both setup methods write the resolved log path to standard output. Ensure the chosen directory is writable and mounted persistently when logs must survive container or instance replacement.

## Synchronous entry points

`BuildBootstrapLoggerAndSetGloballySync()` and `ConfigureLoggerSync()` block until their asynchronous counterparts finish. They are intended for startup entry points that cannot be asynchronous; prefer the `ValueTask` methods when the caller can await.
