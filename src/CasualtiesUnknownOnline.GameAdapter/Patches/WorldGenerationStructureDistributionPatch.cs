using System.Collections;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation distribution boundary for mod-bound structures. The vanilla
/// <c>WorldGenerateWorldBorders</c> coroutine is the same point CUCoreLib uses:
/// terrain and vanilla structures are already generated, and the collider /
/// <c>UpdateWorld</c> pass has not run yet. The postfix wraps the returned
/// iterator so <see cref="WorldGenRandomIsolation"/> drives the additional
/// placement as part of the same sealed generation stream. No game/Unity type
/// crosses the mod boundary; only the Game Adapter consumes mod DTOs.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "WorldGenerateWorldBorders")]
internal static class WorldGenerationStructureDistributionPatch
{
	private static IEnumerator Postfix(IEnumerator __result, WorldGeneration __instance)
	{
		var adapter = PatchBridge.Impl;
		return adapter is null ? __result : adapter.WrapStructureWorldGen(__result, __instance);
	}
}
