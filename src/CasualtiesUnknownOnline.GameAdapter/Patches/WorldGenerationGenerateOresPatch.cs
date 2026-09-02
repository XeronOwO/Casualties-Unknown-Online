using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation distribution boundary for mod-bound custom tile ore. The
/// vanilla <c>WorldGeneration.GenerateOres</c> method runs synchronously inside
/// the terrain coroutine, so a postfix here keeps custom tile placement on the
/// same sealed generation stream as the vanilla copper/ilmenite passes. No
/// game/Unity type crosses the mod boundary; only the Game Adapter consumes mod
/// DTOs.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "GenerateOres")]
internal static class WorldGenerationGenerateOresPatch
{
	private static void Postfix(WorldGeneration __instance) =>
		PatchBridge.Impl?.OnCustomTileOreGeneration(__instance);
}
