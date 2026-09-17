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

	/// <summary>
	/// Is this captured/wire multiplier a value the game could have produced? The
	/// game starts a run at <see cref="Neutral"/> and only ever SCALES the two
	/// values, so a NaN or an infinity comes from a buggy or hostile peer — the wire
	/// carries a float with no finiteness rule of its own — and never from
	/// generation. (The archive is not one of those producers: its JSON layer
	/// refuses a non-finite number on both read and write, so the restore-side
	/// caller of this rule is a backstop rather than the archive's defence.) Such a
	/// value must not shape a layer: the kernel refuses a baseline that carries one
	/// on every path into it — the command path, the applied wire batch, and the
	/// checkpoint restore — and the adapter refuses to write one into the live
	/// world, so the layer keeps the game's own values instead of being scaled by a
	/// number that has no meaning.
	///
	/// Null ("this producer captured no multiplier") is well formed by definition:
	/// it is a field the sender never carried, not a malformed value, and every
	/// reader defaults it to <see cref="Neutral"/>.
	/// </summary>
	public static bool IsWellFormed(float? multiplier) =>
		multiplier is not { } value || (!float.IsNaN(value) && !float.IsInfinity(value));
}
