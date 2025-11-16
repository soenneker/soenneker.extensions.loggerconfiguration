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
    private const int _buffer = 10000;
    private static readonly AnsiConsoleTheme _theme = AnsiConsoleTheme.Code;

    // "log-.log" -> creates "log-20251116.log"
    private const string _fileName = "log-.log";

    public static Serilog.LoggerConfiguration BuildBootstrapLoggerAndSetGlobally(DeployEnvironment deployEnvironment)
    {
        const LogEventLevel logLevel = LogEventLevel.Verbose;

        Serilog.LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration().MinimumLevel.Is(logLevel);

        string logPath = LogPathUtil.Get(_fileName)
                                    .GetAwaiter()
                                    .GetResult();

        Console.WriteLine($"[BuildBootstrapLoggerAndSetGlobally] Using log path: {logPath}");

        EnsureDirectoryExists(logPath);
        TryTestWrite(logPath, "[Bootstrap]");

        // 🟢 Console async, file direct (never dropped)
        loggerConfig.WriteTo.Async(w =>
                    {
                        w.Console(theme: _theme);
                    }, _buffer)
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

    private static void TryTestWrite(string logPath, string prefix)
    {
        try
        {
            File.AppendAllText(logPath, $"{prefix} test write at {DateTime.UtcNow:o}{Environment.NewLine}");
            Console.WriteLine($"{prefix} Successfully wrote test line to '{logPath}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{prefix} FAILED to write test line to '{logPath}': {ex}");
        }
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
                                    .GetAwaiter()
                                    .GetResult();

        Console.WriteLine($"[ConfigureLogger] Using log path: {logPath}");

        EnsureDirectoryExists(logPath);
        TryTestWrite(logPath, "[ConfigureLogger]");

        loggerConfig.WriteTo.Async(sinks =>
        {
            if (configuration.GetValue<bool>("Log:Console"))
                sinks.Console(theme: _theme, levelSwitch: levelSwitch);
        }, _buffer);

        loggerConfig.WriteTo.File(logPath, levelSwitch: levelSwitch, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);

        Log.Warning("[ConfigureLogger] Logger initialized at {Utc}", DateTime.UtcNow);

        return loggerConfig;
    }
}