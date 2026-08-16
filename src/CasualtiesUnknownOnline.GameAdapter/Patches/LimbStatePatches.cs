using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Limb-latch triggers: <c>Limb.BreakBone</c> / <c>MendBone</c> /
/// <c>Dislocate</c> / <c>UnDislocate</c> / <c>Dismember</c> flip a limb's
/// presentation latch on the LOCAL body (falls, traps, bites, the amputation
/// minigame — Limb.cs:193-273; natural healing reaches MendBone/UnDislocate
/// through Limb.Update, Limb.cs:518-522). The post-event terminal state
/// travels as the dedicated <see cref="LimbStateEventMsg"/> event (one
/// operation = one message; the 1 Hz character snapshot stays the fallback).
/// The prefix captures the latch's pre-call value and the postfix reports only
/// a verified false→true / true→false TRANSITION (Harmony runs the postfix
/// regardless of the prefix — report only verified writes, user rule; a
/// repeated BreakBone on an already-broken limb only refreshes boneHealTimer,
/// which is not a latch edge). A render clone is never reported: its limbs are
/// frozen render state (the RemoteBodyDriver guard); the restore path writes
/// fields directly through the mapper, which never reaches these methods.
/// </summary>
internal static class LimbStatePatches
{
	[HarmonyPatch(typeof(Limb), "BreakBone")]
	internal static class BreakBonePatch
	{
		private static void Prefix(Limb __instance, out bool __state) => __state = __instance.broken;

		private static void Postfix(Limb __instance, bool __state) =>
			ReportVerified(__instance, !__state && __instance.broken);
	}

	[HarmonyPatch(typeof(Limb), "MendBone")]
	internal static class MendBonePatch
	{
		private static void Prefix(Limb __instance, out bool __state) => __state = __instance.broken;

		private static void Postfix(Limb __instance, bool __state) =>
			ReportVerified(__instance, __state && !__instance.broken);
	}

	[HarmonyPatch(typeof(Limb), "Dislocate")]
	internal static class DislocatePatch
	{
		private static void Prefix(Limb __instance, out bool __state) => __state = __instance.dislocated;

		private static void Postfix(Limb __instance, bool __state) =>
			ReportVerified(__instance, !__state && __instance.dislocated);
	}

	[HarmonyPatch(typeof(Limb), "UnDislocate")]
	internal static class UnDislocatePatch
	{
		private static void Prefix(Limb __instance, out bool __state) => __state = __instance.dislocated;

		private static void Postfix(Limb __instance, bool __state) =>
			ReportVerified(__instance, __state && !__instance.dislocated);
	}

	[HarmonyPatch(typeof(Limb), "Dismember")]
	internal static class DismemberPatch
	{
		private static void Prefix(Limb __instance, out bool __state) => __state = __instance.dismembered;

		private static void Postfix(Limb __instance, bool __state) =>
			ReportVerified(__instance, !__state && __instance.dismembered);
	}

	private static void ReportVerified(Limb limb, bool latchChanged)
	{
		if (latchChanged && limb.GetComponentInParent<RemoteBodyDriver>() == null) // Unity object — ==
		{
			PatchBridge.Impl?.OnLimbStateEvent(limb);
		}
	}
}
