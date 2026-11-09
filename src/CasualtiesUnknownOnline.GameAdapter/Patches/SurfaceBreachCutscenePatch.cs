using System.Collections;
using CasualtiesUnknownOnline.GameState.Domains.World;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>The surface-breach branch — see <see cref="MercEndingPatch"/>.</summary>
[HarmonyPatch(typeof(WorldGeneration), "SurfaceBreachCutscene")]
internal static class SurfaceBreachCutscenePatch
{
	private static bool Prefix(WorldGeneration __instance, ref IEnumerator __result) =>
		EndingBranch.Redirect(__instance, RunEnding.SurfaceBreach, ref __result);
}
