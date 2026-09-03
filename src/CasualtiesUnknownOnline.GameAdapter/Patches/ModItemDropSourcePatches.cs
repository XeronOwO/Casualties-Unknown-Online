using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Opts fixed vanilla loot containers into the mod-authored explicit
/// drop-source pools. The pools are represented as synthetic
/// <c>ItemLootPool</c> categories by
/// <see cref="Content.GameAdapterItemContentProvider"/>, so these patches only
/// add that category to the existing per-source category list and let the
/// vanilla loot code consume it. No new wire message, no game/Unity type
/// crosses Abstractions, and no extra Random consumption is introduced beyond
/// the host-only trader stock generation (guests already receive the host's
/// authoritative trader stock).
/// </summary>
internal static class ModItemDropSourcePatches
{
	private static readonly string[] VanillaTraderCategories =
	[
		"medical", "food", "water", "tool", "drug", "container", "utility", "custom"
	];

	[HarmonyPatch(typeof(CorpseScript), "Start")]
	internal static class CorpseDropSourcePatch
	{
		private static void Prefix(CorpseScript __instance)
		{
			if (__instance.animalCorpse)
			{
				return;
			}

			if (PatchBridge.Impl is not { } bridge
				|| !bridge.TryGetModDropSourceCategory(ModItemDropSource.Corpse, out var category))
			{
				return;
			}

			__instance.categories = AppendCategory(__instance.categories, category);
		}
	}

	[HarmonyPatch(typeof(BuildingEntity), "Start")]
	internal static class BuildingDropSourcePatch
	{
		private static void Prefix(BuildingEntity __instance)
		{
			if (!TryResolveBuildingSource(__instance.id, out var source))
			{
				return;
			}

			if (PatchBridge.Impl is not { } bridge
				|| !bridge.TryGetModDropSourceCategory(source, out var category))
			{
				return;
			}

			__instance.itemCategoriesToAdd = AppendCategory(__instance.itemCategoriesToAdd, category);
		}
	}

	[HarmonyPatch(typeof(TraderScript), "GenerateSingleItemList")]
	internal static class TraderDropSourcePatch
	{
		private static bool Prefix(
			TraderScript __instance,
			TraderScript.TraderItemPreference pref,
			ref List<TraderItem> __result)
		{
			if (PatchBridge.Impl is not { } bridge
				|| !TryAppendTraderCategories(__instance, bridge, out var categories))
			{
				return true;
			}

			__result = BuildTraderList(__instance, pref, categories);
			return false;
		}
	}

	private static string[]? AppendCategory(string[]? categories, string category)
	{
		if (string.IsNullOrEmpty(category))
		{
			return categories;
		}

		if (categories is null)
		{
			return [category];
		}

		if (Array.IndexOf(categories, category) >= 0)
		{
			return categories;
		}

		var next = new string[categories.Length + 1];
		Array.Copy(categories, next, categories.Length);
		next[next.Length - 1] = category;
		return next;
	}

	private static bool TryResolveBuildingSource(string? id, out ModItemDropSource source)
	{
		source = ModItemDropSource.None;
		if (string.IsNullOrWhiteSpace(id))
		{
			return false;
		}

		var normalized = (id ?? string.Empty).Trim().ToLowerInvariant();
		source = normalized switch
		{
			"medcrate" => ModItemDropSource.MedicalCrate,
			"foodbox" => ModItemDropSource.FoodCrate,
			"containercrate" => ModItemDropSource.ContainerCrate,
			"lifepodchest" => ModItemDropSource.CapsuleContainer,
			"dropcapsule" => ModItemDropSource.DropCapsule,
			_ => ModItemDropSource.None
		};

		return source != ModItemDropSource.None;
	}

	private static bool TryAppendTraderCategories(
		TraderScript trader,
		IPatchBridge bridge,
		out List<string> categories)
	{
		categories = [.. VanillaTraderCategories];

		var added = false;
		foreach (var source in ResolveTraderSources(trader))
		{
			if (!bridge.TryGetModDropSourceCategory(source, out var category)
				|| string.IsNullOrEmpty(category)
				|| categories.Contains(category))
			{
				continue;
			}

			categories.Add(category);
			added = true;
		}

		return added;
	}

	private static ModItemDropSource[] ResolveTraderSources(TraderScript trader)
	{
		if (trader != null)
		{
			var name = trader.gameObject != null
				? trader.gameObject.name.ToLowerInvariant()
				: string.Empty;

			if (name.Contains("trader1"))
			{
				return [ModItemDropSource.Trader1];
			}

			if (name.Contains("trader2"))
			{
				return [ModItemDropSource.Trader2];
			}

			if (name.Contains("trader3"))
			{
				return [ModItemDropSource.Trader3];
			}
		}

		return trader?.character switch
		{
			0 => [ModItemDropSource.Trader1],
			1 => [ModItemDropSource.Trader2],
			2 => [ModItemDropSource.Trader3],
			_ => [ModItemDropSource.Trader1, ModItemDropSource.Trader2, ModItemDropSource.Trader3]
		};
	}

	private static List<TraderItem> BuildTraderList(
		TraderScript trader,
		TraderScript.TraderItemPreference pref,
		List<string> categories)
	{
		var result = new List<TraderItem>();
		var amount = Mathf.RoundToInt(Random.Range(2, 9) * WorldGeneration.GetRunSettingFloat("traderitemamount"));
		if (trader.character == 2)
		{
			amount = Mathf.RoundToInt(amount * 0.66f);
		}

		if (categories.Count == 0)
		{
			return result;
		}

		for (var i = 0; i < amount; i++)
		{
			for (var attempt = 0; attempt < 24; attempt++)
			{
				var category = categories[Random.Range(0, categories.Count)];
				var chosen = ItemLootPool.RandomFromPool(category);
				if (chosen.Item2 is null || chosen.Item2.value <= 0)
				{
					continue;
				}

				result.Add(new TraderItem
				{
					preference = pref,
					id = chosen.Item1,
					bought = false,
					value = chosen.Item2.DefaultValue()
				});
				break;
			}
		}

		return result;
	}
}
