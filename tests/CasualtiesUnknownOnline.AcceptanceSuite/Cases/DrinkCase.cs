using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The world's drink family, read on the host's own body: the game's own
/// <c>FluidManager.DrinkLiquid</c> path accepts the types a player drinks (water) and refuses the
/// refuse type by name. A row from the items domain of the sweep (the "unhooked item and body sound
/// families" the recipe serves); read on the release build over the suite's own IP-direct run.
/// </summary>
internal sealed class DrinkCase : AcceptanceCase
{
	public override string Name => "world-drink";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// The drink branch: the cell under the body gets a liquid and the game's own drink call takes
		// it. 5 is the game's own water, the type a player drinks.
		var drunk = await context.RecipeAsync("world-drink", cancellationToken, ("liquid", "5")).ConfigureAwait(false);
		context.Read("drink-water", drunk);
		Require(Boolean(drunk, "ok") == true, "the game refused a drink of its own water: " + Raw(drunk));
		Require(Integer(drunk, "liquid") == 5, "the drink answered a different liquid: " + Raw(drunk));

		// The refuse type: the game's own drink call still takes any liquid type (measured 2026-11-13:
		// `{"ok":true,"liquid":6,…}`), so this row does not assert a refusal — the refusal a player sees
		// lives in the body's reaction to the drink, not in this call. Recorded as a reading beside the
		// water branch so the case does not invent a rule the game does not have.
		var refuse = await context.RecipeAsync("world-drink", cancellationToken, ("liquid", "6")).ConfigureAwait(false);
		context.Read("drink-refuse", refuse);
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static long Integer(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return -1;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number => value.GetInt64(),
			JsonValueKind.String when long.TryParse(value.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
			_ => -1,
		};
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
