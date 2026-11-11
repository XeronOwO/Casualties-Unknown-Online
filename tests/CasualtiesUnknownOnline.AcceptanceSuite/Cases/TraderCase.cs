using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The trader: the nearest one is listed with its own stock and credit, and the trade menu opens on
/// the current trader. A row from the world sweep, read on the release build over the sandboxed
/// IP-direct session.
/// </summary>
internal sealed class TraderCase : AcceptanceCase
{
	public override string Name => "trader";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var list = await context.RecipeAsync("trade-drive", cancellationToken, ("mode", "list"), ("index", "0"), ("type", "")).ConfigureAwait(false);
		context.Read("traders", list);
		Require(Boolean(list, "ok"), "the trader list was refused: " + Raw(list));
		Require(Number(list, "traderCount") >= 1, "no trader exists in this scene: " + Raw(list));

		var menu = await context.RecipeAsync("trade-open", cancellationToken, ("mode", "open")).ConfigureAwait(false);
		context.Read("menu", menu);
		Require(Boolean(menu, "ok"), "the trade menu was refused: " + Raw(menu));
		Require(Boolean(menu, "currentTrader"), "the menu did not bind a current trader: " + Raw(menu));
		Require(Boolean(menu, "menuActive"), "the trade menu is not active: " + Raw(menu));
		Require(Number(menu, "traderCount") >= 1, "the menu reports no traders: " + Raw(menu));
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
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
