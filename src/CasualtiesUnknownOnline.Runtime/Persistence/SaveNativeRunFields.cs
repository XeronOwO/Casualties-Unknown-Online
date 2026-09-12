using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The native values one cut reads that the kernel's run baseline does NOT hold
/// (§3.4, <c>run.json</c>'s <c>native-run-fields</c> row):
///
/// - <see cref="SavedRunTime"/> is the run clock base — the game writes
///   <c>SaveSystem.savedRunTime + WorldGeneration.world.realTimeElapsed</c> into
///   its save (<c>SaveSystem.cs:165</c>) and reads it back into
///   <c>savedRunTime</c> (<c>:439</c>), which is what
///   <c>WorldGeneration.TotalRunTime</c> adds the current layer's elapsed time
///   to. A cut has to read it at the cut instant because it grows every frame
///   inside a layer.
/// - <see cref="Recipes"/> is the recipe table's unlock state
///   (<c>Recipes.recipes[].hasMadeBefore</c> / <c>.INT</c>, written at
///   <c>SaveSystem.cs:151-157</c>, read at <c>:442-447</c>). A recipe domain
///   would own it later; until one exists, the archive carries the game's own
///   rows so a continued world does not silently re-lock everything the player
///   learned to craft.
///
/// The rarity multipliers are NOT here: they are world-generation inputs, so they
/// ride the run baseline row (<c>run</c>) — stamped with the cut instant's value
/// — where a side that has to GENERATE the layer reads them from.
///
/// A field this row could not read is the ABSENCE of the row, and the restore
/// names it — never a quiet default.
/// </summary>
public sealed class SaveNativeRunFields
{
	/// <summary>
	/// The run clock base at the cut instant (seconds), exactly the game's own
	/// <c>runTime</c> value. It is read at the cut rather than at the generation
	/// boundary (where the run baseline's own fields are captured) because it grows
	/// every frame inside a layer.
	/// </summary>
	public float SavedRunTime { get; init; }

	/// <summary>
	/// One row per entry of the game's recipe table, in table order. Written as a
	/// list (not a dictionary) because the native write/read pair is positional.
	/// </summary>
	public List<SaveRecipeUnlockRow> Recipes { get; init; } = [];
}
