using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Adds the carried/rider player's share of the load to a local carrier's
/// <c>Body.GetTotalEncumberance()</c>. The server/authority computes the
/// snapshot contribution through <see cref="PatchBridge"/>, so the Harmony
/// adapter stays thin and the rule remains testable behind the bridge.
/// </summary>
[HarmonyPatch(typeof(Body), "GetTotalEncumberance")]
internal static class CarryEncumbrancePatch
{
	private static void Postfix(Body __instance, ref float __result)
	{
		if (PatchBridge.Impl is not { } bridge)
		{
			return;
		}

		__result += bridge.GetCarriedEncumbrance(__instance);
	}
}
