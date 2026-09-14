namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The carriage half of the Harmony patch bridge: the carriage domain's adapter
/// reads a patch needs (the carrier's extra encumbrance, whether a body is the
/// local carrier, and the local carrier's post-Update re-pin moment). Kept as
/// its own interface so <see cref="IPatchBridge"/> stays under the architecture
/// line gate while the carriage surface has one focused seam (same reason
/// <see cref="IRemoteMedicalPatchBridge"/> exists).
/// </summary>
internal interface ICarriagePatchBridge
{
	/// <summary>
	/// The extra encumbrance a LOCAL carrier body owes while it carries or
	/// piggybacks a teammate. Zero for non-local bodies or when there is no
	/// active carry relation, so the native <c>Body.GetTotalEncumberance</c>
	/// result is unchanged for remote clones and standalone players.
	/// </summary>
	float GetCarriedEncumbrance(Body body);

	/// <summary>
	/// True when the given body is the LOCAL player's body and that player is
	/// currently the carrier half of a carry/piggyback relation. Harmony pose
	/// patches use this to suppress the native idle-sit while carrying; the
	/// runtime carry mirror is the single source of truth (no extra local
	/// marker to keep in sync).
	/// </summary>
	bool IsLocalCarrier(Body body);

	/// <summary>
	/// The local carrier's <c>Body.Update</c> finished. Re-pin the remote rider
	/// clones to the just-updated local carrier transform; CUO's own update pump
	/// may have pinned them before the game moved the local body this frame.
	/// </summary>
	void OnLocalCarrierBodyUpdated();
}
