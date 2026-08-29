using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// World-start parameters captured by the host at run start and applied by
/// guests before their own world generation. The game's world gen is fully
/// non-deterministic (no seed, Unity global Random everywhere) — restoring the
/// host's Random.state plus run settings is the only way to produce the same
/// world on both sides (see docs/game-internals.md).
/// </summary>
public sealed class WorldStartParams
{
	public byte[] RandomState { get; init; } = [];

	public byte BiomeOverride { get; init; }

	public byte BiomeDepth { get; init; }

	public int TotalTraveled { get; init; }

	public bool LoadedRun { get; init; }

	public Dictionary<string, object>? RunSettings { get; init; }

	/// <summary>
	/// True when the world is a tutorial — the guest must enter via StartTutorial
	/// (it nulls runSettings itself, PreRunScript.cs:307-314). WorldGeneration.
	/// OverrideSceneType.Tutorial == 1 (the adapter owns the only other read of
	/// the enum; the Runtime never references game assemblies).
	/// </summary>
	public bool IsTutorial => BiomeOverride == 1;

}
