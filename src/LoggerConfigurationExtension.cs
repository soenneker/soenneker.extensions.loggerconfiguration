using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Soenneker.Enums.DeployEnvironment;
using Soenneker.Utils.Logger;
using System.IO;
using Serilog.Core;
using Soenneker.Utils.LogPath;

namespace Soenneker.Extensions.LoggerConfiguration;

/// <summary>
/// A set of useful Serilog LoggerConfiguration extension methods
/// </summary>
public static class LoggerConfigurationExtension
{
    private const int _buffer = 500;
    private static readonly AnsiConsoleTheme _theme = AnsiConsoleTheme.Code;
    private const string _fileName = "log.log";

    /// <summary>
    /// Called before Configuration is built.
    /// Verbose is used during startup
    /// </summary>
    public static Serilog.LoggerConfiguration BuildBootstrapLoggerAndSetGlobally(DeployEnvironment deployEnvironment)
    {
        const LogEventLevel logLevel = LogEventLevel.Verbose;

        Serilog.LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration().MinimumLevel.Is(logLevel);

        string logPath = LogPathUtil.Get(_fileName).GetAwaiter().GetResult();

        EnsureDirectoryExists(logPath);
        DeleteIfExists(logPath);

        loggerConfig.WriteTo.Async(w =>
        {
            w.Console(theme: _theme, restrictedToMinimumLevel: logLevel);
            w.File(logPath, restrictedToMinimumLevel: logLevel, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);
        }, _buffer);

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

        loggerConfig.MinimumLevel.ControlledBy(levelSwitch) // single source of truth
                    .Enrich.FromLogContext();

        // Build sink pipeline once, wrapped in a single async queue
        loggerConfig.WriteTo.Async(sinks =>
        {
            // Console sink (optional)
            if (configuration.GetValue<bool>("Log:Console"))
            {
                sinks.Console(theme: _theme, levelSwitch: levelSwitch, restrictedToMinimumLevel: logLevel);
            }

            string logPath = LogPathUtil.Get(_fileName).GetAwaiter().GetResult();

            EnsureDirectoryExists(logPath);

            sinks.File(logPath, levelSwitch: levelSwitch, restrictedToMinimumLevel: logLevel, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);
        }, bufferSize: _buffer);

        return loggerConfig;
    }
}