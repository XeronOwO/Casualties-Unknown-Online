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
	}
}
