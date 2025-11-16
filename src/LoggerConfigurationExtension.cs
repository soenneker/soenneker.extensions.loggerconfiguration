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
/// A set of useful Serilog LoggerConfiguration extension methods
/// </summary>
public static class LoggerConfigurationExtension
{
    private const int _buffer = 10000;
    private static readonly AnsiConsoleTheme _theme = AnsiConsoleTheme.Code;
    private const string _fileName = "log.log";

    /// <summary>
    /// Called before Configuration is built.
    /// Verbose is used during startup
    /// </summary>
    public static Serilog.LoggerConfiguration BuildBootstrapLoggerAndSetGlobally(DeployEnvironment deployEnvironment)
    {
        const LogEventLevel logLevel = LogEventLevel.Verbose;

        Serilog.LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration()
                                                   .MinimumLevel.Is(logLevel);

        string logPath = LogPathUtil.Get(_fileName).GetAwaiter().GetResult();

        Console.WriteLine($"[BuildBootstrapLoggerAndSetGlobally] Using log path: {logPath}");

        EnsureDirectoryExists(logPath);
        DeleteIfExists(logPath);

        // Async only for console (lightweight); file sink is direct so it can't be dropped
        loggerConfig.WriteTo.Async(w =>
        {
            w.Console(theme: _theme);
        }, _buffer);

        loggerConfig.WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: true);

        Log.Logger = loggerConfig.CreateBootstrapLogger();
        return loggerConfig;
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        // Path.GetDirectoryName never returns null for a rooted path
        string dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir); // idempotent; no Exists check needed
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    /// <summary>
    /// Should be called from <c>UseSerilog()</c>.  
    /// Re-configures the global static <see cref="Log.Logger"/>.
    /// </summary>
    public static Serilog.LoggerConfiguration ConfigureLogger(this Serilog.LoggerConfiguration loggerConfig, IConfiguration configuration)
    {
        // Initialise helpers
        LoggerUtil.Init();

        // Determine runtime log level & switch
        LogEventLevel logLevel = LoggerUtil.SetLogLevelFromConfig(configuration);
        LoggingLevelSwitch levelSwitch = LoggerUtil.GetSwitch();
        levelSwitch.MinimumLevel = logLevel;

        loggerConfig
            .MinimumLevel.ControlledBy(levelSwitch) // single source of truth
            .Enrich.FromLogContext();

        // Resolve log path once (outside async)
        string logPath = LogPathUtil.Get(_fileName).GetAwaiter().GetResult();

        Console.WriteLine($"[ConfigureLogger] Using log path: {logPath}");

        EnsureDirectoryExists(logPath);

        if (configuration.GetValue<bool>("Log:Console"))
        {
            // Async only for console to avoid blocking; no file here to prevent queue overflow dropping logs
            loggerConfig.WriteTo.Async(sinks =>
            {
                sinks.Console(theme: _theme, levelSwitch: levelSwitch);
            }, bufferSize: _buffer);
        }

        // File sink is direct (non-async) so events can't be dropped by the async buffer
        loggerConfig.WriteTo.File(
            logPath,
            levelSwitch: levelSwitch,
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: true);

        return loggerConfig;
    }
}