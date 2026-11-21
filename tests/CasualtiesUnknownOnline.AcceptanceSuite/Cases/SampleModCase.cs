using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The sample-mod row: content a mod registered reaches the game inside a CUO session. The example mod adds an
/// item (<c>example.dressing</c>) with its own limb action and a trophy item; this row grants the dressing on a
/// live body and drives the game's own limb-use entry, which is the path the mod's registered action runs on.
///
/// The chain read before the fixture: <c>tools/acceptance/recipes/item-provide.cs</c> creates a prefab by id
/// through the game's item registry; <c>limb-use.cs</c> resolves the held item and calls
/// <c>PlayerCamera.ApplyWoundItem</c>, which dispatches to the item's own limb action — for a mod item that is
/// the action the mod registered. A mod that failed to load shows up here as a missing prefab or an item the
/// game refuses to use.
/// </summary>
internal sealed class SampleModCase : AcceptanceCase
{
	public override string Name => "sample-mod";

	public override CaseCost Cost => CaseCost.InRun;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);

		// A clean body so the bag's slots are known: drop the starting kit, then grant the mod's item.
		foreach (var type in new[] { "trashbag", "dogfood", "waterbottle" })
		{
			await context.RecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "drop")).ConfigureAwait(false);
		}

		var dressing = await context.RecipeAsync("item-provide", cancellationToken, ("type", "example.dressing"), ("mode", "create")).ConfigureAwait(false);
		context.Read("mod-item-granted", dressing);
		Require(Boolean(dressing, "ok"), "the example mod's item could not be created — the mod's item registry did not reach the game: " + Raw(dressing));
		Require(IsTrue(dressing, "onBody"), "the example mod's item was created but did not land on the body: " + Raw(dressing));

		var trophy = await context.RecipeAsync("item-provide", cancellationToken, ("type", "example.trophy"), ("mode", "create")).ConfigureAwait(false);
		context.Read("mod-second-item", trophy);
		Require(Boolean(trophy, "ok"), "the mod's second item could not be created: " + Raw(trophy));

		await context.RecipeAsync("select-limb", cancellationToken, ("limb", "2")).ConfigureAwait(false);
		var use = await context.RecipeAsync("limb-use", cancellationToken, ("type", "example.dressing"), ("mode", "use")).ConfigureAwait(false);
		context.Read("mod-item-used", use);
		Require(Boolean(use, "ok") && IsTrue(use, "actuallyUsable"),
			"the game refused the mod item's limb action: " + Raw(use));

		var items = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		var ids = Text(items, "ids");
		context.Read("body-items", items);
		Require(ids.Contains("example.", StringComparison.Ordinal),
			"the mod's items are not in the body's bag after the grants: " + Raw(items));

		context.Note("the example mod's item, its registry entry and its limb action are live inside a CUO session");
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue)
		&& propertyValue.ValueKind == JsonValueKind.True;

	private static bool IsTrue(JsonElement? reading, string property) => Boolean(reading, property);

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue)
			? propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() ?? string.Empty : propertyValue.ToString()
			: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
