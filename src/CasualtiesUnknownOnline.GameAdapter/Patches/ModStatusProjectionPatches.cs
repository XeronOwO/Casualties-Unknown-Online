using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Mod-status vanilla projection hooks (phase 3). The projection service cannot
/// be constructor-injected into static patches, so these postfixes only forward
/// to <see cref="PatchBridge"/>; the bridge decides whether the body/limb is the
/// local player's and the projection service owns the typed decode/apply.
///
/// Both methods run after the native update because the projection is an
/// additive overlay: re-asserting it after the game's own formulas/limb tick
/// keeps the mod contribution from being erased by the next native write.
/// </summary>
internal static class ModStatusProjectionPatches
{
	[HarmonyPatch(typeof(Body), "Update")]
	internal static class BodyStatusProjectionPatch
	{
		private static void Postfix(Body __instance) =>
			PatchBridge.Impl?.ApplyBodyStatusProjection(__instance);
	}

	[HarmonyPatch(typeof(Limb), "Update")]
	internal static class LimbStatusProjectionPatch
	{
		private static void Postfix(Limb __instance)
		{
			var body = __instance.body;
			if (body != null) // Unity object — ==
			{
				PatchBridge.Impl?.ApplyLimbStatusProjection(body, __instance);
			}
		}
	}
}
