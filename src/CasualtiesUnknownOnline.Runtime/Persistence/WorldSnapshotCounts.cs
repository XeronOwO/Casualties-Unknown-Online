using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The per-domain record counts ONE cut and ONE restore report (S4 scope 3): the
/// same domains in the same order on both sides, so a restore that took fewer rows
/// than the cut wrote is diagnosable by comparing two log lines — locating a "my
/// base is gone" mismatch otherwise needs a code change, a deploy and a
/// reproduction, which is the loop this line exists to remove.
///
/// The counts are the rows the ARCHIVE holds, and that is the whole contract. Each side
/// takes them from the only place that can answer for it: the cut from
/// <see cref="EncodedSnapshot"/> (the encoder knows what it WROTE — a layer-end cut drops
/// the in-layer rows, so the kernel checkpoint it was handed counts a different set), and
/// the restore from <see cref="Of"/>'s inputs, which are the decoded snapshot itself —
/// the kernel tables the archive's rows materialized into (items, players, enemies
/// including their tombstones, fluids, the world-entity facts) plus the file-backed row
/// lists the decode produced (characters, world blocks, world transients, the native
/// run-field row). A count taken from the writer's INPUT on one side and from the file on
/// the other would make the two lines disagree on a perfectly good snapshot and teach a
/// reader to distrust them.
/// </summary>
internal readonly record struct WorldSnapshotCounts(
	int Items,
	int Players,
	int Enemies,
	int Fluids,
	int WorldEntities,
	int Characters,
	int WorldBlocks,
	int WorldTransients,
	int? RecipeRows)
{
	/// <summary>
	/// The counts of one DECODED snapshot: the kernel tables the archive's rows
	/// materialized into (<paramref name="checkpoint"/> — items, players, the enemies and
	/// their tombstones, the fluid regions and the four world-entity fact lists), plus the
	/// rows that live outside the checkpoint and are therefore passed in.
	/// <paramref name="recipeRows"/> is null when the snapshot carries no native
	/// run-field row at all — a named gap (§6), never a zero.
	/// </summary>
	internal static WorldSnapshotCounts Of(
		GameCheckpoint checkpoint,
		int characters,
		int worldBlocks,
		int worldTransients,
		int? recipeRows)
	{
		var entities = checkpoint.WorldEntities;
		return new WorldSnapshotCounts(
			checkpoint.Items.Count,
			checkpoint.Players?.Players.Count ?? 0,
			(checkpoint.Enemies?.Enemies.Count ?? 0) + (checkpoint.Enemies?.Removed.Count ?? 0),
			checkpoint.Fluids?.Regions.Count ?? 0,
			(entities?.Consumptions.Count ?? 0) + (entities?.BuildingHealth.Count ?? 0) + (entities?.OpenedEntities.Count ?? 0) + (entities?.TrapStates.Count ?? 0),
			characters,
			worldBlocks,
			worldTransients,
			recipeRows);
	}

	/// <summary>The counts as the one line both log sites carry.</summary>
	internal string Describe() =>
		$"{Items} item row(s), {Players} player row(s), {Enemies} enemy row(s), {Fluids} fluid chunk(s), "
		+ $"{WorldEntities} world-entity row(s), {Characters} character(s), {WorldBlocks} world-block row(s), "
		+ $"{WorldTransients} transient row(s), native run fields {RunFieldsText()}";

	private string RunFieldsText() => RecipeRows is { } recipes ? $"present ({recipes} recipe row(s))" : "absent";
}
