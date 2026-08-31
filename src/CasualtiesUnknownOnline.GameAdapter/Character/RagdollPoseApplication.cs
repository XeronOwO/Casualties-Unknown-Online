using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Applies the player stream's exact limb-pose facts onto a remote render
/// clone. The clone has no physics, so the owner's local-space limb transforms
/// are written directly; while an exact pose is active, BodyPatches must not
/// let HandleVisuals overwrite those transforms with the animator skeleton.
/// </summary>
internal static class RagdollPoseApplication
{
	internal static void Apply(Body body, List<PlayerLimbPose>? poses, RemoteBodyDriver? driver)
	{
		if (body.standing || body.sleeping || poses is not { Count: > 0 })
		{
			if (driver != null) // Unity object — ==
			{
				driver.RagdollPoseActive = false;
			}

			return;
		}

		foreach (var pose in poses)
		{
			if (pose.Index < 0 || pose.Index >= body.limbs.Length)
			{
				continue;
			}

			var limb = body.limbs[pose.Index];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			limb.transform.localPosition = new Vector3(pose.LocalPosition.X, pose.LocalPosition.Y, 0f);
			limb.transform.localRotation = Quaternion.Euler(0f, 0f, pose.RotationZ);
		}

		if (driver != null) // Unity object — ==
		{
			driver.RagdollPoseActive = true;
		}
	}
}
