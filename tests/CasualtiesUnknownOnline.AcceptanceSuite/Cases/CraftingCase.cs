using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The crafting entry the game itself calls (<c>Recipes.recipes[index].TryMake()</c>, the call behind
/// <c>PlayerCamera.TryCraft</c>): with a run's own starting materials at least one recipe is craftable,
/// and making the first craftable one grows the player's product count. A row of the crafting domain.
/// </summary>
internal sealed class CraftingCase : AcceptanceCase
{
	private static readonly Regex RecipeIndex = new("\"index\"\\s*:\\s*(?<value>\\d+)", RegexOptions.CultureInvariant);

	public override string Name => "crafting";

	// Needs the character's unspent starting materials: it is registered before any case that spends
	// them, so a single run still reaches it with a craftable recipe in hand.

	public override CaseCost Cost => CaseCost.ClientRestart;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		var list = await context.RecipeAsync("craft-drive", cancellationToken, ("mode", "list"), ("index", "0")).ConfigureAwait(false);
		context.Read("craft-list", list);
		Require(Boolean(list, "ok") == true, "the craft list could not be read: " + Raw(list));
		var craftable = Integer(list, "craftable");
		Require(craftable >= 1, $"the run's own starting materials leave no craftable recipe (craftable {craftable}): " + Raw(list));

		// The recipe index the game reports for its first craftable row — the same index
		// `Recipes.recipes[index]` takes in the make call.
		var match = RecipeIndex.Match(Raw(list));
		Require(match.Success, "the craft list names no recipe index: " + Raw(list));
		var index = match.Groups["value"].Value;

		// The player opens the craft screen with the game's own key; the game refuses that open for its own reasons
		// (`PlayerCamera.cs:192-203`), so the row presses the bind and then reads the panel the game switched on.
		await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "press")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
		await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "release")).ConfigureAwait(false);

		JsonElement? panel = null;
		for (var wait = 0; wait < 40; wait++)
		{
			panel = await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "state"), ("index", index)).ConfigureAwait(false);
			if (Boolean(panel, "panelActive") == true)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("craft-panel", panel);
		Require(Boolean(panel, "panelActive") == true, "the game did not open its craft screen: " + Raw(panel));

		var made = await context.RecipeAsync("craft-drive", cancellationToken, ("mode", "make"), ("index", index)).ConfigureAwait(false);
		context.Read("craft-make", made);
		Require(Boolean(made, "ok") == true, $"the game refused the craft of recipe {index}: " + Raw(made));
		Require(Boolean(made, "crafted") == true, $"the craft of recipe {index} did not run: " + Raw(made));
		var before = Integer(made, "beforeCount");
		var after = Integer(made, "afterCount");
		Require(after > before, $"the craft of recipe {index} left the product count at {after} (before {before}): " + Raw(made));

		// Leave the screen the way the player found it: the game's own close entry, then the state read proves it shut.
		await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "close"), ("index", index)).ConfigureAwait(false);
		var shut = await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "state"), ("index", index)).ConfigureAwait(false);
		Require(Boolean(shut, "panelActive") == false, "the craft screen stayed open after the close: " + Raw(shut));
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
			JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
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
