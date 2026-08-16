using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Heater cooker conversion hook (Heater.OnCollisionEnter2D, Heater.cs:41-49).
/// The native original remains the ONLY conversion implementation on the
/// host/solo side; this patch verifies the created steak and commits one
/// ItemCook report. A guest in an active session skips the original — its
/// world items are layer-isolated to the Ground layer, so the host's copy is
/// the only full-physics copy that can legitimately cook, and the guest
/// replays the host's broadcast. If the postfix cannot identify the created
/// steak, it claims nothing and the existing generic ItemDestroy + ItemSpawn
/// hooks remain the self-healing fallback.
/// </summary>
[HarmonyPatch(typeof(Heater), "OnCollisionEnter2D")]
internal static class HeaterCookPatch
{
	/// <summary>Per-call state crossing Prefix → Postfix — never a static field.</summary>
	private sealed class CookState
	{
		internal CookState(ulong sourceItemId, float sourceCondition, Vector2 sourcePosition)
		{
			SourceItemId = sourceItemId;
			SourceCondition = sourceCondition;
			SourcePosition = sourcePosition;
		}

		internal ulong SourceItemId { get; }

		internal float SourceCondition { get; }

		internal Vector2 SourcePosition { get; }
	}

	private static bool Prefix(Heater __instance, Collision2D collision, out CookState? __state)
	{
		__state = null;
		if (PatchBridge.Impl is not { } bridge || !__instance.cooker)
		{
			return true;
		}

		var item = collision.gameObject.GetComponent<Item>();
		if (item == null || !HeaterCookRule.IsCookCandidate(__instance.cooker, item.Stats.HasTag("meat"), item.id)) // Unity object — ==
		{
			return true;
		}

		// The host's full-physics scene owns the conversion; a guest in a live
		// session must never cook locally (layer isolation already prevents the
		// collision — this is the explicit authority gate, not just physics).
		if (!bridge.IsHeaterCookAuthority)
		{
			return false;
		}

		// Stamp the raw item BEFORE the native Destroy — a generation-time meat
		// entering the domain through the cooker still needs an instance id.
		var sourceItemId = bridge.OnHeaterCookBegin(item);
		if (sourceItemId == 0)
		{
			return true; // not reportable (remote apply / still generating) — fall back to the generic hooks
		}

		__state = new CookState(sourceItemId, item.condition, item.transform.position);
		return true;
	}

	private static void Postfix(Collision2D collision, CookState? __state)
	{
		if (__state == null || PatchBridge.Impl is not { } bridge)
		{
			return;
		}

		// The native original created the steak in the same physics callback,
		// before its Item.Start ran — Item.allItems therefore does NOT contain
		// it yet (Item.cs:112-118), while any pre-existing steak is registered.
		var steak = FindCreatedSteak(__state);
		if (steak == null) // Unity object — ==
		{
			bridge.OnHeaterCookCaptureFailed(__state.SourceItemId);
			return;
		}

		bridge.OnHeaterCookCompleted(__state.SourceItemId, steak, __state.SourceCondition, __state.SourcePosition);
	}

	private static Item? FindCreatedSteak(CookState state)
	{
		Item? best = null;
		var bestDistanceSqr = float.MaxValue;
		foreach (var item in Object.FindObjectsOfType<Item>())
		{
			if (item == null // Unity object — ==
				|| item.id != HeaterCookRule.CookedItemId
				|| Item.allItems.Contains(item)
				|| item.GetComponent<ItemInstanceId>() != null // Unity object — ==; already part of the item domain
				|| !HeaterCookRule.IsCookedCondition(item.condition, state.SourceCondition)
				|| !HeaterCookRule.IsCookedSpawnAt(item.transform.position.x, item.transform.position.y, state.SourcePosition.x, state.SourcePosition.y))
			{
				continue;
			}

			var dx = item.transform.position.x - state.SourcePosition.x;
			var dy = item.transform.position.y - state.SourcePosition.y;
			var distanceSqr = (dx * dx) + (dy * dy);
			if (best == null || distanceSqr < bestDistanceSqr) // Unity object — ==
			{
				best = item;
				bestDistanceSqr = distanceSqr;
			}
		}

		return best;
	}
}
