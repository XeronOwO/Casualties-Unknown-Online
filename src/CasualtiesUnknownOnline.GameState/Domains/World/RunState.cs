using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// The authoritative run identity and world-generation baseline owned by the
/// kernel. The raw <c>RandomState</c> is the game's only seed carrier (the game
/// has no numeric seed); run settings are typed so every domain can replay the
/// same run without reading Unity globals.
/// </summary>
public sealed record RunState(
	ulong RunId,
	byte[] RandomState,
	byte BiomeOverride,
	byte BiomeDepth,
	int TotalTraveled,
	bool LoadedRun,
	IReadOnlyList<RunSetting>? RunSettings = null,
	int LayerIndex = 0)
{
	public bool IsTutorial => BiomeOverride == 1;
}
