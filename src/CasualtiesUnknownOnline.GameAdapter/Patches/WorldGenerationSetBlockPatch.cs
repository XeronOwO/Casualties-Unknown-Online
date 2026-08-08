using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-mutation capture on the SetBlock write path (the one post-generation
/// write entry — mining, remote damage application, earthquakes, placement).
/// Host: diff against the generated baseline (damage table, late-joiner full
/// snapshot) + broadcast the mutation live (air writes included — the quake
/// breaks SetBlock(0) with per-side random, so without the relay the terrain
/// diverges). Guest: report local mutations for arbitration (mining
/// double-reports via BlockDamaged — idempotent). Generation itself is the
/// baseline and is excluded via generatingWorld; SetBlockNoUpdate is
/// generation-only and intentionally not hooked.
/// QUAKE REGIONS are the UNION of every side's breaks: each side quakes on
/// the synced timer and breaks its own nearby region; the air-write relay
/// applies every side's breaks everywhere, and SetBlock(0) is idempotent —
/// overlapping regions are computed exactly once (user mandate).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "SetBlock")]
internal static class WorldGenerationSetBlockPatch
{
	private static void Postfix(Vector2Int pos, ushort block) => PatchBridge.Impl?.OnBlockSet(pos, block);
}
