using System;
using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Guest-side mine shielding: a locally simulated item (dynamic, carries
/// ItemInstanceId — see ItemPositionFollow) must not trip the local copy of a
/// mine — the trigger (MineScript.cs:44-52) checks no layer, only
/// !attachedRigidbody.isKinematic, so the layer isolation alone cannot
/// guarantee it. Blocking the trigger is a local event shield: the host's own
/// item-mine collision keeps original behaviour (the host's mine presses and
/// explodes there); the explosion sync is #123. Players (no ItemInstanceId)
/// trip mines as the game intends — their sync is #123 too.
/// </summary>
[HarmonyPatch(typeof(MineScript), "OnCollisionEnter2D")]
internal static class MineScriptPatches
{
	/// <summary>Set by the adapter: true while this side is a guest in a session.</summary>
	public static Func<bool>? ShouldShieldItems;

	private static bool Prefix(Collision2D collision)
	{
		if (ShouldShieldItems?.Invoke() != true)
		{
			return true;
		}

		var rb = collision.collider.attachedRigidbody;
		return rb == null || rb.GetComponentInParent<ItemInstanceId>() == null; // Unity object — ==
	}
}
