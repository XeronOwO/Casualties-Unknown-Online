namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// Diagnostics configuration for the opt-in hot-path latency instrumentation.
/// Off by default so normal play pays only a disabled branch; when enabled,
/// <c>LatencyInstrumentation</c> aggregates the measured domain update calls and
/// emits one summary line per name at the configured log interval.
/// </summary>
public sealed class LatencyOptions
{
	/// <summary>True = collect per-domain frame/call timing for the main CUO update pump.</summary>
	public bool Enabled { get; set; }

	/// <summary>Minimum seconds between aggregated latency log lines. Values below 0.1 are clamped by the plugin factory.</summary>
	public double LogIntervalSeconds { get; set; } = 1.0;
}
