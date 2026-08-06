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
		private static bool Prefix(Body __instance)
		{
			var driver = __instance.GetComponent<RemoteBodyDriver>();
			if (driver != null && !driver.simulated)
				return false; // render proxy: position/velocity come from the session
			return true;
		}
	}

	[HarmonyPatch(typeof(Body), "Start")]
	internal static class BodyStartPatch
	{
		private static void Postfix(Body __instance)
		{
			Physics2D.IgnoreLayerCollision(
				LayerMask.GetMask("Player"), LayerMask.GetMask("Player"), true);

			var bodies = Object.FindObjectsOfType<Body>();
			if (bodies.Length < 2)
				return;
			foreach (var other in bodies)
			{
				if (other == __instance)
					continue;
				var self = __instance.transform.parent?.GetComponentsInChildren<Collider2D>();
				var otherColliders = other.transform.parent?.GetComponentsInChildren<Collider2D>();
				if (self == null || otherColliders == null)
					continue;
				foreach (var a in self)
					foreach (var b in otherColliders)
						Physics2D.IgnoreCollision(a, b, true);
			}
		}
	}
}
