using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Core.Logging;

/// <summary>Broadcasts to multiple ILoggers (e.g. BepInEx console + CUO log file).</summary>
public sealed class CompositeLogger : ILogger
{
	private readonly ILogger[] _sinks;

	public CompositeLogger(params ILogger[] sinks) => _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));

	public void Info(string message)
	{
		foreach (var sink in _sinks)
			sink.Info(message);
	}

	public void Warning(string message)
	{
		foreach (var sink in _sinks)
			sink.Warning(message);
	}

	public void Error(string message)
	{
		foreach (var sink in _sinks)
			sink.Error(message);
	}
}
