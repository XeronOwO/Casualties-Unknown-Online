using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Applies the player stream's exact limb-pose facts onto a remote render
/// clone. The clone has no physics, so the owner's world-space limb transforms
/// are written directly; while an exact pose is active, BodyPatches must not
/// let HandleVisuals overwrite those transforms with the animator skeleton.
/// World-space is a deliberate choice: the visible limb transforms are not
/// reliably centered on the Body transform, and local offsets leave the clone
/// upright/underground even when every limb value is synced.
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

		// World-space writes must happen parents-first: when visible limbs are
		// nested, setting a child's world transform before its parent would be
		// shifted by the parent's subsequent move. Sorting by transform depth
		// makes the application order-independent of the stream's limb order.
		foreach (var pose in poses.OrderBy(p => LimbDepth(body, p)))
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

			var position = new Vector3(pose.WorldPosition.X, pose.WorldPosition.Y, 0f);
			var rotation = Quaternion.Euler(0f, 0f, pose.RotationZ);
			limb.transform.position = position;
			limb.transform.rotation = rotation;
			// Keep the frozen Rigidbody2D state aligned too: some game logic
			// reads limb.rb.position/rotation even on a render clone.
			limb.rb.position = position;
			limb.rb.rotation = pose.RotationZ;
		}

		if (driver != null) // Unity object — ==
		{
			driver.RagdollPoseActive = true;
		}
	}

	private static int LimbDepth(Body body, PlayerLimbPose pose)
	{
		if (pose.Index < 0 || pose.Index >= body.limbs.Length)
		{
			return 0;
		}

		var limb = body.limbs[pose.Index];
		if (limb == null) // Unity object — ==
		{
			return 0;
		}

		var depth = 0;
		var current = limb.transform.parent;
		while (current != null)
		{
			depth++;
			current = current.parent;
		}

		return depth;
	}
}
