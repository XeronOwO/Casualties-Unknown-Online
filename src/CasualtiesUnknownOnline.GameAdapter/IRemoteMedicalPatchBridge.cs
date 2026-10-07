namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The remote native-call half of the Harmony patch bridge: the WoundView
/// treatment surface AND the consume chain's drink, which is the same shape — a
/// native container call swallowed while this client measures a cross-player use
/// of an item it is holding. Kept as its own interface so
/// <see cref="IPatchBridge"/> stays under the architecture line gate while those
/// surfaces have one focused seam.
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

	/// <summary>
	/// The native injection call the operator's own client is running for a
	/// cross-player syringe session (the item's own <c>useLimbAction</c> reached
	/// <c>WaterContainerItem.Inject</c>). Returns true when a session owns this
	/// container, in which case the caller must NOT run the original: the ml
	/// becomes the session's dose, the host owns the drain and the patient's
	/// client owns the effect. Returns false for every local or unrelated
	/// injection, which must run untouched.
	/// </summary>
	bool TryDivertRemoteInjection(WaterContainerItem container, Limb limb, float amount);

	/// <summary>
	/// The native topical call the operator's own client is running while it
	/// measures a cross-player topical use (the item's own <c>useLimbAction</c>
	/// reached <c>WaterContainerItem.ApplyToLimb</c>). Returns true when a
	/// measurement owns this container and limb, in which case the caller must
	/// NOT run the original: the ml becomes the request's dose, the host owns the
	/// drain and the patient's client owns the effect. Returns false for every
	/// local or unrelated application, which must run untouched.
	/// </summary>
	bool TryDivertRemoteTopicalApply(WaterContainerItem container, Limb limb, float amount);

	/// <summary>
	/// The native drink call the operator's own client is running while it
	/// measures a cross-player drink (the item's own <c>useAction</c> reached
	/// <c>WaterContainerItem.Drink</c>). Returns true when a measurement owns this
	/// container, in which case the caller must NOT run the original: the ml
	/// becomes the request's dose, the host owns the drain and the patient's
	/// client owns the effect. Returns false for every local or unrelated drink,
	/// which must run untouched.
	/// </summary>
	bool TryDivertRemoteDrink(WaterContainerItem container, float amount);

	/// <summary>
	/// Measure the ml one drink of <paramref name="dragItem"/> takes, for the
	/// held-remote-item route: the requester's own client runs the item's native
	/// use action against the requester's OWN body — the body the native call
	/// would have used — inside the capture window, and the diverted container
	/// call reports what the delegate computed. Returns 0 when the item is not a
	/// drink container or its own action delivered nothing, in which case the
	/// intent carries no dose and the host refuses it by name.
	/// </summary>
	float MeasureRemoteDrinkDose(Item dragItem, Body drinker);

	/// <summary>
	/// Measure the ml one topical use of <paramref name="dragItem"/> applies, for
	/// the held-remote-item route: the requester's own client runs the item's
	/// native limb action — the very call this release would have made — inside
	/// the treatment capture window, and the diverted container call reports what
	/// the delegate computed. Returns 0 when the item is not a topical container
	/// or its own action delivered nothing, in which case the intent carries no
	/// dose and the host refuses it by name.
	/// </summary>
	float MeasureRemoteTopicalDose(Item dragItem, Limb limb);
}
