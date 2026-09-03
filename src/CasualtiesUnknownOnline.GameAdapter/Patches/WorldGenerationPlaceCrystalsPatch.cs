using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation distribution boundary for mod-bound custom building
/// entities and custom item loose spawns. The vanilla
/// <c>WorldGeneration.PlaceCrystals</c> call runs inside the sealed generation
/// stream after colliders exist (the same point CUCoreLib uses); the postfix
/// distributes custom buildings and scatters mod items on the ground. Buildings
/// are deterministic and never need a wire message; the existing
/// generation-item snapshot synchronizes loose items. The two passes run in
/// one explicit fixed order (buildings then items), so both peers consume the
/// shared generation random stream identically. No wire message and no
/// game/Unity type crosses the mod boundary.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "PlaceCrystals")]
internal static class WorldGenerationPlaceCrystalsPatch
{
	private static void Postfix(WorldGeneration __instance)
	{
		PatchBridge.Impl?.OnCustomBuildingWorldGeneration(__instance);
		PatchBridge.Impl?.OnCustomItemWorldGeneration(__instance);
	}
}
