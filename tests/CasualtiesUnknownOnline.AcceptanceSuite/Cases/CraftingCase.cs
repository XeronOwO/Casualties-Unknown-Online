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
		// A press is DOWN for one frame only, and the game's own Update does not run every frame of a loading world
		// (measured 2026-11-23: 10 `HandleInput` entries across 28 s), so the key is held and re-pressed until the
		// game's own panel answers — the way a player keeps holding the key until the screen appears.
		JsonElement? panel = null;
		JsonElement? gates = null;
		for (var attempt = 0; attempt < 20 && Boolean(panel, "panelActive") != true; attempt++)
		{
			var pressed = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "down")).ConfigureAwait(false);
			context.Read("craft-press", pressed);
			Require(Boolean(pressed, "ok") == true, "the harness could not press the craft bind: " + Raw(pressed));
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			panel = await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "state"), ("index", index)).ConfigureAwait(false);
			if (Boolean(panel, "panelActive") == true)
			{
				break;
			}

			gates ??= await context.RecipeAsync("walk-truth", cancellationToken).ConfigureAwait(false);
		}

		var released = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "up")).ConfigureAwait(false);
		Require(Boolean(released, "ok") == true, "the harness could not release the craft bind: " + Raw(released));
		gates ??= await context.RecipeAsync("walk-truth", cancellationToken).ConfigureAwait(false);
		context.Read("craft-gates", gates);
		var modal = await context.RecipeAsync("ui-modal", cancellationToken).ConfigureAwait(false);
		context.Read("craft-modal", modal);
		Require(Boolean(panel, "panelActive") == true, "the game did not open its craft screen: " + Raw(panel) + " after " + Raw(gates) + " modal " + Raw(modal));

		var made = await context.RecipeAsync("craft-drive", cancellationToken, ("mode", "make"), ("index", index)).ConfigureAwait(false);
		context.Read("craft-make", made);
		Require(Boolean(made, "ok") == true, $"the game refused the craft of recipe {index}: " + Raw(made));
		Require(Boolean(made, "crafted") == true, $"the craft of recipe {index} did not run: " + Raw(made));
		var before = Integer(made, "beforeCount");
		var after = Integer(made, "afterCount");
		Require(after > before, $"the craft of recipe {index} left the product count at {after} (before {before}): " + Raw(made));

		// Leave the screen the way the player found it: the same key that opened it toggles it shut
		// (`PlayerCamera.cs:1027-1030` → `OpenCraftScreen` flips `craftingPanel`), and the state read proves it.
		JsonElement? shut = null;
		for (var attempt = 0; attempt < 5 && Boolean(shut, "panelActive") != false; attempt++)
		{
			var closed = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "down")).ConfigureAwait(false);
			Require(Boolean(closed, "ok") == true, "the harness could not press the craft bind to close: " + Raw(closed));
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			shut = await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "state"), ("index", index)).ConfigureAwait(false);
			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "up")).ConfigureAwait(false);
		}

		context.Read("craft-shut", shut);
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
