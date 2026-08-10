using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Cactus → CactusHit (repeatable): a body bumped it (CactusScript.cs — gore
/// sound + the local body's knock/damage; the cactus takes self-damage that
/// stays local-only, a recorded small divergence). The event replays the
/// sound.
/// </summary>
[HarmonyPatch(typeof(CactusScript), "OnCollisionEnter2D")]
internal static class TrapCactusPatch
{
	private static void Postfix(CactusScript __instance, Collision2D collision)
	{
		if (!collision.gameObject.TryGetComponent<Body>(out _))
		{
			return; // only the body branch has a visible effect
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CactusHit, __instance.transform.position, 0);
	}
}
