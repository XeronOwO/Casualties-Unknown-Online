using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Diagnostics;

/// <summary>
/// Opt-in hot-path timing for the CUO update pump. When
/// <see cref="LatencyOptions.Enabled"/> is false, <c>Measure</c> returns null
/// (or invokes the action directly in the one-line form), so normal play sees
/// only a disabled branch. When enabled, it aggregates call count / total /
/// max per named domain and emits one summary line per name on the configured
/// log interval.
/// </summary>
public sealed class LatencyInstrumentation(
	IOptionsMonitor<LatencyOptions> options,
	ILogger<LatencyInstrumentation> log)
{
	private readonly IOptionsMonitor<LatencyOptions> _options = options;
	private readonly ILogger<LatencyInstrumentation> _log = log;
	private readonly object _sync = new();
	private readonly Dictionary<string, Accumulator> _samples = [];
	private long _nextLogMs;

	/// <summary>
	/// Measure one named call with an explicit <see cref="IDisposable"/> scope.
	/// Returns null while instrumentation is disabled (no allocation).
	/// </summary>
	public IDisposable? Measure(string name)
	{
		if (!_options.CurrentValue.Enabled)
		{
			return null;
		}

		return new Scope(this, name, Stopwatch.StartNew());
	}

	/// <summary>Measure one named call without an explicit scope.</summary>
	public void Measure(string name, Action action)
	{
		if (action is null)
		{
			throw new ArgumentNullException(nameof(action));
		}

		if (!_options.CurrentValue.Enabled)
		{
			action();
			return;
		}

		var stopwatch = Stopwatch.StartNew();
		try
		{
			action();
		}
		finally
		{
			Record(name, stopwatch.Elapsed.TotalMilliseconds);
		}
	}

	/// <summary>
	/// Flush aggregated samples when the configured log interval has elapsed.
	/// Called at the end of the Game Adapter's update pump; only logs while the
	/// feature is enabled.
	/// </summary>
	public void Flush()
	{
		var current = _options.CurrentValue;
		if (!current.Enabled)
		{
			return;
		}

		var intervalMs = (long)(Math.Max(0.0, current.LogIntervalSeconds) * 1000.0);
		var now = Environment.TickCount;
		Sample[]? snapshot;
		lock (_sync)
		{
			if (now < _nextLogMs)
			{
				return;
			}

			_nextLogMs = now + Math.Max(1, intervalMs);
			if (_samples.Count == 0)
			{
				return;
			}

			snapshot = [.. _samples
				.Select(pair => new Sample(
					pair.Key,
					pair.Value.Calls,
					pair.Value.TotalMs,
					pair.Value.TotalMs / Math.Max(1, pair.Value.Calls),
					pair.Value.MaxMs))
				.OrderByDescending(sample => sample.TotalMs)];
			_samples.Clear();
		}

		foreach (var sample in snapshot!)
		{
			_log.LogInformation(
				"[Latency] {Name}: calls={Calls} total={Total:F2}ms avg={Avg:F2}ms max={Max:F2}ms",
				sample.Name, sample.Calls, sample.TotalMs, sample.AverageMs, sample.MaxMs);
		}
	}

	/// <summary>Test seam: current unflushed sample data.</summary>
	internal IReadOnlyDictionary<string, Sample> CurrentSamples
	{
		get
		{
			lock (_sync)
			{
				return _samples.ToDictionary(
					pair => pair.Key,
					pair => new Sample(
						pair.Key,
						pair.Value.Calls,
						pair.Value.TotalMs,
						pair.Value.TotalMs / Math.Max(1, pair.Value.Calls),
						pair.Value.MaxMs));
			}
		}
	}

	private void Record(string name, double elapsedMs)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			name = "(unnamed)";
		}

		lock (_sync)
		{
			if (!_samples.TryGetValue(name, out var accumulator))
			{
				accumulator = new Accumulator();
				_samples[name] = accumulator;
			}

			accumulator.Calls++;
			accumulator.TotalMs += elapsedMs;
			if (elapsedMs > accumulator.MaxMs)
			{
				accumulator.MaxMs = elapsedMs;
			}
		}
	}

	/// <summary>One named timing aggregate.</summary>
	public sealed record Sample(
		string Name,
		int Calls,
		double TotalMs,
		double AverageMs,
		double MaxMs);

	private sealed class Accumulator
	{
		internal int Calls;
		internal double TotalMs;
		internal double MaxMs;
	}

	private sealed class Scope(LatencyInstrumentation owner, string name, Stopwatch stopwatch) : IDisposable
	{
		private LatencyInstrumentation? _owner = owner;
		private readonly string _name = name;

		public void Dispose()
		{
			_owner?.Record(_name, stopwatch.Elapsed.TotalMilliseconds);
			_owner = null;
		}
	}
}
