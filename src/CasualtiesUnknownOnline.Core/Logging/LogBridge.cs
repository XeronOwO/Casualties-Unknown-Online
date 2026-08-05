using System;

namespace CasualtiesUnknownOnline.Core.Logging;

/// <summary>
/// Host-agnostic global logger for CUO Core. The plugin entry calls
/// <see cref="Initialize"/> once with its logging implementation.
/// </summary>
public static class LogBridge
{
	private static ILogger _logger = NullLogger.Instance;

	public static ILogger Log => _logger;

	public static void Initialize(ILogger logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	private sealed class NullLogger : ILogger
	{
		public static readonly NullLogger Instance = new();

		public void Info(string message)
		{
		}

		public void Warning(string message)
		{
		}

		public void Error(string message)
		{
		}
	}
}
