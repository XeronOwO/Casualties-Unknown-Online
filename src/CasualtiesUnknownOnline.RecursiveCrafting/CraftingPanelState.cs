using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;
using UnityEngine;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The recipe panel's third state, computed for the recipe the native UI is currently reading. The native panel
/// already distinguishes two states by itself: an entry whose ingredients are in hand renders white, one that is
/// not renders dark grey. This adds the third — an entry that is not craftable NOW but is reachable through other
/// crafts — as a name marker plus a tooltip line naming the steps, so it never reads as either of the native two.
///
/// <para>
/// The judgement is cached per FRAME and keyed by the body's holdings fingerprint: the getters are read once per
/// row per refresh (and again per hover for the tooltip), so an uncached resolution would re-read the recipe table
/// for every row. A frame boundary or any change in what the body holds invalidates the cache.
/// </para>
/// </summary>
internal static class CraftingPanelState
{
	/// <summary>The suffix a recursively reachable entry's name carries — ASCII, matching the native "(x3)"/"(50%)" style.</summary>
	internal const string Marker = " (+)";

	/// <summary>The tooltip line a recursively reachable entry carries, before the steps.</summary>
	internal const string TooltipLead = "Reachable through other crafts: ";

	private static int _frame = -1;
	private static string _fingerprint = string.Empty;
	private static List<CraftRecipe> _supported = [];
	private static List<CraftStack> _inventory = [];
	private static readonly Dictionary<string, CraftResolution> _resolutions = [with(StringComparer.Ordinal)];
	/// <summary>
	/// The resolution to show for a RECURSIVE entry, or null when the entry must keep its native rendering (it is
	/// craftable now, unreachable, unsupported, or no body is in a world).
	/// </summary>
	internal static CraftResolution? RecursiveState(Recipe recipe)
	{
		if (recipe?.result == null || string.IsNullOrEmpty(recipe.result.id))
		{
			return null;
		}

		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null)
		{
			return null;
		}

		Refresh();
		var key = recipe.result.id;
		if (_resolutions.TryGetValue(key, out var cached))
		{
			return cached.Reachability == CraftReachability.ThroughSteps ? cached : null;
		}

		var target = _supported.FirstOrDefault(candidate =>
			string.Equals(candidate.ResultItemId, recipe.result.id, StringComparison.Ordinal));
		var resolution = target is null
			? null
			: CraftChainResolver.Resolve(target, [.. _supported.Where(candidate => !string.Equals(candidate.Id, target.Id, StringComparison.Ordinal))], _inventory);
		if (resolution is not null)
		{
			_resolutions[key] = resolution;
		}

		return resolution is not null && resolution.Reachability == CraftReachability.ThroughSteps ? resolution : null;
	}

	/// <summary>Re-read the table and the holdings when the frame or the holdings changed; clears the resolution cache when either moved.</summary>
	private static void Refresh()
	{
		var frame = Time.frameCount;
		var inventory = GameCraftingTable.ReadInventory();
		var fingerprint = BuildFingerprint(inventory);
		if (frame == _frame && string.Equals(fingerprint, _fingerprint, StringComparison.Ordinal))
		{
			return;
		}

		_frame = frame;
		_fingerprint = fingerprint;
		_inventory = inventory;
		_supported = [.. GameCraftingTable.ReadRecipes().Where(row => row.Recipe is not null).Select(row => row.Recipe!)];
		_resolutions.Clear();
	}

	private static string BuildFingerprint(IReadOnlyList<CraftStack> inventory) =>
		string.Join("|", inventory.Select(stack =>
			$"{stack.ItemId}:{stack.Amount}:{stack.Condition:F2}:{string.Join(",", stack.Qualities)}").OrderBy(entry => entry, StringComparer.Ordinal));
}
