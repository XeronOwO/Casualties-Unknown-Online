using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A logger factory that records every rendered line together with its level and
/// category, so a suite can assert on what a production type WROTE — not only on
/// what it returned. It exists for the observability contracts (one account line
/// per cut and per restore, with the per-domain counts in it): those are facts
/// about the log, and a suite that asserts them through the return value would
/// pass while the log stayed silent.
///
/// It is deliberately not a full logger: scopes, event ids and providers are
/// ignored, which is all this repository's tests need.
/// </summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
	internal List<(LogLevel Level, string Category, string Message)> Entries { get; } = [];

	/// <summary>Rendered messages at <paramref name="level"/> from every category whose name contains <paramref name="categoryFragment"/>.</summary>
	internal IReadOnlyList<string> Messages(LogLevel level, string categoryFragment) =>
		[.. Entries
			.Where(entry => entry.Level == level && entry.Category.Contains(categoryFragment, StringComparison.Ordinal))
			.Select(entry => entry.Message)];

	public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

	public void AddProvider(ILoggerProvider provider)
	{
		// No providers: the recorder IS the sink.
	}

	public void Dispose()
	{
		// Nothing to release.
	}

	private sealed class RecordingLogger(
		string category,
		List<(LogLevel Level, string Category, string Message)> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			entries.Add((logLevel, category, formatter(state, exception)));
	}
}
