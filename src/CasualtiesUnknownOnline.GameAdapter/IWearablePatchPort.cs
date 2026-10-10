namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The wear placement's patch port: what a static Harmony patch needs to refuse
/// and report a wear the game's own flow would throw on. <c>Body.WearWearable</c>
/// resolves the item's declared limb name with <c>Body.LimbByName</c>
/// (<c>Body.cs:1493</c>) and dereferences the result on the next line, so a
/// garment whose limb this body does not carry is stopped before the native body
/// runs, with the refusal named instead of thrown.
///
/// <para>
/// The decision itself is <see cref="Content.GameWearPlacement"/>'s and is read
/// statically — a patch class may not be handed anything by construction — so
/// this port carries the REPORT of that decision, the one thing the patch cannot
/// reach: the logger. A door with a single member is the shape
/// <see cref="ILayerAdvancePatchPort"/> already has. The aggregate does not
/// compose it, so a patch that wants this door reads <c>PatchBridge.Wearable</c>
/// and the compiler keeps it off <see cref="IPatchBridge"/>.
/// </para>
/// </summary>
internal interface IWearablePatchPort
{
	/// <summary>
	/// A local wear of <paramref name="item"/> was refused because
	/// <paramref name="limbName"/> names no limb on this body — the state the game's
	/// own wear family would have dereferenced null in. Designed behaviour, so it is
	/// stated rather than silent: the item is not worn (or is reported as not worn),
	/// and the declaration that could not be placed is named.
	/// </summary>
	void ReportWearPlacementRefused(string itemId, string limbName);
}
