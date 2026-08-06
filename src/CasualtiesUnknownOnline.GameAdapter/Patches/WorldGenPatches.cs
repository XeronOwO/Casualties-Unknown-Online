using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation boundary hook. GenerateWorld is the coroutine that starts
/// procedural generation — the only correct point to capture/apply the host's
/// Random.state: anything the game consumes from Random before this moment
/// (scene loading, menu/update-time randomness) is already baked into the
/// captured state, so host and guest continue from identical RNG streams
/// (KrokMP does the same, LastBeforeGenerationState).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "GenerateWorld")]
internal static class WorldGenerationGenerateWorldPatch
{
	private static void Prefix() => GameAdapter.Instance?.OnWorldGenerate();
}

/// <summary>
/// Block damage sync (local compute, remote verify/sync): after ANY local
/// DamageBlock (mining/attacking blocks) the adapter reports it so the peer
/// applies the same damage at the same world position. The internal
/// Vector2Int overload (footstep-crushing fragile blocks, Body.cs:2709) is not
/// hooked — Phase 1 covers player block damage only.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "DamageBlock",
	new Type[] { typeof(Vector2), typeof(float), typeof(bool), typeof(bool) })]
internal static class WorldGenerationDamageBlockPatch
{
	private static void Postfix(Vector2 pos, float dmg) => GameAdapter.Instance?.OnBlockDamaged(pos, dmg);
}
