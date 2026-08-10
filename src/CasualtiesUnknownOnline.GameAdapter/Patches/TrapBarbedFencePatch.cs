using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Barbed fence → BarbedFenceHit (repeatable — the 10 s sound cooldown is the
/// game's own gate): a limb hit the fence (BarbedFence.cs — hitSprite + fence
/// sound + the local limb's damage). The damage happens on the triggering
/// side's limb; the event replays the visible state (hitSprite + sound).
/// </summary>
[HarmonyPatch(typeof(BarbedFence), "OnTriggerEnter2D")]
internal static class TrapBarbedFencePatch
{
	private static void Postfix(BarbedFence __instance, Collider2D collision)
	{
		if (!collision.TryGetComponent<Limb>(out _))
		{
			return; // only the limb branch has a visible state
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.BarbedFenceHit, __instance.transform.position, 0);
	}
}
