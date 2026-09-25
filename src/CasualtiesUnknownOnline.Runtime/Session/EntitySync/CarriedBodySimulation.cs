namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The rule that decides who runs the game's own per-frame body simulation.
/// A remote clone is a presentation proxy and never simulates. A LOCAL carried
/// body is not a proxy: the carry relation owns its TRANSFORM, not its
/// simulation, so a conscious/alive rider keeps the whole native per-frame pass
/// (<c>Body.FixedUpdate</c>, <c>Body.Update</c>, <c>Limb.Update</c> — vital-sign
/// progression, the heart progression that drives the ECG animation, breathing,
/// moodles, limb wound/infection state and the sounds those steps play) while
/// the carry placement writes its position.
///
/// A dead or unconscious carried body is deliberately outside this rule: it is
/// presented as a physics ragdoll pinned to a moving carrier, which is a
/// different mechanism, so it keeps the frozen presentation it has today.
///
/// "Conscious" does NOT mean "standing": the native pass ragdolls a body whose
/// legs are gone or which is in shock even while it is conscious
/// (<c>Body.cs:2772</c>), which is exactly the population a carry exists to
/// move. Such a rider keeps its vitals simulation, is presented limp, and its
/// limb physics stays with the carry relation (a limb left to simulate under a
/// root that is teleported every frame is the twitch family this rule exists to
/// remove).
/// </summary>
public static class CarriedBodySimulation
{
	/// <summary>
	/// Whether a local carried body keeps its native per-frame simulation. True
	/// only for a living, conscious rider: a corpse or a comatose body cannot
	/// hold the standing pose the carry presentation assumes.
	/// </summary>
	public static bool KeepsNativeSimulation(bool isLocalCarriedBody, bool alive, bool conscious) =>
		isLocalCarriedBody && alive && conscious;

	/// <summary>
	/// Whether the render-proxy patches must skip the body's own per-frame
	/// simulation. The adapter's body patches make exactly this decision.
	/// </summary>
	public static bool SkipsNativeSimulation(bool isRemoteClone, bool isLocalCarriedBody, bool alive, bool conscious) =>
		isRemoteClone || (isLocalCarriedBody && !KeepsNativeSimulation(isLocalCarriedBody, alive, conscious));

	/// <summary>
	/// Whether movement input must be suppressed at the body while it is
	/// carried. The carrier, not the rider, drives the position; a rider whose
	/// input still reached the body would fight the carry placement and play
	/// walk/jump/sit poses for movement that cannot happen.
	/// </summary>
	public static bool SuppressesMovement(bool isLocalCarriedBody, bool alive, bool conscious) =>
		KeepsNativeSimulation(isLocalCarriedBody, alive, conscious);

	/// <summary>
	/// Whether the carry relation owns the body's ground contact. A carried
	/// body does not touch the terrain — the carrier under it does — so the
	/// native ground pass (landing dust and the <c>Grounded</c> clip, impact and
	/// footstep sounds, toxicity/slippery accumulation and the low-health-block
	/// damage query, <c>Body.cs:2702-2711</c>) belongs to the body that actually
	/// stands there, and a guest rider can never edit the world from a position
	/// it is only being carried at.
	/// </summary>
	public static bool CarrierOwnsGroundContact(bool isLocalCarriedBody, bool alive, bool conscious) =>
		KeepsNativeSimulation(isLocalCarriedBody, alive, conscious);
}
