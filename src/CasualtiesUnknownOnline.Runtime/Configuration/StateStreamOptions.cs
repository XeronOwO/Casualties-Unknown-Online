using System;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// State-stream cadence configuration. One frequency drives both state
/// streams that were hard-coded at 20 Hz: the player entity stream
/// (<c>EntitySyncService</c> host broadcast + guest report) and the
/// host-authoritative enemy stream (<c>EnemySyncService</c>). The value is
/// normalized into the supported 1-60 Hz band here so every consumer reads
/// one safe number; the Game Adapter's 1 Hz character snapshot is deliberately
/// NOT part of this option (it is the full-fact fallback, not a state stream).
/// </summary>
public sealed class StateStreamOptions
{
	public const int DefaultStateStreamHz = 20;
	public const int MinStateStreamHz = 1;
	public const int MaxStateStreamHz = 60;

	private int _stateStreamHz = DefaultStateStreamHz;

	/// <summary>Snapshots per second (clamped to 1-60).</summary>
	public int StateStreamHz
	{
		get => _stateStreamHz;
		set => _stateStreamHz = Math.Max(MinStateStreamHz, Math.Min(MaxStateStreamHz, value));
	}

	/// <summary>The send throttle interval in seconds for the configured cadence.</summary>
	public float SendIntervalSeconds => 1f / _stateStreamHz;
}
