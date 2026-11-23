using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The crafting screen through the player's own key: pressing the game's craft bind opens the screen with the recipe
/// list the game draws (<c>PlayerCamera.RefreshRecipeList</c> fills one row per visible recipe, `PlayerCamera.cs:302`),
/// and pressing it again closes the screen. The mod's own extension renders inside this panel, so this row is the
/// surface every crafting row is read on. A row of the crafting domain.
/// </summary>
internal sealed class CraftPanelCase : AcceptanceCase
{
	public override string Name => "craft-panel";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// Our own window swallows the game's keys while it is up (`PlayerCameraHandleInputPatch`), so the row closes it
		// first — the same step every other row that drives the game's keys takes.
		await context.Session.CloseWindowAsync(context.Session.HostUrl, cancellationToken).ConfigureAwait(false);

		var panel = await PressAndReadAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("opened-by-key", panel);
		Require(Boolean(panel, "ok") == true, "the crafting list could not be read: " + Raw(panel));
		Require(Boolean(panel, "panelActive") == true, "the game did not open its craft screen when the craft key went down: " + Raw(panel));
		var rows = Integer(panel, "rows");
		Require(rows >= 1, "the craft screen opened with no recipe row the game drew: " + Raw(panel));
		var firstName = Text(panel, "firstName");
		Require(!string.IsNullOrWhiteSpace(firstName), "the first recipe row the game drew carries no name: " + Raw(panel));

		var shut = await PressAndReadAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("closed-by-key", shut);
		Require(Boolean(shut, "panelActive") == false, "the craft screen stayed open after the craft key went down again: " + Raw(shut));
	}

	/// <summary>
	/// Puts the game's own craft bind down, waits for the screen's state to change out of the state the caller saw, then
	/// releases the bind — the press-and-release a player makes. The wait is bounded and reads the game's own state.
	/// </summary>
	private static async Task<JsonElement?> PressAndReadAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var pressed = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "down")).ConfigureAwait(false);
		Require(Boolean(pressed, "ok") == true, "the craft key could not be staged: " + Raw(pressed));

		var panel = await context.RecipeAsync("craft-list", cancellationToken).ConfigureAwait(false);
		for (var attempt = 0; attempt < 20 && Boolean(panel, "ok") != true; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
			panel = await context.RecipeAsync("craft-list", cancellationToken).ConfigureAwait(false);
		}

		await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
		panel = await context.RecipeAsync("craft-list", cancellationToken).ConfigureAwait(false);

		var released = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "craft"), ("action", "up")).ConfigureAwait(false);
		Require(Boolean(released, "ok") == true, "the craft key could not be released: " + Raw(released));
		return panel;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.Number ? value.GetInt32() : -1
			: -1;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
			: string.Empty;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
