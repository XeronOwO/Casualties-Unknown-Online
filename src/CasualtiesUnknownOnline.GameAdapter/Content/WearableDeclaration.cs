using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The one rule behind a wearable declaration: can the game's own wear flow PLACE
/// this garment? <c>Body.WearWearable</c> resolves the limb NAME the declaration
/// carries with <c>Body.LimbByName</c> (<c>Body.cs:1493</c>) and dereferences the
/// result on the next line, and it compares the slot id with
/// <c>Body.GetWearableBySlotID</c> (<c>Body.cs:1590</c>, called at <c>:1482</c>)
/// — so a declaration missing either names a garment the game cannot hang
/// anywhere.
///
/// <para>
/// Both readers ask here rather than deciding for themselves: the mapping that
/// installs the wearable flag (<see cref="ModItemInfoFactory"/>) and the load-time
/// report that names an incomplete declaration
/// (<see cref="GameAdapterItemContentProvider"/>). Whether the named limb exists on
/// a BODY is a different question with a different answer (it needs a live body and
/// is answered where the game asks it, in <c>GameWearPlacement</c>).
/// </para>
/// </summary>
internal static class WearableDeclaration
{
	/// <summary>True when the declaration names both halves of the placement the game's own flow reads.</summary>
	internal static bool HasPlacement(IModItemWearable wearable) =>
		!string.IsNullOrWhiteSpace(wearable.Limb) && !string.IsNullOrWhiteSpace(wearable.SlotId);
}
