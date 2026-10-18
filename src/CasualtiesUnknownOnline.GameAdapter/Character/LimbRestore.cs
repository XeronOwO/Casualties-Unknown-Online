using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Puts a severed limb back on the body — the piece the game's own <c>heal</c> command leaves behind.
/// <para>
/// The restore is smaller than the reference implementation suggests, and that is a reading rather than a
/// shortcut. Dismembering a limb does exactly this: <c>limb.dismembered = true</c>,
/// <c>limb.gameObject.SetActive(false)</c>, and zeroes its infection/health/bleed — the GameObject is
/// DEACTIVATED, never destroyed, so its <c>HingeJoint2D</c>, its local transform and its connection to the
/// connected body all survive untouched. The reference re-derives that geometry from saved hinge state (a type
/// in an assembly this project does not reference) after ragdolling the whole body, which it needs because it
/// rebuilds the body from scratch; this restore does not, because nothing was torn down.
/// </para>
/// <para>
/// What it does have to do is order the clear before the activation. The reason is narrower than "the enable
/// path reads the flag": the game implements no <c>OnEnable</c> anywhere, and re-activating an object that was
/// already active re-runs neither <c>Awake</c> nor <c>Start</c>. The one reader that does exist is
/// <c>Limb.Start</c>, whose guard switches the object back off when <c>dismembered</c> is still set — so the
/// flag is cleared first, which keeps a limb whose <c>Start</c> has not run yet from switching itself off.
/// </para>
/// </summary>
internal static class LimbRestore
{
	/// <summary>
	/// The first limb whose severed flag this call cleared, or null when nothing was severed. One limb is the
	/// whole answer because the change has to be reported ONCE: the limb-state message carries EVERY limb of the
	/// owner and the receiver replaces its whole set, so a single message from any cleared limb reconstructs the
	/// body exactly — reporting per limb would send N identical messages for one action.
	/// </summary>
	internal static Limb? Apply(Body body)
	{
		if (!AnyLimbMissing(body))
		{
			return null;
		}

		var limbs = body.limbs;
		Limb? first = null;
		foreach (var limb in limbs)
		{
			if (limb != null && limb.dismembered) // Unity object — ==
			{
				limb.dismembered = false;
				first ??= limb;
			}
		}

		// Second pass, now that the flag is clear everywhere: activating is what makes the game re-register the
		// limb's own rigidbody and colliders.
		foreach (var limb in limbs)
		{
			if (limb != null) // Unity object — ==
			{
				limb.gameObject.SetActive(true);
			}
		}

		ReignoreCollisions(body);
		return first;
	}

	/// <summary>Whether any limb of the body is severed. Written here rather than taken from the reference, whose
	/// equivalent is its own extension method rather than a native member.</summary>
	internal static bool AnyLimbMissing(Body body) => CountMissing(body) > 0;

	/// <summary>How many severed limbs the body carries — the number the heal reset's answer names.</summary>
	internal static int CountMissing(Body body)
	{
		var limbs = body.limbs;
		var count = 0;
		for (var i = 0; i < limbs.Length; i++)
		{
			var limb = limbs[i];
			if (limb != null && limb.dismembered) // Unity object — ==
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Re-applies the limb-versus-body collision ignores. A limb that was inactive while it was gone comes back
	/// with its colliders re-enabled, and without this a player's own limbs collide with every body in the scene.
	/// </summary>
	private static void ReignoreCollisions(Body body)
	{
		// Every body the scene has, rather than the reference's `NetBody.all_instances`: that registry lives in an
		// assembly this project does not reference, and the bodies it holds are the same ones the scene exposes.
		var bodies = Object.FindObjectsOfType<Body>();
		var others = new List<Collider2D>();
		foreach (var other in bodies)
		{
			if (other == null || other.transform.parent == null) // Unity object — ==
			{
				continue;
			}

			others.AddRange(other.transform.parent.GetComponentsInChildren<Collider2D>(true));
		}

		if (others.Count == 0)
		{
			return;
		}

		foreach (var limb in body.limbs)
		{
			var collider = limb == null ? null : limb.GetComponent<Collider2D>(); // Unity object — ==
			if (collider == null) // Unity object — ==
			{
				continue;
			}

			foreach (var other in others)
			{
				if (other != null) // Unity object — ==
				{
					Physics2D.IgnoreCollision(collider, other, true);
				}
			}
		}
	}
}
