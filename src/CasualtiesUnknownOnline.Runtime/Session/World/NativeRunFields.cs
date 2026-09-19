using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The native values that shape a RUN without belonging to any CUO domain, read
/// at one instant: the two accumulated rarity multipliers, the run clock base and
/// the recipe table's unlock state. They are exactly the run-level fields the
/// native <c>SaveSystem</c> write/read pair carries
/// (<c>SaveSystem.cs:151-186</c> / <c>:433-447</c>), which CUO stopped reading
/// when it stopped reading <c>save.sv</c> (decision 165) — the archive has to
/// carry them instead, and the adapter writes them back at the slot that used to
/// load them.
///
/// This is the capture/restore unit rather than four loose arguments because the
/// four are one read of one world at one instant: a cut that took the clock from
/// one frame and the multipliers from another would not be a cut.
/// <see cref="Failure"/> follows <see cref="NativeWorldFactCapture"/>: a reader
/// that met no live world (or no recipe table) reports that instead of returning
/// zeros, because zero and empty are REAL values a restore would write onto the
/// world — a re-locked recipe table and a run clock that restarts at zero.
/// </summary>
/// <param name="LootRarityMultiplier">The run's accumulated loot multiplier (<c>WorldGeneration.world.lootRarityMultiplier</c>).</param>
/// <param name="TrapRarityMultiplier">The run's accumulated trap multiplier.</param>
/// <param name="SavedRunTime">The run clock base at the read instant (seconds) — the game's own <c>savedRunTime + realTimeElapsed</c> value.</param>
/// <param name="LayerTimeSpent">
/// The layer's own radiation-timer accounting (<c>WorldGeneration.world.layerTimeSpent</c>,
/// seconds), null when the reader could not read it. It is the one per-LAYER value this
/// run-level read carries, and only a mid-run cut records it: a layer-end cut names a layer
/// that is regenerated, so its timer is the new layer's own. The LIMIT
/// (<c>maxTimePerLayer</c>) is deliberately NOT carried — <c>WorldGeneration.Start</c>
/// derives it from the run's <c>timelimit</c> setting (<c>WorldGeneration.cs:258</c>), which
/// the restored run baseline already applies, so carrying it would be a second, disagreeing
/// carrier for a value the game recomputes.
/// </param>
/// <param name="Recipes">Every entry of the game's own recipe table, in table order.</param>
/// <param name="Failure">Why the values could not be read; null when the read succeeded.</param>
public readonly record struct NativeRunFields(
	float LootRarityMultiplier,
	float TrapRarityMultiplier,
	float SavedRunTime,
	IReadOnlyList<SaveRecipeUnlockRow> Recipes,
	string? Failure,
	float? LayerTimeSpent = null)
{
	/// <summary>The live world (or its recipe table) is not there: the cut must be refused rather than store defaults.</summary>
	public static NativeRunFields Unreadable(string failure) => new(0f, 0f, 0f, [], failure);
}
