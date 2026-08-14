using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Enemy-freeze patches: a guest-side enemy marked with
/// <see cref="RemoteEnemyDriver"/> must not simulate its AI or physics — its
/// position/rotation/health come from the host's snapshot. Each enemy AI
/// script's Update/FixedUpdate is skipped when the marker is present (the same
/// pattern as BodyPatches for the player render clones). The enemy-script list
/// is locked by EnemyFreezePatchContractTests: a new enemy AI script with an
/// Update/FixedUpdate fails the contract until a freeze patch is added.
///
/// Scope note: these are the MOVING animals (physics/AI-driven position —
/// SpiderHandler.cs:114-133, CrystalEnemy.cs:169-191). Static traps that also
/// carry BuildingEntity (GrabberPlant, ScrapEater) are event-synced by their
/// own Trap* patches and are NOT frozen here.
/// </summary>
internal static class EnemyPatches
{
	[HarmonyPatch(typeof(SpiderHandler), "Update")]
	internal static class SpiderHandlerUpdatePatch
	{
		private static bool Prefix(SpiderHandler __instance) => __instance.GetComponentInParent<RemoteEnemyDriver>() == null;
	}

	[HarmonyPatch(typeof(SpiderHandler), "FixedUpdate")]
	internal static class SpiderHandlerFixedUpdatePatch
	{
		private static bool Prefix(SpiderHandler __instance) => __instance.GetComponentInParent<RemoteEnemyDriver>() == null;
	}

	[HarmonyPatch(typeof(CrystalEnemy), "Update")]
	internal static class CrystalEnemyUpdatePatch
	{
		private static bool Prefix(CrystalEnemy __instance) => __instance.GetComponentInParent<RemoteEnemyDriver>() == null;
	}

	[HarmonyPatch(typeof(CrystalEnemy), "FixedUpdate")]
	internal static class CrystalEnemyFixedUpdatePatch
	{
		private static bool Prefix(CrystalEnemy __instance) => __instance.GetComponentInParent<RemoteEnemyDriver>() == null;
	}
}
