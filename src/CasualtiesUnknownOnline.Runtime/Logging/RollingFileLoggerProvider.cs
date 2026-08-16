using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Logging;

/// <summary>
/// Minecraft-style file logging: writes to <c>&lt;logDirectory&gt;/latest.log</c>;
/// on startup the previous session's log is compressed to a timestamped
/// <c>yyyy-MM-dd-N.log.gz</c>. Pure BCL — no BepInEx dependency, so it also
/// serves a future dedicated server. Never throws — logging must not crash.
/// The configurable minimum level is enforced here so hot reload affects the
/// file sink without rebuilding the logging factory.
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
	private const string LatestLogFileName = "latest.log";
	private const string ArchiveSuffix = ".log.gz";

	private readonly object _sync = new();
	private readonly string _directory;
	private readonly string? _legacyLogPath;
	private readonly IOptionsMonitor<LoggingOptions> _options;
	private readonly Dictionary<string, RollingFileLogger> _loggers = [];
	private StreamWriter? _writer;
	private bool _disabled;

	/// <summary>
	/// <paramref name="legacyLogPath"/>: an old single-file log (e.g. the
	/// pre-rollover BepInEx/CUO.log) archived into <paramref name="logDirectory"/>
	/// once, so history is not lost during the cutover.
	/// </summary>
	public RollingFileLoggerProvider(string logDirectory, string? legacyLogPath,
		IOptionsMonitor<LoggingOptions> options)
	{
		_directory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
		_legacyLogPath = legacyLogPath;
		_options = options ?? throw new ArgumentNullException(nameof(options));

		try
		{
			Directory.CreateDirectory(_directory);
			ArchiveLegacyLog();
			RotatePreviousLog();
			// FileShare.Read lets an external tailer watch latest.log; append
			// mode preserves a crash session's tail if compression failed.
			_writer = new StreamWriter(
				new FileStream(Path.Combine(_directory, LatestLogFileName), FileMode.Append, FileAccess.Write, FileShare.Read),
				new UTF8Encoding(false))
			{ AutoFlush = true };
		}
		catch
		{
			_disabled = true;
		}
	}

	internal bool IsEnabled => !_disabled;

	public ILogger CreateLogger(string categoryName)
	{
		lock (_sync)
		{
			if (!_loggers.TryGetValue(categoryName, out var logger))
			{
				logger = new RollingFileLogger(this, categoryName);
				_loggers.Add(categoryName, logger);
			}
			return logger;
		}
	}

	public void Dispose()
	{
		lock (_sync)
		{
			_writer?.Dispose();
			_writer = null;
		}
	}

	private void ArchiveLegacyLog()
	{
		if (_legacyLogPath is null || !File.Exists(_legacyLogPath))
		{
			return;
		}

		try
		{
			CompressToGz(_legacyLogPath, FindAvailableArchiveName(File.GetLastWriteTime(_legacyLogPath)));
			File.Delete(_legacyLogPath);
		}
		catch
		{
			// Best-effort: keep the legacy file untouched.
		}
	}

	private void RotatePreviousLog()
	{
		var latest = Path.Combine(_directory, LatestLogFileName);
		if (!File.Exists(latest))
		{
			return;
		}

		try
		{
			if (new FileInfo(latest).Length > 0)
			{
				CompressToGz(latest, FindAvailableArchiveName(DateTime.Now));
			}

			File.Delete(latest);
		}
		catch
		{
			// Locked/corrupt: leave latest.log, this session appends to it.
		}
	}

	private string FindAvailableArchiveName(DateTime date)
	{
		for (var n = 1; n < 100_000; n++)
		{
			var path = Path.Combine(_directory, $"{date:yyyy-MM-dd}-{n}{ArchiveSuffix}");
			if (!File.Exists(path))
			{
				return path;
			}
		}
		return Path.Combine(_directory, $"{date:yyyy-MM-dd}-{Environment.TickCount}{ArchiveSuffix}");
	}

	private static void CompressToGz(string sourcePath, string targetPath)
	{
		using var input = File.OpenRead(sourcePath);
		using var output = File.Create(targetPath);
		using var gzip = new GZipStream(output, CompressionLevel.Optimal);
		input.CopyTo(gzip);
	}

	private void WriteLine(string line)
	{
		try
		{
			lock (_sync)
			{
				_writer?.WriteLine(line);
			}
		}
		catch
		{
			_disabled = true;
		}
	}

	private sealed class RollingFileLogger(RollingFileLoggerProvider provider, string category) : ILogger
	{
		private readonly RollingFileLoggerProvider _provider = provider;
		private readonly string _category = category;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			// The default formatter does NOT include the exception — append it
			// explicitly (ToString includes the stack trace).
			var message = formatter(state, exception);
			if (exception is not null)
			{
				message += Environment.NewLine + exception;
			}

			_provider.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{LevelCode(logLevel)}] [{_category}] {message}");
		}

		public bool IsEnabled(LogLevel logLevel) =>
			logLevel != LogLevel.None
			&& _provider.IsEnabled
			&& logLevel >= _provider._options.CurrentValue.MinimumLevel;

		public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

		private static string LevelCode(LogLevel level) => level switch
		{
			LogLevel.Trace => "TRC",
			LogLevel.Debug => "DBG",
			LogLevel.Information => "INF",
			LogLevel.Warning => "WRN",
			LogLevel.Error => "ERR",
			LogLevel.Critical => "CRT",
			_ => "???"
		};
	}
}
