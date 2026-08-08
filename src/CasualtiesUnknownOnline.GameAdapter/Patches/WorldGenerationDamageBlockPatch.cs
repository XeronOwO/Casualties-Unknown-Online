using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

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
	private static void Postfix(Vector2 pos, float dmg) => PatchBridge.Impl?.OnBlockDamaged(pos, dmg);
}

/// <summary>
/// Block-loot suppression on the guest. DamageBlock rolls block drops itself
/// (WorldGeneration.cs:751 — chance rolls on the local Random stream, per
/// block id). The damage is applied on BOTH sides (the BlockDamaged stream),
/// so without this both sides would roll their own independent drop — one
/// block yielding two items with different states. Only the host rolls (its
/// loot reports through the world-item domain and materializes identically
/// for the guests); the guest's DamageBlock calls pass ignoreLoot=true.
/// Every overload funnels through the Vector2Int one (WorldGeneration.cs:
/// 851-854), so this Prefix covers all paths.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "DamageBlock",
	new Type[] { typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool) })]
internal static class WorldGenerationDamageBlockLootPatch
{
	private static void Prefix(ref bool ignoreLoot)
	{
		if (PatchBridge.Impl is { IsGuestItemDropSuppressed: true })
		{
			ignoreLoot = true;
		}
	}
}
