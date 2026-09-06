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

	/// <summary>
	/// Remote-medical WoundView special action on a limb with shrapnel: starts
	/// the native shrapnel minigame on the display body and joins/routes the
	/// shared host-authoritative shrapnel session. Returns true when the action
	/// was handled (the original local-body special action must be skipped).
	/// </summary>
	bool TryStartRemoteShrapnelSpecial(Limb limb);

	/// <summary>
	/// Remote-medical WoundView special action on any eligible limb: routes
	/// tourniquet removal, shrapnel, splint removal or dislocation fix through
	/// the host-authoritative medical operation session. Returns true when
	/// handled, false when read-only must remain.
	/// </summary>
	bool TryStartRemoteWoundSpecial(Limb limb);
}
