using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Blood terminal → BioTerminalUnlocked: the success branch Backgroundify()s
/// the terminal (BioTerminalScript.cs:33 — the collider flips disabled), the
/// deny branch only plays a sound. The collider transition IS the verified
/// success verdict (reports must only ride verified commits — the deny path
/// must never broadcast). The blood consumption already happened on the
/// trigger side (item domain); the event replays the unlock.
/// </summary>
[HarmonyPatch(typeof(BioTerminalScript), "OnUse")]
internal static class TrapBioTerminalPatch
{
	private static void Prefix(BioTerminalScript __instance, out bool __state)
	{
		var collider = __instance.GetComponent<Collider2D>();
		__state = collider != null && collider.enabled; // Unity object — ==
	}

	private static void Postfix(BioTerminalScript __instance, bool __state)
	{
		var collider = __instance.GetComponent<Collider2D>();
		if (__state || collider == null || collider.enabled) // Unity object — ==
		{
			return; // not a just-disabled transition (deny path, or already unlocked)
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.BioTerminalUnlocked, __instance.transform.position, 0);
	}
}
