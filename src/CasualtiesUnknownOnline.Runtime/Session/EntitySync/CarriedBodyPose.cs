namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure carried-ride presentation rule (no Unity): while a body is a carried
/// rider, the native idle-sit pose must never be published, replayed, or
/// allowed to linger. The rule is shared by the rider's own state publication,
/// the carrier-side remote-rider clone, and every other peer's render path so
/// the three views cannot drift.
/// </summary>
public static class CarriedBodyPose
{
	/// <summary>
	/// Whether the 20 Hz player stream may present this body as sitting.
	/// A carried rider is never "idle-sitting": the ride presentation replaces
	/// the native sit state, so even a stale/inflated idleTime must not leak
	/// onto the wire.
	/// </summary>
	public static bool ShouldPublishSitting(bool isCarried, bool idleTimeExceeded, bool exercising)
		=> !isCarried && idleTimeExceeded && !exercising;

	/// <summary>
	/// Whether the render proxy may replay the sit clips from the entity stream.
	/// A carrier-side rider clone is pinned to the carrier and must not switch
	/// to the native sit clips even if a stale Sitting=true snapshot arrives.
	/// </summary>
	public static bool ShouldReplaySit(bool isCarriedRider, bool entitySitting)
		=> !isCarriedRider && entitySitting;

	/// <summary>
	/// Whether the render path must actively leave an already-playing sit clip.
	/// This covers the transition into carry from a previously sitting body:
	/// resetting the idle timer alone does not make HandleVisuals leave an
	/// already-active ExperimentSit clip when the body still presents as
	/// standing to the animator.
	/// </summary>
	public static bool ShouldExitSit(bool isCarriedRider, bool currentClipIsSit)
		=> isCarriedRider && currentClipIsSit;

	/// <summary>
	/// Whether a remote clone must replay the normal standing clips when the
	/// entity stream ends a sit state. This is the general sit-exit transition
	/// (used by SessionStatePump) and also the boundary that lets a carried
	/// rider return to the ride presentation rather than lingering in sit.
	/// </summary>
	public static bool ShouldRestoreGroundedOnSitEnd(bool entitySitting, bool previousSitting)
		=> !entitySitting && previousSitting;

	/// <summary>
	/// Whether a carried-ride body must keep the native idle timer at zero
	/// every frame (not only after the 11 s pre-sit threshold), so the sit
	/// condition can never begin accumulating on a ride.
	/// </summary>
	public static bool ShouldZeroIdleTimer(bool isCarriedRide)
		=> isCarriedRide;
}
