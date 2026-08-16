using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using ManualLogSource = BepInEx.Logging.ManualLogSource;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Logging;

/// <summary>
/// Bridges M.E. <see cref="ILogger{T}"/> into BepInEx logging (console +
/// LogOutput.log). Level mapping: Trace/Debug → LogDebug, Information → LogInfo,
/// Warning → LogWarning, Error → LogError, Critical → LogFatal. The
/// configurable minimum level is enforced here (the provider owns the bridge,
/// so the BepInEx sink never sees a suppressed message).
/// </summary>
public sealed class BepInExLoggerProvider(ManualLogSource source, IOptionsMonitor<LoggingOptions> options) : ILoggerProvider
{
	private readonly ManualLogSource _source = source ?? throw new ArgumentNullException(nameof(source));
	private readonly IOptionsMonitor<LoggingOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly Dictionary<string, BepInExLogger> _loggers = [];

	public ILogger CreateLogger(string categoryName)
	{
		lock (_loggers)
		{
			if (!_loggers.TryGetValue(categoryName, out var logger))
			{
				logger = new BepInExLogger(this, categoryName);
				_loggers.Add(categoryName, logger);
			}
			return logger;
		}
	}

	public void Dispose()
	{
		// BepInEx owns the log source; nothing to release here.
	}

	private sealed class BepInExLogger(BepInExLoggerProvider provider, string category) : ILogger
	{
		private readonly BepInExLoggerProvider _provider = provider;
		private readonly string _category = category;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			var message = $"[{_category}] {formatter(state, exception)}";
			if (exception is not null)
			{
				message += Environment.NewLine + exception;
			}

			switch (logLevel)
			{
				case LogLevel.Trace:
				case LogLevel.Debug:
					_provider._source.LogDebug(message);
					break;
				case LogLevel.Information:
					_provider._source.LogInfo(message);
					break;
				case LogLevel.Warning:
					_provider._source.LogWarning(message);
					break;
				case LogLevel.Error:
					_provider._source.LogError(message);
					break;
				case LogLevel.Critical:
					_provider._source.LogFatal(message);
					break;
			}
		}

		public bool IsEnabled(LogLevel logLevel) =>
			logLevel != LogLevel.None && logLevel >= _provider._options.CurrentValue.MinimumLevel;

		public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
	}
}
