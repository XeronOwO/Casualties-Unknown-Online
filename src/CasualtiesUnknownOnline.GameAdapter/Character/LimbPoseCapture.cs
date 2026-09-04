using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Captures the local body's current visible-limb transform poses for the 20 Hz
/// player state stream. A frozen remote clone cannot reproduce the owner's
/// physics-driven ragdoll/dead/unconscious pose, so the owner publishes each
/// limb's world-space position and z rotation and the peer writes them directly
/// onto the clone. World-space is required because the visible limb transforms
/// are not reliably centered on the Body transform; local offsets would be
/// parent-relative and can leave the clone upright/underground.
/// </summary>
internal static class LimbPoseCapture
{
	internal static List<PlayerLimbPose>? Capture(Body body)
	{
		// Standing and sleeping use the animator/nap clips on the proxy; exact
		// poses are only needed for the non-standing, non-sleeping lying states
		// (ragdoll/dead/unconscious).
		if (body.standing || body.sleeping)
		{
			return null;
		}

		if (body.limbs.Length == 0)
		{
			return null;
		}

		var poses = new List<PlayerLimbPose>(body.limbs.Length);
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			var position = limb.transform.position;
			poses.Add(new PlayerLimbPose
			{
				Index = i,
				WorldPosition = new NetVector2(position.x, position.y),
				RotationZ = limb.transform.eulerAngles.z,
			});
		}

		return poses.Count == 0 ? null : poses;
	}
}
