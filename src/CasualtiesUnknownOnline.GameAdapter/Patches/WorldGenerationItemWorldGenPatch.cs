using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation distribution boundary for mod-bound custom item loose
/// spawns. The vanilla <c>WorldGeneration.PlaceCrystals</c> call runs inside
/// the sealed generation stream after colliders exist (the same point
/// CUCoreLib uses); the postfix scatters mod items on the ground, and the
/// existing generation-item snapshot synchronizes them. No wire message and no
/// game/Unity type crosses the mod boundary.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "PlaceCrystals")]
internal static class WorldGenerationItemWorldGenPatch
{
	private static void Postfix(WorldGeneration __instance) =>
		PatchBridge.Impl?.OnCustomItemWorldGeneration(__instance);
}
