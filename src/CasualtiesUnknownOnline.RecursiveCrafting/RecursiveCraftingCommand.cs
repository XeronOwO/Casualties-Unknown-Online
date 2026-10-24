using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The mod's first consumer: <c>/craftchain &lt;item&gt;</c> on the local console answers the ticket's question
/// for a real recipe and a real body — craftable now, reachable through steps (and which), or not craftable and
/// why. With no argument it names what the table holds, so a player can find the id to ask about. Local only:
/// nothing here crosses the wire, and the console prints the same sentence the future panel state will use
/// (<see cref="CraftChainReport"/>).
/// </summary>
internal static class RecursiveCraftingCommand
{
	internal static ConsoleAnswer Execute(IModConsoleCommandContext context)
	{
		var rows = GameCraftingTable.ReadRecipes();
		var supported = rows.Where(row => row.Recipe is not null).Select(row => row.Recipe!).ToList();

		if (context.Arguments.Count == 0 || string.IsNullOrWhiteSpace(context.Arguments[0]))
		{
			return ConsoleAnswer.Ok(DescribeTable(rows, supported));
		}

		var query = context.Arguments[0].Trim();
		var target = supported.FirstOrDefault(recipe =>
			string.Equals(recipe.Id, query, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(recipe.ResultItemId, query, StringComparison.OrdinalIgnoreCase));
		if (target is null)
		{
			var skipped = rows.FirstOrDefault(row => row.Recipe is null
				&& string.Equals(row.ResultItemId, query, StringComparison.OrdinalIgnoreCase));
			return ConsoleAnswer.Refused(skipped is not null
				? $"[Recursive] {query}: not modelled — the recipe has {skipped.SkipReason}."
				: $"[Recursive] {query}: no recipe produces that item ({supported.Count} of {rows.Count} recipes supported).");
		}

		var inventory = GameCraftingTable.ReadInventory();
		var recipes = supported.Where(recipe => !string.Equals(recipe.Id, target.Id, StringComparison.Ordinal)).ToList();
		var resolution = CraftChainResolver.Resolve(target, recipes, inventory);
		RecursiveCraftingGate.Log?.LogInformation(
			$"[Recursive] resolved {target.Id} against {inventory.Count} held item(s): {CraftChainReport.Describe(target.Id, resolution)}");
		return ConsoleAnswer.Ok("[Recursive] " + CraftChainReport.Describe(target.ResultItemId, resolution));
	}

	private static string DescribeTable(IReadOnlyList<GameRecipeRow> rows, IReadOnlyList<CraftRecipe> supported)
	{
		var skipped = rows.Count - supported.Count;
		var ids = string.Join(", ", supported.Take(24).Select(recipe => recipe.ResultItemId).Distinct(StringComparer.Ordinal));
		var suffix = supported.Count > 24 ? ", …" : "";
		return $"[Recursive] {rows.Count} recipes read ({supported.Count} supported, {skipped} not modelled): {ids}{suffix}. Usage: /craftchain <itemId>.";
	}
}
