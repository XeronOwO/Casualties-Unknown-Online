using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The carry placement/restore rules, shared by the two riders: the carried
/// player's own client follows the remote carrier
/// (<see cref="PlayerInteractionApply"/>), and the carrier's own client pins the
/// remote rider clone to the local body (<see cref="RemotePlayerRenderer"/>).
/// The follow writes only what the carry relation owns — position, velocity,
/// facing, crouch pose and look target — so the local rider's own simulation
/// and pose stay the game's own. Also owns the release-side physics restore so a
/// dropped/rider body does not stay frozen or floating.
/// </summary>
internal static class CarriedBodyPlacement
{
	/// <summary>
	/// The back-offset position for a body riding on a carrier. The carrier's
	/// facing determines which side the rider sits on; a crouching carrier lowers
	/// the rider.
	/// </summary>
	public static Vector3 BackOffset(Vector3 carrierPosition, bool carrierIsRight, bool carrierCrouching)
	{
		var side = carrierIsRight ? -1f : 1f;
		var up = carrierCrouching ? 0.5f : 0.9f;
		return carrierPosition + new Vector3(0.35f * side, up, 0f);
	}

	/// <summary>
	/// The local scale that turns a carry mount into a world-space identity
	/// transform when the mount is a direct child of a carrier Body. The
	/// carrier's Body uses <c>localScale.x</c> sign for facing; cancelling the
	/// whole carrier world scale here lets the rider root keep its normal
	/// facing/scale semantics without inheriting the carrier's flip.
	/// </summary>
	public static Vector3 CarryMountScale(Vector3 carrierWorldScale)
	{
		if (carrierWorldScale.x == 0f || carrierWorldScale.y == 0f || carrierWorldScale.z == 0f)
		{
			return Vector3.one;
		}

		return new Vector3(
			1f / carrierWorldScale.x,
			1f / carrierWorldScale.y,
			1f / carrierWorldScale.z);
	}

	/// <summary>
	/// The carrier follow for a REMOTE RIDER CLONE, which is a frozen render
	/// proxy: on top of the shared follow it holds the proxy pose gates
	/// (non-standing, no movement input) that the clone's skipped simulation
	/// would otherwise maintain. Placing the root is also where the read-only
	/// limb check runs: this pass runs on every rendered frame while the exact
	/// poses arrive from the stream, so it is the one place where "did the limbs
	/// follow the root" can be read.
	/// </summary>
	public static void ApplyRidePose(
		Body body,
		Vector3 carrierPosition,
		bool carrierIsRight,
		bool carrierCrouching,
		Vector2 carrierVelocity,
		Vector2? carrierLookTarget)
	{
		ApplyCarrierFollow(body, carrierPosition, carrierIsRight, carrierCrouching, carrierVelocity, carrierLookTarget);
		body.standing = false;
		body.moveDir = Vector2.zero;
		// The root was just written, so this is the frame's one chance to read
		// whether the clone's exact limb poses actually travelled with it. The
		// check writes NOTHING: the hierarchy is expected to have carried the
		// limbs, and a non-zero reading is the evidence that would justify a fix.
		RagdollPoseApplication.MeasurePinnedRootSeparation(body);
	}

	/// <summary>
	/// The carrier follow for the LOCAL carried rider. The rider is not a proxy:
	/// its own per-frame simulation and pose keep running
	/// (<see cref="Runtime.Session.EntitySync.CarriedBodySimulation"/>),
	/// so this writes only what the carry relation owns — the transform, the
	/// reported velocity, the facing/crouch pose, the aim point and the movement
	/// input gate. Its <c>standing</c> is never written: the body's own native
	/// state decides the pose.
	/// </summary>
	public static void ApplyLocalRiderPose(
		Body body,
		Vector3 carrierPosition,
		bool carrierIsRight,
		bool carrierCrouching,
		Vector2 carrierVelocity,
		Vector2? carrierLookTarget)
	{
		ApplyCarrierFollow(body, carrierPosition, carrierIsRight, carrierCrouching, carrierVelocity, carrierLookTarget);
		// The movement input gate is also asserted in BodyUpdatePatch before the
		// native Body.Update runs (the placement's own frame position relative
		// to it is not guaranteed); zeroing it here keeps the value honest for
		// readers between the two writes.
		body.moveDir = Vector2.zero;
		// The carry relation owns the physics as well as the transform: the root
		// must not integrate against the placement, and the visible limbs must
		// not be simulated bodies under a root that is teleported every frame
		// (that is the limb-twitch family). BodyUpdatePatch re-asserts this after
		// the native pass, which can ragdoll a rider whose legs are gone
		// (Body.cs:2772) and re-enable limb physics (Body.cs:1723).
		body.rb.simulated = false;
		foreach (var limb in body.limbs)
		{
			limb.rb.simulated = false;
		}
	}

	/// <summary>
	/// Release-side restore for a LOCAL body that was carried. The carry
	/// relation took three things from the body — the root transform, the
	/// physics of the root and its limbs, and the native movement gate — and
	/// the relation may have started while the body was in either presentation
	/// mode or changed mode mid-relation, so the restore hands all three back
	/// from the body's OWN current state instead of from a recorded mode: that
	/// cannot go stale, and no release path can leave the player unable to move.
	/// </summary>
	public static void RestoreLocalBody(Body body)
	{
		body.rb.simulated = true;
		body.moveDir = Vector2.zero;
		body.rb.velocity = Vector2.zero;
		// The movement gate is handed back unconditionally: BodyUpdatePatch held
		// it shut on every frame the rider was simulating, including frames
		// before the rider lost consciousness. A start gate that still holds the
		// player re-locks it in the same frame (BodyUpdatePatch prefix).
		Traverse.Create(body).Field("movingAllowed").SetValue(true);
		// A standing body's visible limbs are animator-driven and stay out of
		// physics (the game's own Stand() disables them, Body.cs:1687); a
		// non-standing body is a ragdoll and needs its limb physics back.
		var ragdolled = !body.standing;
		foreach (var limb in body.limbs)
		{
			limb.rb.simulated = ragdolled;
		}

		// The carried follow wrote Body.isRight while the body's native flip
		// path was skipped or frozen. Restore the visual scale to match the
		// logical facing so the released body's HandleVisuals can flip normally
		// again (a stale scale sign makes the auto-flip condition fight the
		// render).
		BodyFacing.Apply(body);
	}

	/// <summary>
	/// The follow both riders share: the carrier's own transform/facing/crouch
	/// pose and velocity drive the rider, and the rider's body root is placed at
	/// the carrier's back with the facing scale reconciled.
	/// </summary>
	private static void ApplyCarrierFollow(
		Body body,
		Vector3 carrierPosition,
		bool carrierIsRight,
		bool carrierCrouching,
		Vector2 carrierVelocity,
		Vector2? carrierLookTarget)
	{
		body.transform.position = BackOffset(carrierPosition, carrierIsRight, carrierCrouching);
		body.rb.velocity = carrierVelocity;
		body.isRight = carrierIsRight;
		body.crouching = carrierCrouching;
		if (carrierLookTarget is { } lookTarget)
		{
			body.targetLookPos = lookTarget;
		}

		// Facing is rendered through transform.localScale.x; the native flip
		// path reads the carrier's facing while carried, so the shared write
		// must reconcile the visual scale with logical facing every time.
		BodyFacing.Apply(body);
	}
}
