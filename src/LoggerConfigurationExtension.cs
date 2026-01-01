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

public static class LoggerConfigurationExtension
{
    private static readonly AnsiConsoleTheme _theme = AnsiConsoleTheme.Code;

    // "log-.log" -> creates "log-20251116.log"
    private const string _fileName = "log-.log";

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

        Log.Warning("[Bootstrap] Logger initialized at {Utc}", DateTime.UtcNow);

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

        Log.Warning("[ConfigureLogger] Logger initialized at {Utc}", DateTime.UtcNow);

        return loggerConfig;
    }
}