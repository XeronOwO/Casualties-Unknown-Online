using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Block-drop identification: a Utils.Create(string, Vector2, float) call
/// INSIDE a local DamageBlock roll (the DamageBlockOrigin scope — the roll
/// loop, WorldGeneration.cs:751-837, is where every block drop is created)
/// gets a DropOrigin marker with the exact spawn position. Items created
/// elsewhere (use-spawned, creature loot, starting supplies) never see the
/// scope and stay unmasked. The marker cannot be read from Item.allItems at
/// report time: the list registers in Item.Start, a frame after Instantiate.
/// </summary>
[HarmonyPatch(typeof(Utils), "Create",
	[typeof(string), typeof(Vector2), typeof(float)])]
internal static class UtilsCreateDropPatch
{
	private static void Postfix(GameObject? __result, Vector2 pos)
	{
		if (__result == null || CallContext.Current != CallContext.Origin.DamageBlockOrigin)
		{
			return;
		}

		// Only item spawns are drops — particle/Special spawns inside the roll
		// (the break dust) have no Item component and must not be marked.
		if (__result.GetComponent<Item>() != null) // Unity object — ==
		{
			__result.AddComponent<DropOrigin>().SpawnPosition = pos;
		}
	}
}
