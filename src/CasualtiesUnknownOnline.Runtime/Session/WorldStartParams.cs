using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// World-start parameters captured by the host at run start and applied by
/// guests before their own world generation. The game's world gen is fully
/// non-deterministic (no seed, Unity global Random everywhere) — restoring the
/// host's Random.state plus run settings is the only way to produce the same
/// world on both sides (see docs/game-internals.md).
/// </summary>
public sealed class WorldStartParams
{
	// NOTE: plain set, not init — net48 lacks IsExternalInit.
	public byte[] RandomState { get; set; } = [];

	public byte BiomeOverride { get; set; }

	public byte BiomeDepth { get; set; }

	public int TotalTraveled { get; set; }

	public bool LoadedRun { get; set; }

	public Dictionary<string, object>? RunSettings { get; set; }
}
