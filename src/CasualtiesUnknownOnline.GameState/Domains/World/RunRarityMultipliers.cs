namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// The one value both rarity multipliers start a run at. The game initializes
/// <c>WorldGeneration.lootRarityMultiplier</c> / <c>trapRarityMultiplier</c> to
/// 1f (<c>WorldGeneration.cs:4174-4177</c>) and only ever scales them during
/// generation — per layer (<c>:1061-1062</c>) and, for the trap multiplier, once
/// at <c>Start</c> for the debug start depth (<c>:257</c>) — never inside a
/// running layer, so "no multiplier captured yet" and "the game's fresh value"
/// are the same number: a caller never has to guess one apart from the other, and
/// a sender that predates the field degrades to exactly the behavior it had.
/// </summary>
public static class RunRarityMultipliers
{
	/// <summary>The game's own starting multiplier for a new run.</summary>
	public const float Neutral = 1f;
}
