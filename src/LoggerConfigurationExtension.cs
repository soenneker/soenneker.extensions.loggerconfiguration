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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Extensions.LoggerConfiguration;

/// <summary>
/// Provides extension methods for configuring and initializing Serilog loggers with application-specific settings,
/// including asynchronous and synchronous bootstrap and configuration routines.
/// </summary>
/// <remarks>This static class offers both asynchronous and synchronous methods to set up Serilog logging for
/// applications, supporting configuration from environment and configuration sources. It is intended to simplify logger
/// initialization during application startup, including scenarios where asynchronous initialization is preferred but
/// synchronous fallbacks are required. The methods in this class handle log file path resolution, log level
/// configuration, and output to both console and file targets as appropriate.</remarks>
public static class LoggerConfigurationExtension
{
    private static readonly AnsiConsoleTheme _theme = AnsiConsoleTheme.Code;

    // "log-.log" -> creates "log-20251116.log"
    private const string _fileName = "log-.log";

    // Cache once per process; resolves on first use.
    private static readonly Lazy<Task<string>> _logPathTask = new(static () => LogPathUtil.Get(_fileName));

    /// <summary>
    /// Async-first bootstrap logger creation (preferred).
    /// </summary>
    public static async ValueTask<Serilog.LoggerConfiguration> BuildBootstrapLoggerAndSetGlobally(DeployEnvironment deployEnvironment)
    {
        const LogEventLevel logLevel = LogEventLevel.Verbose;

        Serilog.LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration().MinimumLevel.Is(logLevel);

        string logPath = await GetOrCreateLogPath()
            .NoSync();

        Console.WriteLine($"[BuildBootstrapLoggerAndSetGloballyAsync] Using log path: {logPath}");

        // Console async, file direct (never dropped)
        loggerConfig.WriteTo.Async(a => a.Console(theme: _theme))
                    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);

        Log.Logger = loggerConfig.CreateBootstrapLogger();
        Log.Warning("[Bootstrap] Logger initialized at {Utc}", DateTimeOffset.UtcNow);

        return loggerConfig;
    }

    /// <summary>
    /// Sync wrapper for startup call sites that can't be async.
    /// </summary>
    public static Serilog.LoggerConfiguration BuildBootstrapLoggerAndSetGloballySync(DeployEnvironment deployEnvironment) =>
        BuildBootstrapLoggerAndSetGlobally(deployEnvironment)
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Async-first configuration (preferred).
    /// </summary>
    public static async ValueTask<Serilog.LoggerConfiguration> ConfigureLogger(this Serilog.LoggerConfiguration loggerConfig, IConfiguration configuration)
    {
        LoggerUtil.Init();

        LogEventLevel logLevel = LoggerUtil.SetLogLevelFromConfig(configuration);
        LoggingLevelSwitch levelSwitch = LoggerUtil.GetSwitch();
        levelSwitch.MinimumLevel = logLevel;

        loggerConfig.MinimumLevel.ControlledBy(levelSwitch)
                    .Enrich.FromLogContext();

        string logPath = await GetOrCreateLogPath()
            .NoSync();

        Console.WriteLine($"[ConfigureLoggerAsync] Using log path: {logPath}");

        bool consoleEnabled = configuration.GetValue("Log:Console", defaultValue: false);

        if (consoleEnabled)
            loggerConfig.WriteTo.Async(a => a.Console(theme: _theme, levelSwitch: levelSwitch));

        loggerConfig.WriteTo.File(logPath, levelSwitch: levelSwitch, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);

        Log.Warning("[ConfigureLoggerAsync] Logger initialized at {Utc}", DateTimeOffset.UtcNow);

        return loggerConfig;
    }

    /// <summary>
    /// Sync wrapper for startup call sites that can't be async.
    /// </summary>
    public static Serilog.LoggerConfiguration ConfigureLoggerSync(this Serilog.LoggerConfiguration loggerConfig, IConfiguration configuration) => loggerConfig
        .ConfigureLogger(configuration)
        .GetAwaiter()
        .GetResult();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<string> GetOrCreateLogPath()
    {
        Task<string> task = _logPathTask.Value;

        if (task.IsCompletedSuccessfully)
        {
            string path = task.Result;
            EnsureDirectoryExists(path);
            return new ValueTask<string>(path);
        }

        // Slow path: avoid local function; call a private method.
        return new ValueTask<string>(AwaitAndEnsure(task));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<string> AwaitAndEnsure(Task<string> task)
    {
        string path = await task.ConfigureAwait(false);
        EnsureDirectoryExists(path);
        return path;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureDirectoryExists(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);

        if (dir is { Length: > 0 })
        {
            Directory.CreateDirectory(dir);
        }
        else
        {
            Console.WriteLine($"[LoggerConfigurationExtension] WARN: Cannot determine directory for '{filePath}'");
        }
    }
}