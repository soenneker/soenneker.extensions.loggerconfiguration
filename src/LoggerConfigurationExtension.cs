using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Soenneker.Enums.DeployEnvironment;
using Soenneker.Utils.Logger;
using System.IO;
using Soenneker.Utils.Runtime;

namespace Soenneker.Extensions.LoggerConfiguration;

/// <summary>
/// A set of useful Serilog LoggerConfiguration extension methods
/// </summary>
public static class LoggerConfigurationExtension
{
    /// <summary>
    /// Called before Configuration is built.
    /// Verbose is used during startup
    /// </summary>
    public static Serilog.LoggerConfiguration BuildBootstrapLoggerAndSetGlobally(DeployEnvironment deployEnvironment)
    {
        var loggerConfig = new Serilog.LoggerConfiguration();

        const LogEventLevel logLevel = LogEventLevel.Verbose;

        loggerConfig.MinimumLevel.Is(logLevel);

        loggerConfig.WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Code, restrictedToMinimumLevel: logLevel));

        string logPath = GetPathFromEnvironment(deployEnvironment);

        string? directoryName = Path.GetDirectoryName(logPath);

        if (Directory.Exists(logPath))
        {
            if (File.Exists(logPath))
                File.Delete(logPath);
        }
        else
        {
            Directory.CreateDirectory(directoryName!);
        }

        loggerConfig.WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: logLevel, rollOnFileSizeLimit: true),
            500);

        Log.Logger = loggerConfig.CreateBootstrapLogger();

        return loggerConfig;
    }

    /// <summary>
    /// Should be called within UseSerilog().
    /// Reconfigures global static Log.Logger
    /// </summary>
    public static Serilog.LoggerConfiguration ConfigureLogger(this Serilog.LoggerConfiguration loggerConfig, IConfiguration configuration)
    {
        LoggerUtil.Init();

        LogEventLevel logLevel = LoggerUtil.SetLogLevelFromConfig(configuration);

        loggerConfig.MinimumLevel.ControlledBy(LoggerUtil.GetSwitch());
        loggerConfig.MinimumLevel.Is(logLevel);

        loggerConfig.Enrich.FromLogContext();

        var addConsole = configuration.GetValue<bool>("Log:Console");

        if (addConsole)
            loggerConfig.WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Code, restrictedToMinimumLevel: logLevel, levelSwitch: LoggerUtil.GetSwitch()));

        DeployEnvironment? deployEnvironment = DeployEnvironment.FromName(configuration.GetValue<string>("Environment"));

        string logPath = GetPathFromEnvironment(deployEnvironment);

        loggerConfig.WriteTo.Async(
            a => a.File(logPath, rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: logLevel, levelSwitch: LoggerUtil.GetSwitch(),
                rollOnFileSizeLimit: true), 500);

        return loggerConfig;
    }

    public static string GetPathFromEnvironment(DeployEnvironment env)
    {
        const string fileName = "log.log";

        // Use the persistent /home mount on Linux, D:\home on Windows
        string root = RuntimeUtil.IsWindows()
            ? Path.Combine("D:", "home")
            : "/home";

        if (env == DeployEnvironment.Test)
            return Path.Combine("logs", fileName);      // runs locally in CI, etc.

        return Path.Combine(root, "LogFiles", fileName);
    }
}