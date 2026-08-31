using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Thin GunScript transition hooks: after every native path that can change
/// the gun's persistent state (fire, manual rack, safety, load, unload) and
/// after Update (so timed auto-rack/auto-unrack transitions are caught too),
/// the patch reports the gun to <see cref="GunStateSync"/>. The sync domain
/// deduplicates against the last reported snapshot and only routes an actual
/// state change through the existing item-use fact path — no cross-call state
/// is held by these patch classes.
/// </summary>
internal static class GunStatePatches
{
	[HarmonyPatch(typeof(GunScript), "Update")]
	internal static class UpdatePatch
	{
		private static void Postfix(GunScript __instance) =>
			PatchBridge.Impl?.OnGunStateChanged(__instance);
	}

	[HarmonyPatch(typeof(GunScript), "Fire", [typeof(bool)])]
	internal static class FirePatch
	{
		private static void Postfix(GunScript __instance) =>
			PatchBridge.Impl?.OnGunStateChanged(__instance);
	}

	[HarmonyPatch(typeof(GunScript), "TryRack")]
	internal static class TryRackPatch
	{
		private static void Postfix(GunScript __instance) =>
			PatchBridge.Impl?.OnGunStateChanged(__instance);
	}

	[HarmonyPatch(typeof(GunScript), "ToggleSafety")]
	internal static class ToggleSafetyPatch
	{
		private static void Postfix(GunScript __instance) =>
			PatchBridge.Impl?.OnGunStateChanged(__instance);
	}

	[HarmonyPatch(typeof(GunScript), "LoadMag", [typeof(AmmoScript)])]
	internal static class LoadMagPatch
	{
		private static void Postfix(GunScript __instance) =>
			PatchBridge.Impl?.OnGunStateChanged(__instance);
	}

	[HarmonyPatch(typeof(GunScript), "UnloadMag")]
	internal static class UnloadMagPatch
	{
		private static void Postfix(GunScript __instance) =>
			PatchBridge.Impl?.OnGunStateChanged(__instance);
	}
}
