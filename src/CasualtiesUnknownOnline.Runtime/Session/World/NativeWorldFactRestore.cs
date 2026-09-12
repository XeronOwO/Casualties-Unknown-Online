using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The restored native world facts of ONE cut, taken as a unit: the values the
/// Game Adapter holds between the Continue click and the world-entry seam, because
/// none of them can be written before the world exists — and, for the recipe
/// unlock table, before the world's recipe table is COMPLETE.
///
/// Taken ONCE per generation — a second generation must never be handed the same
/// set — which is why the port exposes a READ-then-COMMIT rather than a read.
/// </summary>
/// <param name="Keypads">The decided keypad codes; the <c>Openable</c> they belong to exists only after generation.</param>
/// <param name="Geysers">The decided geyser liquid types, for the same reason.</param>
/// <param name="BlockDamages">The game's own partial-damage rows; every cell's block exists only after generation.</param>
/// <param name="Recipes">
/// The recipe table's unlock state. It belongs to THIS seam rather than to the
/// native save slot for a reason that is easy to get wrong: the game rebuilds
/// <c>Recipes.recipes</c> in <c>WorldGeneration.Awake</c>
/// (<c>WorldGeneration.cs:125</c>) and CUO's mod-content provider APPENDS the
/// custom recipes on a later Update frame (<c>GameAdapterRecipeContentProvider</c>),
/// so a write at <c>WorldGeneration.Start</c> would only see the vanilla table and
/// would refuse every custom recipe's row. By the world-entry edge the table is
/// finished for this world.
/// </param>
public readonly record struct NativeWorldFactRestore(
	IReadOnlyList<KeypadEntryMsg> Keypads,
	IReadOnlyList<GeyserStateEntryMsg> Geysers,
	IReadOnlyList<BlockDamageEntryMsg> BlockDamages,
	IReadOnlyList<SaveRecipeUnlockRow> Recipes)
{
	/// <summary>Nothing pending: what a cut that carried no native row decodes to.</summary>
	public static NativeWorldFactRestore Empty { get; } = new([], [], [], []);
}
