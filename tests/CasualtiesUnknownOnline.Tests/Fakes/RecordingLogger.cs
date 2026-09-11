using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> stand-in that records what a suite asserts
/// on: the level and the rendered message. Scopes and event ids are ignored —
/// nothing in this repository's tests asserts on them.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
	internal List<(LogLevel Level, string Message)> Entries { get; } = [];

	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
		Entries.Add((logLevel, formatter(state, exception)));

	/// <summary>True when an error-level entry contains the fragment.</summary>
	internal bool HasError(string fragment) =>
		Entries.Exists(entry => entry.Level == LogLevel.Error && entry.Message.Contains(fragment, StringComparison.Ordinal));
}
