using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// <c>Item.OnCollisionEnter2D</c> (Item.cs:238-247) plays the native
/// drop/random-step sounds and spawns DustMini whenever a world item impacts
/// anything above the velocity threshold. On a guest these effects belong to
/// the host's authoritative copy, not the local presentation clone — the guest
/// copy only simulates for smoothness, so foreground/background frame changes
/// must not make its local collisions audible.
/// </summary>
[HarmonyPatch(typeof(Item), "OnCollisionEnter2D")]
internal static class ItemCollisionEnter2DPatch
{
	private static bool Prefix(Item __instance)
	{
		if (NonAuthoritativeItemImpactGuard.Suppress(__instance, "Item.OnCollisionEnter2D"))
		{
			return false;
		}

		return true;
	}
}
