using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Beartrap → BearTrapClamped / BearTrapReleased (repeatable, both directions):
/// a limb entered and the trap closed (BearTrap.cs:24-56 — activated + the
/// clamped limb), or the caught body stood up and the trap released
/// (BearTrap.cs:61-69 — caughtLimb cleared, sprite restored). The clamp's
/// damage happens on the triggering side (its OWN limb is clamped); the event
/// syncs the trap's visible state on the peers.
/// </summary>
internal static class TrapBearTrapPatch
{
	[HarmonyPatch(typeof(BearTrap), "OnTriggerEnter2D")]
	internal static class ClampPatch
	{
		private static void Prefix(BearTrap __instance, out bool __state) =>
			__state = Traverse.Create(__instance).Field("activated").GetValue<bool>();

		private static void Postfix(BearTrap __instance, bool __state)
		{
			if (__state || !Traverse.Create(__instance).Field("activated").GetValue<bool>())
			{
				return; // not the open → clamped transition
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.BearTrapClamped, __instance.transform.position, 0);
		}
	}

	[HarmonyPatch(typeof(BearTrap), "Update")]
	internal static class ReleasePatch
	{
		private static void Prefix(BearTrap __instance, out bool __state) => __state = __instance.caughtLimb != null; // Unity object — ==

		private static void Postfix(BearTrap __instance, bool __state)
		{
			if (!__state || __instance.caughtLimb != null) // Unity object — ==
			{
				return; // not the clamped → released transition
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.BearTrapReleased, __instance.transform.position, 0);
		}
	}
}
