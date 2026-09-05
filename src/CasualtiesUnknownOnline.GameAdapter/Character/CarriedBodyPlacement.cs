using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Pure placement/restore rules shared by the two carry presentation paths:
/// the carried player's own client follows the remote carrier
/// (<see cref="PlayerInteractionApply"/>), and the carrier's own client pins the
/// remote rider clone to the local body (<see cref="RemotePlayerRenderer"/>).
/// Also owns the release-side physics restore so a dropped/rider body does not
/// stay frozen or floating.
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
	/// Applies the complete rider presentation onto a carried/rider Body using
	/// one shared rule. Both the rider's own client (following the remote
	/// carrier) and the carrier's client (pinning the remote rider clone) call
	/// this, so every presentation field — position, velocity, facing,
	/// crouching pose, standing/move-dir gates and look target — can never
	/// diverge between the two sides.
	/// </summary>
	public static void ApplyRidePose(
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
		body.standing = false;
		body.moveDir = Vector2.zero;
		if (carrierLookTarget is { } lookTarget)
		{
			body.targetLookPos = lookTarget;
		}

		// Facing is rendered through transform.localScale.x; Body.Update is
		// skipped on both carry paths, so the shared write must reconcile the
		// visual scale with logical facing every time.
		BodyFacing.Apply(body);
	}

	/// <summary>
	/// Release-side restore for a LOCAL body that was carried. The carried
	/// presentation path froze the body and limb rigidbodies (the same
	/// render-proxy freeze used for remote clones), so destroying the driver
	/// alone leaves the body unable to fall, move or stand. Re-enable the
	/// physics, then restore the native standing/ragdoll pose for the body's
	/// current alive/conscious state.
	/// </summary>
	public static void RestoreLocalBody(Body body)
	{
		body.rb.simulated = true;
		body.moveDir = Vector2.zero;
		body.rb.velocity = Vector2.zero;

		foreach (var limb in body.limbs)
		{
			limb.rb.simulated = true;
		}

		if (body.conscious && body.alive)
		{
			body.Stand(true);
		}
		else
		{
			body.Ragdoll();
			// Ragdoll only limbs-enables when the body was standing; the carried
			// proxy path already left standing=false, so re-assert the physics
			// enable directly as well.
			foreach (var limb in body.limbs)
			{
				limb.rb.simulated = true;
			}
		}

		// The carried follow wrote Body.isRight while the body's native flip
		// path was skipped. Restore the visual scale to match the logical
		// facing so the released body's HandleVisuals can flip normally again
		// (a stale scale sign makes the auto-flip condition fight the render).
		BodyFacing.Apply(body);
	}
}
