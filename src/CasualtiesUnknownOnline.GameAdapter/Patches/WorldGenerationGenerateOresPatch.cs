using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation distribution boundary for mod-bound custom tile ore and
/// liquid-tile pools. The vanilla <c>WorldGeneration.GenerateOres</c> method
/// runs synchronously inside the terrain coroutine, so a postfix here keeps
/// both custom placements on the same sealed generation stream as the vanilla
/// copper/ilmenite and water passes. The two calls use one explicit, fixed
/// order (tile ore then liquid tile), so both peers consume the shared random
/// stream identically. No game/Unity type crosses the mod boundary; only the
/// Game Adapter consumes mod DTOs.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "GenerateOres")]
internal static class WorldGenerationGenerateOresPatch
{
	private static void Postfix(WorldGeneration __instance)
	{
		PatchBridge.Impl?.OnCustomTileOreGeneration(__instance);
		PatchBridge.Impl?.OnCustomLiquidWorldGeneration(__instance);
	}
}
