using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Soenneker.Enums.DeployEnvironment;
using Soenneker.Utils.Logger;
using Soenneker.Utils.LogPath;
using System;
using System.IO;

namespace Soenneker.Extensions.LoggerConfiguration;

/// <summary>
/// Provides extension methods for configuring and initializing Serilog logger instances with application-specific
/// settings.
/// </summary>
/// <remarks>This static class contains methods to set up Serilog logging for applications, including bootstrap
/// logger creation and configuration based on environment and application configuration. The methods are intended to be
/// used during application startup to ensure consistent logging behavior across the application lifecycle.</remarks>
public static class LoggerConfigurationExtension
{
    private static readonly AnsiConsoleTheme _theme = AnsiConsoleTheme.Code;

    // "log-.log" -> creates "log-20251116.log"
    private const string _fileName = "log-.log";

    /// <summary>
    /// Builds a Serilog bootstrap logger configuration for the specified deployment environment and sets it as the
    /// global logger instance.
    /// </summary>
    /// <remarks>This method initializes the global Serilog logger using a verbose log level and configures
    /// both console and file sinks. It should be called early in application startup to ensure that logging is
    /// available as soon as possible. The returned LoggerConfiguration can be further customized if needed.</remarks>
    /// <param name="deployEnvironment">The deployment environment for which to configure the logger. This value may influence logger settings or output
    /// destinations.</param>
    /// <returns>A Serilog.LoggerConfiguration instance representing the configured bootstrap logger.</returns>
    public static Serilog.LoggerConfiguration BuildBootstrapLoggerAndSetGlobally(DeployEnvironment deployEnvironment)
    {
        const LogEventLevel logLevel = LogEventLevel.Verbose;

        Serilog.LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration().MinimumLevel.Is(logLevel);

        string logPath = LogPathUtil.Get(_fileName)
                                    .ConfigureAwait(false)
                                    .GetAwaiter()
                                    .GetResult();

        Console.WriteLine($"[BuildBootstrapLoggerAndSetGlobally] Using log path: {logPath}");

        EnsureDirectoryExists(logPath);

        // 🟢 Console async, file direct (never dropped)
        loggerConfig.WriteTo.Async(w =>
                    {
                        w.Console(theme: _theme);
                    })
                    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);

        Log.Logger = loggerConfig.CreateBootstrapLogger();

        Log.Warning("[Bootstrap] Logger initialized at {Utc}", DateTimeOffset.UtcNow);

        return loggerConfig;
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        else
            Console.WriteLine($"[LoggerConfigurationExtension] WARN: Cannot determine directory for '{filePath}'");
    }

    /// <summary>
    /// Configures the specified Serilog logger with settings from the provided configuration source, including log
    /// level, output sinks, and file paths.
    /// </summary>
    /// <remarks>This method applies log level and output sink settings based on the provided configuration.
    /// It supports console and file logging, and ensures the log file directory exists before writing logs. The method
    /// is intended to be used as an extension method during logger setup in application startup code.</remarks>
    /// <param name="loggerConfig">The Serilog logger configuration to be updated with additional settings.</param>
    /// <param name="configuration">The configuration source containing logger settings such as log level, output options, and file paths. Cannot be
    /// null.</param>
    /// <returns>The updated Serilog logger configuration with applied settings from the configuration source.</returns>
    public static Serilog.LoggerConfiguration ConfigureLogger(this Serilog.LoggerConfiguration loggerConfig, IConfiguration configuration)
    {
        LoggerUtil.Init();

        LogEventLevel logLevel = LoggerUtil.SetLogLevelFromConfig(configuration);
        LoggingLevelSwitch levelSwitch = LoggerUtil.GetSwitch();
        levelSwitch.MinimumLevel = logLevel;

        loggerConfig.MinimumLevel.ControlledBy(levelSwitch)
                    .Enrich.FromLogContext();

        string logPath = LogPathUtil.Get(_fileName)
                                    .ConfigureAwait(false)
                                    .GetAwaiter()
                                    .GetResult();

        Console.WriteLine($"[ConfigureLogger] Using log path: {logPath}");

        EnsureDirectoryExists(logPath);

        if (configuration.GetValue<bool>("Log:Console"))
        {
            loggerConfig.WriteTo.Async(sinks =>
            {
                sinks.Console(theme: _theme, levelSwitch: levelSwitch);
            });
        }

        loggerConfig.WriteTo.File(logPath, levelSwitch: levelSwitch, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);

        Log.Warning("[ConfigureLogger] Logger initialized at {Utc}", DateTimeOffset.UtcNow);

        return loggerConfig;
    }
}