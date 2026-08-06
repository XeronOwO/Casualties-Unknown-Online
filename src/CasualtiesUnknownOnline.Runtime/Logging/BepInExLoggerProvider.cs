using System;
using System.Collections.Generic;
using ManualLogSource = BepInEx.Logging.ManualLogSource;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Logging;

/// <summary>
/// Bridges M.E. <see cref="ILogger{T}"/> into BepInEx logging (console +
/// LogOutput.log). Level mapping: Trace/Debug → LogDebug, Information → LogInfo,
/// Warning → LogWarning, Error → LogError, Critical → LogFatal.
/// </summary>
public sealed class BepInExLoggerProvider(ManualLogSource source) : ILoggerProvider
{
	private readonly ManualLogSource _source = source ?? throw new ArgumentNullException(nameof(source));
	private readonly Dictionary<string, BepInExLogger> _loggers = [];

	public ILogger CreateLogger(string categoryName)
	{
		lock (_loggers)
		{
			if (!_loggers.TryGetValue(categoryName, out var logger))
			{
				logger = new BepInExLogger(_source, categoryName);
				_loggers.Add(categoryName, logger);
			}
			return logger;
		}
	}

	public void Dispose()
	{
		// BepInEx owns the log source; nothing to release here.
	}

	private sealed class BepInExLogger(ManualLogSource source, string category) : ILogger
	{
		private readonly ManualLogSource _source = source;
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
				message += "\n" + exception;
			}

			switch (logLevel)
			{
				case LogLevel.Trace:
				case LogLevel.Debug:
					_source.LogDebug(message);
					break;
				case LogLevel.Information:
					_source.LogInfo(message);
					break;
				case LogLevel.Warning:
					_source.LogWarning(message);
					break;
				case LogLevel.Error:
					_source.LogError(message);
					break;
				case LogLevel.Critical:
					_source.LogFatal(message);
					break;
			}
		}

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
	}
}
