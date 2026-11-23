using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Drinking is automatic: standing in water, the body's own loop drinks every two seconds and the water raises
/// the body's thirst by the game's own amount while making it sick; standing in refuse does not quench anything.
/// The row puts the game's own liquid in the cell the body stands in and then reads the body — it never calls the
/// drink path itself, which is what the old row did.
/// </summary>
internal sealed class WorldDrinkCase : AcceptanceCase
{
	public override string Name => "world-drink";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var before = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		context.Read("before", before);
		var thirstBefore = Number(before, "thirst");
		var sicknessBefore = Number(before, "sickness");
		var temperatureBefore = Number(before, "temperature");

		// Stand in water: the game's own two-second loop does the drinking. Two ticks of slack (4.6 s) covers the
		// loop's phase without inventing a cadence of our own.
		var filled = await context.RecipeAsync("liquid-fill", cancellationToken, ("liquid", "5")).ConfigureAwait(false);
		context.Read("water", filled);
		Require(Boolean(filled, "ok"), "the water cell could not be filled: " + Raw(filled));

		JsonElement? drank = null;
		var thirst = thirstBefore;
		for (var tick = 0; tick < 10; tick++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			drank = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
			thirst = Number(drank, "thirst");
			if (thirst >= thirstBefore + 12)
			{
				break;
			}
		}

		context.Read("after standing in water", drank);
		var gained = thirst - thirstBefore;
		// The game's own water branch is `body.Drink(12f)` (`FluidManager.cs`, case 5), so one tick is +12 and
		// five seconds can hold two. Anything else means the game drank a different liquid or nothing at all.
		Require(gained >= 12, $"standing in water did not raise the body's thirst: {thirstBefore:0.##} -> {thirst:0.##}");
		Require(Math.Abs(gained - 12 * Math.Round(gained / 12)) < 0.001, $"the thirst rose by {gained:0.###}, which is not the game's own 12 per drink");
		var sicknessAfter = Number(drank, "sickness");
		// Raw water is not clean water: the same branch adds 12-16 sickness and costs a point of happiness unless
		// the body holds a filter straw, which this body does not.
		Require(sicknessAfter > sicknessBefore, $"the raw water did not make the body sick: {sicknessBefore:0.##} -> {sicknessAfter:0.##}");
		var temperatureAfter = Number(drank, "temperature");
		Require(temperatureAfter < temperatureBefore + 0.001, $"the water did not cool the body: {temperatureBefore:0.##} -> {temperatureAfter:0.##}");

		// Refuse: the game's branch for it is a scream, not a drink (`FluidManager.cs`, case 6), so thirst must not
		// move. The old row recorded this branch and asserted nothing.
		var refuse = await context.RecipeAsync("liquid-fill", cancellationToken, ("liquid", "6")).ConfigureAwait(false);
		context.Read("refuse", refuse);
		Require(Boolean(refuse, "ok"), "the refuse cell could not be filled: " + Raw(refuse));
		await Task.Delay(TimeSpan.FromMilliseconds(2600), cancellationToken).ConfigureAwait(false);
		var afterRefuse = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		context.Read("after standing in refuse", afterRefuse);
		var thirstAfterRefuse = Number(afterRefuse, "thirst");
		Require(Math.Abs(thirstAfterRefuse - thirst) < 0.001, $"standing in refuse still quenched the body: {thirst:0.##} -> {thirstAfterRefuse:0.##}");
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static double Number(JsonElement? reading, string property) => reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: throw new AssertionException($"the reading has no number '{property}': {Raw(reading)}");

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
