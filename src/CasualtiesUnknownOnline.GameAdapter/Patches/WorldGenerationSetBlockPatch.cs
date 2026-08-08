using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-mutation capture on the SetBlock write path (the one post-generation
/// write entry — mining, remote damage application, earthquakes, placement).
/// Host: diff against the generated baseline (damage table, late-joiner full
/// snapshot) + broadcast placements live. Guest: report local placements to
/// the host (breaking SetBlock(0) is already covered by the BlockDamaged
/// stream — only non-air writes are reported here). Generation itself is the
/// baseline and is excluded via generatingWorld; SetBlockNoUpdate is
/// generation-only and intentionally not hooked.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "SetBlock")]
internal static class WorldGenerationSetBlockPatch
{
	private static void Postfix(Vector2Int pos, ushort block) => PatchBridge.Impl?.OnBlockSet(pos, block);
}
