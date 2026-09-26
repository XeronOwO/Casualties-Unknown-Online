namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The rule that decides who runs the game's own per-frame body simulation.
/// A remote clone is a presentation proxy and never simulates. A LOCAL carried
/// body is not a proxy: the carry relation owns its TRANSFORM, not its
/// simulation. The pose the carry presentation can hold then splits the local
/// case in two:
/// <list type="bullet">
/// <item>a conscious/alive rider keeps the WHOLE native per-frame pass
/// (<c>Body.FixedUpdate</c>, <c>Body.Update</c>, <c>Limb.Update</c> — vital-sign
/// progression, the heart progression that drives the ECG animation, breathing,
/// moodles, limb wound/infection state and the sounds those steps play) while
/// the carry placement writes its position;</item>
/// <item>a dead or unconscious rider is presented as a physics ragdoll pinned to
/// a moving carrier, whose pose and physics the carry relation owns, so the
/// pose, physics, ground and sound stages of <c>Body.Update</c> stay out — but
/// its VITALS keep advancing, because the body is still a body and the game
/// itself advances them for every local body (<c>Body.Update</c> runs for a
/// corpse, and <c>Limb.Update</c> carries no alive guard).</item>
/// </list>
///
/// "Conscious" does NOT mean "standing": the native pass ragdolls a body whose
/// legs are gone or which is in shock even while it is conscious
/// (<c>Body.cs:2772</c>), which is exactly the population a carry exists to
/// move. Such a rider keeps its vitals simulation, is presented limp, and its
/// limb physics stays with the carry relation (a limb left to simulate under a
/// root that is teleported every frame is the twitch family this rule exists to
/// remove).
///
/// <see cref="Treatment"/> owns the decision. <see cref="SkipsNativeSimulation"/>,
/// <see cref="RunsVitalsSubset"/>, <see cref="SuppressesMovement"/> and
/// <see cref="CarrierOwnsGroundContact"/> are its views of that one answer, and
/// <see cref="KeepsNativeSimulation"/> is the carried-rider predicate it is built
/// on. <see cref="RunsLimbSimulation"/> is the same family's limb half, decided by
/// the proxy flag ALONE because a limb's wound/infection pass carries no pose and
/// no physics — the rule matrix pins that it agrees with the mode for every input
/// combination rather than assuming it.
/// </summary>
public static class CarriedBodySimulation
{
	/// <summary>What a body's own per-frame simulation gets on this client.</summary>
	public enum Mode
	{
		/// <summary>A remote clone: presentation only, no simulation at all.</summary>
		Proxy,

		/// <summary>
		/// A local carried body whose pose the carry relation owns as a pinned
		/// ragdoll (dead or unconscious): the vitals half of the native pass runs,
		/// its pose, physics, ground and sound stages do not.
		/// </summary>
		VitalsOnly,

		/// <summary>A local body that runs the game's whole per-frame pass.</summary>
		Full,
	}

	/// <summary>
	/// Whether a local carried body keeps its native per-frame simulation. True
	/// only for a living, conscious rider: a corpse or a comatose body cannot
	/// hold the standing pose the carry presentation assumes.
	/// </summary>
	public static bool KeepsNativeSimulation(bool isLocalCarriedBody, bool alive, bool conscious) =>
		isLocalCarriedBody && alive && conscious;

	/// <summary>
	/// The one decision every body patch reads: a remote clone is
	/// <see cref="Mode.Proxy"/>; a local carried body that is alive and conscious
	/// is <see cref="Mode.Full"/>; a local carried body that is dead or
	/// unconscious is <see cref="Mode.VitalsOnly"/>; every other (ordinary local)
	/// body is <see cref="Mode.Full"/>.
	/// </summary>
	public static Mode Treatment(bool isRemoteClone, bool isLocalCarriedBody, bool alive, bool conscious)
	{
		if (isRemoteClone)
		{
			return Mode.Proxy;
		}

		if (!isLocalCarriedBody)
		{
			return Mode.Full;
		}

		return KeepsNativeSimulation(isLocalCarriedBody, alive, conscious) ? Mode.Full : Mode.VitalsOnly;
	}

	/// <summary>
	/// Whether the render-proxy patches must skip the body's own per-frame
	/// simulation. The adapter's body patches make exactly this decision.
	/// </summary>
	public static bool SkipsNativeSimulation(bool isRemoteClone, bool isLocalCarriedBody, bool alive, bool conscious) =>
		Treatment(isRemoteClone, isLocalCarriedBody, alive, conscious) is not Mode.Full;

	/// <summary>
	/// Whether the VITALS half of the native per-frame pass runs for this body on
	/// this client: true exactly for <see cref="Mode.VitalsOnly"/> — a dead or
	/// unconscious LOCAL carried body, whose pose, physics, ground contact and
	/// sounds stay with the carry relation while its bleeding, temperature,
	/// radiation, periodic checks and limb wound/infection state keep advancing.
	/// </summary>
	public static bool RunsVitalsSubset(bool isRemoteClone, bool isLocalCarriedBody, bool alive, bool conscious) =>
		Treatment(isRemoteClone, isLocalCarriedBody, alive, conscious) is Mode.VitalsOnly;

	/// <summary>
	/// Whether a limb's own per-frame wound/infection pass runs — the limb half of
	/// this family, decided by the proxy flag ALONE. Only a remote clone's limbs are
	/// presentation (its vitals are not simulated on this client at all); every
	/// LOCAL body's limbs keep simulating, carried or not, including a corpse's,
	/// exactly as the game runs them for a body lying on the ground. The pass writes
	/// wound/infection numbers and limb shader values only — no pose and no physics —
	/// so no other input can change the answer; the rule matrix asserts that this
	/// predicate agrees with <see cref="Treatment"/> for every input combination.
	/// </summary>
	public static bool RunsLimbSimulation(bool isRemoteClone) => !isRemoteClone;

	/// <summary>
	/// Whether movement input must be suppressed at the body while it is
	/// carried. The carrier, not the rider, drives the position; a rider whose
	/// input still reached the body would fight the carry placement and play
	/// walk/jump/sit poses for movement that cannot happen. True only for a
	/// CARRIED body in <see cref="Mode.Full"/>.
	/// </summary>
	public static bool SuppressesMovement(bool isLocalCarriedBody, bool alive, bool conscious) =>
		isLocalCarriedBody && Treatment(isRemoteClone: false, isLocalCarriedBody, alive, conscious) is Mode.Full;

	/// <summary>
	/// Whether the carry relation owns the body's ground contact. A carried
	/// body does not touch the terrain — the carrier under it does — so the
	/// native ground pass (landing dust and the <c>Grounded</c> clip, impact and
	/// footstep sounds, toxicity/slippery accumulation and the low-health-block
	/// damage query, <c>Body.cs:2702-2711</c>) belongs to the body that actually
	/// stands there, and a guest rider can never edit the world from a position
	/// it is only being carried at. True only for a CARRIED body in
	/// <see cref="Mode.Full"/>.
	/// </summary>
	public static bool CarrierOwnsGroundContact(bool isLocalCarriedBody, bool alive, bool conscious) =>
		isLocalCarriedBody && Treatment(isRemoteClone: false, isLocalCarriedBody, alive, conscious) is Mode.Full;
}
