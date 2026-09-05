namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The remote-medical half of the Harmony patch bridge. Kept as its own
/// interface so <see cref="IPatchBridge"/> stays under the architecture line
/// gate while the native WoundView treatment surface has one focused seam.
/// </summary>
internal interface IRemoteMedicalPatchBridge
{
	/// <summary>
	/// Remote-medical drag release: while the native WoundView is focused on a
	/// remote player, the dragging player dropped a local medical item onto a
	/// body-limb diagram. Routes the exact item and selected limb through the
	/// existing host-authoritative heal/use request path and returns true so
	/// the native read-only display body is never mutated. Returns false when
	/// the view is closed, the item is not a locally-owned usable medical item,
	/// or the item has no authoritative instance id.
	/// </summary>
	bool TryHandleRemoteMedicalLimbUse(Item dragItem, int limbIndex);
}
