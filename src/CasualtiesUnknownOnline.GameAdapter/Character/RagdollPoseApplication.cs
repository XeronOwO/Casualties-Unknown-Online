using System;
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
/// It also owns the read-only measurement of whether a carried clone's limbs
/// actually followed the root the ride pose pinned
/// (<see cref="MeasurePinnedRootSeparation"/>).
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
				driver.LimbAnchor.Clear();
			}

			return;
		}

		// A tick replaces the whole shape, captured in the same parents-first
		// order the poses are written in: a nested limb is measured after the
		// parent whose move it inherits, and a reading can never mix two ticks.
		if (driver != null) // Unity object — ==
		{
			driver.LimbAnchor.Clear();
		}

		var root = body.transform.position;
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
			if (driver != null) // Unity object — ==
			{
				driver.LimbAnchor.Capture(pose.Index, position.x, position.y, root.x, root.y);
			}
		}

		if (driver != null) // Unity object — ==
		{
			driver.RagdollPoseActive = true;
		}
	}

	/// <summary>
	/// Measures how far this clone's rendered limbs sit from the position a rigid
	/// follow of its body root would put them, after the ride pose pinned that
	/// root. This is a READ-ONLY probe: the limb placement the game's own
	/// transform hierarchy performs is the only one, because the hierarchy facts
	/// (the visible limbs are children of the body root and a clone's limb
	/// rigidbodies are frozen) say a root write carries them. Zero therefore means
	/// the hierarchy carried every limb — the expected reading — while a non-zero
	/// value is the runtime evidence that the ticket's "limbs left behind"
	/// hypothesis is live on that screen, which is the only thing that could
	/// justify repositioning limbs here. The window maximum is kept on the driver
	/// for the 1 Hz clone diagnostics.
	/// </summary>
	internal static void MeasurePinnedRootSeparation(Body body)
	{
		var driver = body.GetComponent<RemoteBodyDriver>();
		if (driver == null // Unity object — ==
			|| !CarriedLimbAnchor.ShouldMeasureAgainstPinnedRoot(driver.IsCarriedRider, driver.RagdollPoseActive))
		{
			return;
		}

		var anchor = driver.LimbAnchor;
		if (anchor.IsEmpty)
		{
			return;
		}

		var root = body.transform.position;
		var separation = 0f;
		for (var order = 0; order < anchor.Count; order++)
		{
			if (!anchor.TryTarget(order, root.x, root.y, out var index, out var x, out var y)
				|| index < 0
				|| index >= body.limbs.Length)
			{
				continue;
			}

			var limb = body.limbs[index];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			var position = limb.transform.position;
			var deltaX = position.x - x;
			var deltaY = position.y - y;
			var distance = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
			if (distance > separation)
			{
				separation = distance;
			}
		}

		if (separation > driver.LimbSeparationWindowMax)
		{
			driver.LimbSeparationWindowMax = separation;
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
