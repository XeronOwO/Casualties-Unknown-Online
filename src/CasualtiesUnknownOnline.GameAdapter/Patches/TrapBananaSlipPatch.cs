using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Banana plant → BananaPlantSlip (repeatable): a fast body tripped over it
/// (BananaPlantSlip.cs — plantslip sound; the slide hits the triggering side's
/// limbs). The event replays the sound.
/// </summary>
[HarmonyPatch(typeof(BananaPlantSlip), "OnTriggerEnter2D")]
internal static class TrapBananaSlipPatch
{
	private static void Postfix(BananaPlantSlip __instance, Collider2D collision)
	{
		if (!collision.TryGetComponent<Body>(out var body) || Mathf.Abs(body.rb.velocity.x) <= 5f)
		{
			return; // the game's own trigger condition
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.BananaPlantSlip, __instance.transform.position, 0);
	}
}
