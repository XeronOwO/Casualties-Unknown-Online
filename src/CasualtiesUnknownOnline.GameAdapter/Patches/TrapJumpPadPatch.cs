using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Jump pad → JumpPadLaunched (repeatable — the 15 s cooldown is the game's
/// own gate): a limb landed and the pad launched it (JumpPadScript.cs — light
/// flash + jumppad sound + shake; the launch itself hits the triggering side's
/// limbs). The event replays the visible state.
/// </summary>
[HarmonyPatch(typeof(JumpPadScript), "OnCollisionEnter2D")]
internal static class TrapJumpPadPatch
{
	private static void Postfix(JumpPadScript __instance, Collision2D collision)
	{
		if (!collision.gameObject.TryGetComponent<Limb>(out _))
		{
			return; // only the limb branch has a visible state (the body branch only ragdolls)
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.JumpPadLaunched, __instance.transform.position, 0);
	}
}
