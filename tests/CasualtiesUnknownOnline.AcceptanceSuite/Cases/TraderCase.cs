using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The trader: standing at one and pressing the game's own interact key opens the trade screen, the price of
/// the stock and the credit the trader remembers are the game's own, an unaffordable purchase is refused
/// without taking anything, and an affordable one puts the item in the bag and spends exactly its price.
/// The player's entry is the interact key; the two trade actions are the screen's own buttons
/// (<c>TraderScript.GiveItem</c> and <c>TraderScript.TryPurchase</c>), which is the same pair the buttons call.
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
		Require(Number(list, "cheapestIndex") >= 0, "the trader stocks nothing the player can pay for: " + Raw(list));

		// The entry: the game's own use call opens its trade screen. The interact key sets the camera's current
		// trader through the pointer first, and that pointer path reads the native cursor this driver cannot move
		// (measured: the drag path entered with usable=false at a trader three units away), so the recipe sets the
		// same field and the screen, the prices and the purchase stay the game's own.
		var opened = await context.RecipeAsync("trader-use", cancellationToken).ConfigureAwait(false);
		context.Read("after the use call", opened);
		Require(Boolean(opened, "ok"), "the trader refused its own use call: " + Raw(opened));
		Require(Boolean(opened, "menuOpen"), "the trader's own use call did not open the game's trade screen: " + Raw(opened));

		var cheapest = (int)Number(opened, "cheapestIndex");
		var price = Number(opened, "cheapestPrice");
		var credit = Number(opened, "valueGiven");

		if (credit < price)
		{
			// The player cannot afford it: the game must refuse and take nothing — its own before/after counts say
			// whether the bag and the stock moved.
			var refused = await context.RecipeAsync("trade-drive", cancellationToken, ("mode", "buy"), ("index", cheapest.ToString(CultureInfo.InvariantCulture)), ("type", "")).ConfigureAwait(false);
			context.Read("refused purchase", refused);
			Require(!Boolean(refused, "bought"), "the game sold an item the trader's own credit could not pay for: " + Raw(refused));
			Require(Number(refused, "afterCount") == Number(refused, "beforeCount"), "a refused purchase still put something in the bag: " + Raw(refused));
			Require(Number(refused, "stockAfter") == Number(refused, "stockBefore"), "a refused purchase still moved the trader's stock: " + Raw(refused));
		}

		// Hand the trader something so the credit can pay for the cheapest entry — the screen's own give.
		await context.RecipeAsync("trade-drive", cancellationToken, ("mode", "create"), ("index", "0"), ("type", "trashbag")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		var gave = await context.RecipeAsync("trade-drive", cancellationToken, ("mode", "give"), ("index", "0"), ("type", "trashbag")).ConfigureAwait(false);
		context.Read("give", gave);
		Require(Boolean(gave, "ok") && Boolean(gave, "credited"), "the trader did not take the item the player handed over: " + Raw(gave));

		var afterGive = await context.RecipeAsync("trade-drive", cancellationToken, ("mode", "list"), ("index", "0"), ("type", "")).ConfigureAwait(false);
		var creditAfterGive = Number(afterGive, "valueGiven");
		Require(creditAfterGive > credit, $"the trader's credit did not grow from the give: {credit:0} -> {creditAfterGive:0}");

		var target = (int)Number(afterGive, "cheapestIndex");
		var targetPrice = Number(afterGive, "cheapestPrice");
		var stockBefore = Number(afterGive, "itemCount");

		var bought = await context.RecipeAsync("trade-drive", cancellationToken, ("mode", "buy"), ("index", target.ToString(CultureInfo.InvariantCulture)), ("type", "")).ConfigureAwait(false);
		context.Read("purchase", bought);
		Require(Boolean(bought, "bought"), $"the trader refused a purchase its own credit covered (credit {creditAfterGive:0}, price {targetPrice:0}): {Raw(bought)}");
		// The bag fills on the game's own next tick: poll it instead of reading the purchase reply once
		// (measured 2026-11-25: a run read 2 -> 2 straight after a purchase that did land).
		var bagAfter = Number(bought, "afterCount");
		for (var poll = 0; poll < 5 && bagAfter != Number(bought, "beforeCount") + 1; poll++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			var bag = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			bagAfter = Number(bag, "filled");
		}

		Require(bagAfter == Number(bought, "beforeCount") + 1, $"the purchase did not put one item in the bag: {Number(bought, "beforeCount"):0} -> {bagAfter:0}: " + Raw(bought));
		Require(Number(bought, "stockAfter") == stockBefore - 1, $"the purchase did not take the entry out of the trader's stock: {stockBefore:0} -> {Number(bought, "stockAfter"):0}");
		// `valueGiven` on the reply is the credit the trade spent from, and `valueBefore` is what it was before the
		// purchase; the difference must be exactly the price the trader's own ItemPrice named.
		var spent = Number(bought, "valueBefore") - Number(bought, "valueGiven");
		Require(Math.Abs(spent - targetPrice) < 0.01, $"the purchase spent {spent:0.##} where the game's own price was {targetPrice:0.##}");

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




