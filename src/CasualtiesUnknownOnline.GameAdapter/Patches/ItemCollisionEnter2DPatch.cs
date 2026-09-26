using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// <c>Item.OnCollisionEnter2D</c> (Item.cs:238-247) plays the native
/// drop/random-step sounds and spawns DustMini whenever a world item impacts
/// anything above the velocity threshold. On a guest these effects belong to
/// the host's authoritative copy, not the local presentation clone — the guest
/// copy only simulates for smoothness, so foreground/background frame changes
/// must not make its local collisions audible.
/// <para>
/// The same hook carries the AUTHORITY half: the side that simulates the landing
/// keeps the native presentation and reports it, so every guest replays the same
/// drop, step and dust instead of hearing and seeing nothing at all — its own
/// drops included. The report rides the native body's own condition
/// (<c>relativeVelocity.magnitude &gt; 3f</c>) rather than inventing a second one.
/// </para>
/// </summary>
[HarmonyPatch(typeof(Item), "OnCollisionEnter2D")]
internal static class ItemCollisionEnter2DPatch
{
	private static bool Prefix(Item __instance)
	{
		if (NonAuthoritativeItemImpactGuard.Suppress(__instance, "Item.OnCollisionEnter2D"))
		{
			return false;
		}

		return true;
	}

	private static void Postfix(Item __instance, Collision2D collision)
	{
		if (collision.relativeVelocity.magnitude > 3f && NonAuthoritativeItemImpactGuard.ShouldReport(__instance))
		{
			PatchBridge.Impl?.OnWorldItemImpact(__instance.transform.position, ItemImpactKind.ItemImpact, soundIndex: 0);
		}
	}
}
