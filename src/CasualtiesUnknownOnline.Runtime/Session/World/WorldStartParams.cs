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

	/// <summary>
	/// The game's <c>WorldGeneration.debugStartDepth</c> when this boundary was captured — a
	/// debug-console value the game's own first-layer test reads
	/// (<c>WorldGeneration.cs:1891</c>), so a run started at a debug depth is NOT the run's
	/// first layer and the game hands out no starting supplies on it. The adapter owns the
	/// field (it is the only layer that may look at a game member); this carries the value to
	/// the peers so both sides reach the same verdict.
	/// </summary>
	public byte DebugStartDepth { get; init; }

	public int TotalTraveled { get; init; }

	public bool LoadedRun { get; init; }

	public Dictionary<string, object>? RunSettings { get; init; }

	/// <summary>
	/// The run's accumulated loot multiplier when this boundary was captured
	/// (<c>WorldGeneration.lootRarityMultiplier</c>). It is a world-defining input,
	/// not decoration: the layer's loot distribution is scaled by it, so a guest
	/// that generates with the game's fresh 1.0 instead of the run's value builds a
	/// different layer than the host. Null = the capture point had no live world
	/// yet (the run-start entry), which is the game's own starting value.
	/// </summary>
	public float? LootRarityMultiplier { get; init; }

	/// <summary>The same for the trap multiplier (<c>WorldGeneration.trapRarityMultiplier</c>).</summary>
	public float? TrapRarityMultiplier { get; init; }

	/// <summary>
	/// True when the world is a tutorial — the guest must enter via StartTutorial
	/// (it nulls runSettings itself, PreRunScript.cs:307-314). WorldGeneration.
	/// OverrideSceneType.Tutorial == 1 (the adapter owns the only other read of
	/// the enum; the Runtime never references game assemblies).
	/// </summary>
	public bool IsTutorial => BiomeOverride == 1;

}
