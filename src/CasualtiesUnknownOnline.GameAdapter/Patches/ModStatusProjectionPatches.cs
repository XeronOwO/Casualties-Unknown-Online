using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Mod-status vanilla projection hooks (phase 3). The projection service cannot
/// be constructor-injected into static patches, so these patch methods only forward
/// to <see cref="PatchBridge"/>; the bridge decides whether the body/limb is the
/// local player's and the projection service owns the typed decode/apply.
///
/// The body/limb postfixes re-assert additive overlays after the native
/// update. The circulation patch instead wraps <c>Body.HandleCirculation</c>
/// with a prefix/postfix pair: the previous offset is removed before the
/// native formula and the current offset is reapplied after it, because
/// circulation fields are continuously recomputed and cannot be kept correct
/// by a post-update-only additive write.
/// </summary>
internal static class ModStatusProjectionPatches
{
	[HarmonyPatch(typeof(Body), "Update")]
	internal static class BodyStatusProjectionPatch
	{
		private static void Postfix(Body __instance) =>
			PatchBridge.Impl?.ApplyBodyStatusProjection(__instance);
	}

	[HarmonyPatch(typeof(Body), "HandleCirculation")]
	internal static class BodyCirculationProjectionPatch
	{
		private static void Prefix(Body __instance) =>
			PatchBridge.Impl?.ApplyBodyCirculationPrefix(__instance);

		private static void Postfix(Body __instance) =>
			PatchBridge.Impl?.ApplyBodyCirculationPostfix(__instance);
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
