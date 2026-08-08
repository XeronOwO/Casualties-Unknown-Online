using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Host-authoritative drops: a guest's DamageBlock must never roll the block's
/// loot locally (WorldGeneration.cs:751) — the host's own DamageBlock (its
/// local attack, or the applied report of the guest's) rolls it and broadcasts
/// the items through the item domain. Without this, both sides rolled an
/// independent drop on their own Random streams ("the dropped items are at
/// different spots" — same spawn position, different physics).
/// The 4-argument overload calls this one with ignoreLoot=false hardcoded
/// (WorldGeneration.cs:851), so the local-attack path is covered here too.
/// Remote applications already pass ignoreLoot=true; forcing it on the guest
/// makes every guest-side call a no-roll, whichever path it came from.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "DamageBlock",
	new Type[] { typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool) })]
internal static class WorldGenerationDamageBlockLootPatch
{
	private static void Prefix(ref bool ignoreLoot)
	{
		// Solo play (no session) rolls normally; only a live guest never rolls.
		if (PatchBridge.Impl is { IsSessionActive: true } bridge && bridge.IsHostMode == false)
		{
			ignoreLoot = true;
		}
	}
}
