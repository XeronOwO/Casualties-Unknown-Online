namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure carry-participant presentation rule (no Unity): while a body is either
/// the carried rider or the carrier half of a carry/piggyback relation, the
/// native idle-sit pose must never be published, replayed, or allowed to
/// linger. The rule is shared by both participants' state publication, the
/// remote clone render paths, and every other peer's view so the whole family
/// cannot drift.
/// </summary>
public static class CarriedBodyPose
{
	/// <summary>
	/// Whether the 20 Hz player stream may present this body as sitting.
	/// A carry participant is never "idle-sitting": the ride/carry presentation
	/// replaces the native sit state, so even a stale/inflated idleTime must
	/// not leak onto the wire.
	/// </summary>
	public static bool ShouldPublishSitting(bool isCarryParticipant, bool idleTimeExceeded, bool exercising)
		=> !isCarryParticipant && idleTimeExceeded && !exercising;

	/// <summary>
	/// Whether the render proxy may replay the sit clips from the entity stream.
	/// A carry-participant clone is pinned to the carry presentation and must
	/// not switch to the native sit clips even if a stale Sitting=true snapshot
	/// arrives.
	/// </summary>
	public static bool ShouldReplaySit(bool isCarryParticipant, bool entitySitting)
		=> !isCarryParticipant && entitySitting;

	/// <summary>
	/// Whether the render path must actively leave an already-playing sit clip.
	/// This covers the transition into a carry relation from a previously
	/// sitting body: resetting the idle timer alone does not make HandleVisuals
	/// leave an already-active ExperimentSit clip when the body still presents
	/// as standing to the animator.
	/// </summary>
	public static bool ShouldExitSit(bool isCarryParticipant, bool currentClipIsSit)
		=> isCarryParticipant && currentClipIsSit;

	/// <summary>
	/// Whether a remote clone must replay the normal standing clips when the
	/// entity stream ends a sit state. This is the general sit-exit transition
	/// (used by SessionStatePump) and also the boundary that lets a carry
	/// participant return to the carry/standing presentation rather than
	/// lingering in sit.
	/// </summary>
	public static bool ShouldRestoreGroundedOnSitEnd(bool entitySitting, bool previousSitting)
		=> !entitySitting && previousSitting;

	/// <summary>
	/// Whether a carry-participant body must keep the native idle timer at zero
	/// every frame (not only after the 11 s pre-sit threshold), so the sit
	/// condition can never begin accumulating on either half of the relation.
	/// </summary>
	public static bool ShouldZeroIdleTimer(bool isCarryParticipant)
		=> isCarryParticipant;

	/// <summary>
	/// Whether the 20 Hz player stream should publish the body-root transform
	/// as the entity position for a carried rider. A carried body is placed by
	/// the shared ride-pose path at the carrier's back using its body-root
	/// transform; it is not a ragdoll, so publishing the non-standing torso
	/// anchor (the ragdoll convention) would make third-party viewers place the
	/// rider at a different vertical/reference point from the two participant
	/// views. Carried bodies therefore always use the body root as their stream
	/// anchor.
	/// </summary>
	public static bool ShouldPublishBodyRoot(bool isCarried)
		=> isCarried;
}
