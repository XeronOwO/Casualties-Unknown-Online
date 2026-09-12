using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// The authoritative run identity and world-generation baseline owned by the
/// kernel. The raw <c>RandomState</c> is the game's only seed carrier (the game
/// has no numeric seed); run settings are typed so every domain can replay the
/// same run without reading Unity globals.
///
/// The two rarity multipliers belong here because world generation CONSUMES
/// them: the game accumulates them during generation (<c>WorldGeneration.cs:1061-1062</c>
/// per layer, plus the Start-time trap term at <c>:257</c>) and every loot/trap
/// distribution of a layer is scaled by them, so a side that generates the layer
/// with the game's fresh 1.0 instead of the run's current value produces a
/// different world. They are part of the generation boundary capture — the same
/// instant the RNG state is taken — because the game never changes them while a
/// layer is running.
/// </summary>
public sealed record RunState(
	ulong RunId,
	byte[] RandomState,
	byte BiomeOverride,
	byte BiomeDepth,
	int TotalTraveled,
	bool LoadedRun,
	IReadOnlyList<RunSetting>? RunSettings = null,
	int LayerIndex = 0,
	float LootRarityMultiplier = RunRarityMultipliers.Neutral,
	float TrapRarityMultiplier = RunRarityMultipliers.Neutral)
{
	public bool IsTutorial => BiomeOverride == 1;
}
