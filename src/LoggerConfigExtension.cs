using System;
using System.IO;
using Hangfire.Console.Extensions.Serilog;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Soenneker.Enums.DeployEnvironment;
using Soenneker.Utils.Logger;

namespace Soenneker.Extensions.LoggerConfiguration;

/// <summary>
/// A set of useful Serilog LoggerConfiguration extension methods
/// </summary>
public static class LoggerConfigExtension
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

        loggerConfig.WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: logLevel), 500);

        Log.Logger = loggerConfig.CreateBootstrapLogger();

        return loggerConfig;
    }

    /// <summary>
    /// Should be called within UseSerilog().
    /// Reconfigures global static Log.Logger
    /// </summary>
    public static Serilog.LoggerConfiguration ConfigureLogger(this Serilog.LoggerConfiguration loggerConfig, IConfigurationRoot configRoot)
    {
        LoggerUtil.Init();

        LogEventLevel logLevel = LoggerUtil.SetLogLevelFromConfigRoot(configRoot);

        loggerConfig.MinimumLevel.ControlledBy(LoggerUtil.GetSwitch());
        loggerConfig.MinimumLevel.Is(logLevel);

        loggerConfig.Enrich.FromLogContext();

        var addConsole = configRoot.GetValue<bool>("Log:Console");

        if (addConsole)
            loggerConfig.WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Code, restrictedToMinimumLevel: logLevel, levelSwitch: LoggerUtil.GetSwitch()));

        DeployEnvironment? deployEnvironment = DeployEnvironment.FromName(configRoot.GetValue<string>("Environment"));

        //var levels = new Levels(configRoot);

        //if (levels.Dictionary != null)
        //{
        //    foreach (KeyValuePair<string, LogEventLevel> level in levels.Dictionary)
        //    {
        //        loggerConfig.MinimumLevel.Override(level.Key, level.Value);
        //    }
        //}

        string logPath = GetPathFromEnvironment(deployEnvironment);

        loggerConfig.WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: logLevel, levelSwitch: LoggerUtil.GetSwitch()), 500);

        return loggerConfig;
    }

    public static string GetPathFromEnvironment(DeployEnvironment deployEnvironment)
    {
        string path;

        switch (deployEnvironment.Name)
        {
            case nameof(DeployEnvironment.Development):
            case nameof(DeployEnvironment.Staging):
            case nameof(DeployEnvironment.Production):
                path = @"D:\home\LogFiles\log.log";
                break;
            case nameof(DeployEnvironment.Test):
                path = Path("logs", "log.log");
                break;
            default:
                path = Path("logs", "log.log");
                break;
        }

        return path;
    }
    
    /// <summary>
    /// Adds the Hangfire sink unless the config says that we shouldn't
    /// </summary>
    public static void AddHangfire(this Serilog.LoggerConfiguration loggerConfig, IConfigurationRoot configRoot)
    {
        var enabled = configRoot.GetValue<bool>("Hangfire:Enabled");

        if (!enabled)
            return;

        LogEventLevel logEventLevel = LoggerUtil.GetLogEventLevelFromConfigRoot(configRoot);

        loggerConfig.Enrich.WithHangfireContext();
        loggerConfig.WriteTo.Async(a => a.Hangfire(restrictedToMinimumLevel: logEventLevel));
    }

    /// <summary>
    /// Adds the Application Insights sink unless the config says that we shouldn't
    /// </summary>
    public static void AddApplicationInsightsLogging(this Serilog.LoggerConfiguration loggerConfiguration,
        IServiceProvider services, IConfigurationRoot configRoot)
    {
        var enabled = configRoot.GetValue<bool>("Azure:AppInsights:Enable");

        if (!enabled)
            return;

        LogEventLevel logEventLevel = LoggerUtil.GetLogEventLevelFromConfigRoot(configRoot);

        loggerConfiguration.WriteTo.Async(a => a.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces, logEventLevel));
    }
}
