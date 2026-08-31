using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Captures the local body's current visible-limb transform poses for the 20 Hz
/// player state stream. A frozen remote clone cannot reproduce the owner's
/// physics-driven ragdoll/dead/unconscious pose, so the owner publishes the
/// local-space position + z rotation of each limb and the peer writes them
/// directly onto the clone.
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

			var position = limb.transform.localPosition;
			poses.Add(new PlayerLimbPose
			{
				Index = i,
				LocalPosition = new NetVector2(position.x, position.y),
				RotationZ = limb.transform.localEulerAngles.z,
			});
		}

		return poses.Count == 0 ? null : poses;
	}
}
