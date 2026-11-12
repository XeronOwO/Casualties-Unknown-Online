using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The crafting panel through the game's own entry (<c>PlayerCamera.OpenCraftScreen</c>): opening it
/// makes the panel active and the selected row name a real recipe; closing it leaves the panel
/// inactive. The mod's own extension (the recursive row's third state) renders inside this panel, so
/// this row is the surface every crafting row is read on. A row of the crafting domain.
/// </summary>
internal sealed class CraftPanelCase : AcceptanceCase
{
	public override string Name => "craft-panel";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// The game's own Escape closes the CUO window; the craft screen's own open throws while that
		// window is up (measured 2026-11-13), so the row closes it first and lets the game settle.
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		var opened = await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "open"), ("index", "0")).ConfigureAwait(false);
		context.Read("panel-open", opened);
		Require(Boolean(opened, "ok") == true, "the crafting panel could not be opened: " + Raw(opened));
		Require(Boolean(opened, "panelActive") == true, "the crafting panel did not become active: " + Raw(opened));
		var selected = Text(opened, "selectedName");
		Require(!string.IsNullOrWhiteSpace(selected), "the crafting panel opened with no selected recipe: " + Raw(opened));

		var closed = await context.RecipeAsync("crafting-panel", cancellationToken, ("mode", "close"), ("index", "0")).ConfigureAwait(false);
		context.Read("panel-close", closed);
		Require(Boolean(closed, "ok") == true, "the crafting panel could not be closed: " + Raw(closed));
		Require(Boolean(closed, "panelActive") == false, "the crafting panel stayed active after the close: " + Raw(closed));
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

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
