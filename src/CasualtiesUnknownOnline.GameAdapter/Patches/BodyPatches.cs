using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Body patches: freeze render proxies (no local physics) and make multiple
/// players pass through each other (clone colliders would otherwise fight the
/// local body — same treatment KrokMP applies in Body_Start).
/// </summary>
internal static class BodyPatches
{
	[HarmonyPatch(typeof(Body), "FixedUpdate")]
	internal static class BodyFixedUpdatePatch
	{
		private static bool Prefix(Body __instance) => __instance.GetComponent<RemoteBodyDriver>() is null;
	}
	// NOTE: Body.Update intentionally runs on render proxies — HandleVisuals
	// initializes the limb sprites and the animation system needs the update to
	// drive walk/jump poses. Only the physics (FixedUpdate + rb.simulated=false)
	// is frozen; the session overwrites the root transform each frame.

	[HarmonyPatch(typeof(Body), "Start")]
	internal static class BodyStartPatch
	{
		private static void Postfix(Body __instance)
		{
			Physics2D.IgnoreLayerCollision(
				LayerMask.GetMask("Player"), LayerMask.GetMask("Player"), true);

			var bodies = Object.FindObjectsOfType<Body>();
			if (bodies.Length < 2)
			{
				return;
			}

			foreach (var other in bodies)
			{
				if (other == __instance)
				{
					continue;
				}

				var self = __instance.transform.parent?.GetComponentsInChildren<Collider2D>();
				var otherColliders = other.transform.parent?.GetComponentsInChildren<Collider2D>();
				if (self is null || otherColliders is null)
				{
					continue;
				}

				foreach (var a in self)
				{
					foreach (var b in otherColliders)
					{
						Physics2D.IgnoreCollision(a, b, true);
					}
				}
			}
		}
	}
}
