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

	/// <summary>The sample item the row grants, uses and leaves in the body it was granted to.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Body | CaseLeftovers.Bag | CaseLeftovers.Hands;

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

		// The mod's action has to do something the player sees: the limb's own numbers move. Give the limb a
		// wound first — the game's own damage entry does that — then read before the use and poll after it.
		await context.RecipeAsync("wound-seed", cancellationToken, ("limb", "2"), ("force", "140"), ("infection", "0")).ConfigureAwait(false);
		var before = await context.RecipeAsync("limb-bleeding", cancellationToken, ("limb", "2")).ConfigureAwait(false);
		var (skinBefore, bleedBefore) = LimbFacts(before, 2);
		await context.RecipeAsync("select-limb", cancellationToken, ("limb", "2")).ConfigureAwait(false);
		var use = await context.RecipeAsync("limb-use", cancellationToken, ("type", "example.dressing"), ("mode", "use")).ConfigureAwait(false);
		context.Read("mod-item-used", use);
		Require(Boolean(use, "ok") && IsTrue(use, "actuallyUsable"),
			"the game refused the mod item's limb action: " + Raw(use));

		var (skinAfter, bleedAfter) = (skinBefore, bleedBefore);
		for (var attempt = 0; attempt < 8; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			var after = await context.RecipeAsync("limb-bleeding", cancellationToken, ("limb", "2")).ConfigureAwait(false);
			(skinAfter, bleedAfter) = LimbFacts(after, 2);
			if (skinAfter >= skinBefore + 0.5f || bleedAfter <= bleedBefore - 0.5f)
			{
				break;
			}
		}

		Require(skinAfter >= skinBefore + 0.5f || bleedAfter <= bleedBefore - 0.5f,
			"the mod's dressing ran but moved nothing on the limb: skin " + skinBefore + " -> " + skinAfter
				+ ", bleed " + bleedBefore + " -> " + bleedAfter);

		var items = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		var ids = Text(items, "ids");
		context.Read("body-items", items);
		Require(ids.Contains("example.", StringComparison.Ordinal),
			"the mod's items are not in the body's bag after the grants: " + Raw(items));

		context.Note("the example mod's item, its registry entry and its limb action are live inside a CUO session");
	}

	private static (float Skin, float Bleed) LimbFacts(JsonElement? reading, int index)
	{
		if (reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("limbs", out var limbs)
			&& limbs.ValueKind == JsonValueKind.Array)
		{
			foreach (var limb in limbs.EnumerateArray())
			{
				if (limb.TryGetProperty("i", out var i) && i.TryGetInt32(out var n) && n == index)
				{
					var skin = limb.TryGetProperty("skin", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetSingle() : -1f;
					var bleed = limb.TryGetProperty("bleed", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetSingle() : -1f;
					return (skin, bleed);
				}
			}
		}

		return (-1f, -1f);
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
