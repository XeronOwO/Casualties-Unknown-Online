using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// <c>PlushScript.OnCollisionEnter2D</c> (PlushScript.cs:17-23) squeaks on any
/// impact above its low velocity threshold. A guest plushie copy is the same
/// non-authoritative presentation case; only the player's explicit use action
/// may squeak locally.
/// <para>
/// As with every other member of the family, the authority half rides the same
/// hook: the side that simulates the collision reports its own impact, so the
/// guests replay the squeak instead of hearing silence. The plush's sound index
/// travels with the report — the receiver's copy rolls its own index, so the
/// exact clip is the one fact only the reporting side knows.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PlushScript), "OnCollisionEnter2D")]
internal static class PlushScriptCollisionEnter2DPatch
{
	private static bool Prefix(PlushScript __instance)
	{
		var item = __instance.GetComponent<Item>();
		if (item != null && NonAuthoritativeItemImpactGuard.Suppress(item, "PlushScript.OnCollisionEnter2D")) // Unity object — ==
		{
			return false;
		}

		return true;
	}

	private static void Postfix(PlushScript __instance, Collision2D collision)
	{
		if (collision.relativeVelocity.magnitude <= 2f)
		{
			return;
		}

		var item = __instance.GetComponent<Item>();
		if (item == null || !NonAuthoritativeItemImpactGuard.ShouldReport(item)) // Unity object — ==
		{
			return;
		}

		PatchBridge.Impl?.OnWorldItemImpact(
			__instance.transform.position,
			ItemImpactKind.PlushSqueak,
			(byte)Mathf.Clamp(__instance.index, 0, byte.MaxValue));
	}
}
