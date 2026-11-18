using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The crafting outcome row: a real craft consumes the material the recipe names and puts the product
/// into the crafter's bag, and the product reaches the host's view of that player.
///
/// The chain read before the fixture: <c>PlayerCamera.TryCraft</c> calls <c>Recipes.recipes[index].TryMake()</c>
/// (the class' own header), which matches the ingredients through <c>GetItemsForRecipe</c> and consumes them;
/// the produced item is a real native item with an <c>ItemInstanceId</c>, so the carried-inventory report is
/// what makes the host render it. The panel row (<c>craft-panel</c>) only opens the panel and names the recipe;
/// this row drives the craft itself through the same <c>TryMake</c> entry the craft key reaches.
///
/// The trash bag is the material because the starting-supply grant hands one out and the game's first
/// reachable recipe (<c>rippeddressing</c>) takes exactly one; if the run's startingsupplies setting did not
/// grant it, the native grant path (<c>item-provide mode=create</c>) supplies it, which is a setup
/// substitution the row states rather than assumes.
/// </summary>
internal sealed class CraftingOutcomeCase : AcceptanceCase
{
	private const string Material = "trashbag";

	private const string Product = "rippeddressing";

	public override string Name => "crafting-outcome";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var before = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("items-before", before);
		Require(Boolean(before, "ok"), "the member's body did not answer its item list: " + Raw(before));

		if (!Text(before, "ids").Contains(Material, StringComparison.Ordinal))
		{
			var provided = await context.MemberRecipeAsync("item-provide", cancellationToken,
				("type", Material), ("mode", "create")).ConfigureAwait(false);
			context.Read("material-provided", provided);
			Require(Boolean(provided, "ok"), "the material could not be put into the member's bag: " + Raw(provided));
			before = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			context.Read("items-before-retry", before);
		}

		Require(Text(before, "ids").Contains(Material, StringComparison.Ordinal),
			"the member's bag does not hold the material the recipe consumes: " + Raw(before));
		Require(!Text(before, "ids").Contains(Product, StringComparison.Ordinal),
			"the member's bag already holds the product before the craft: " + Raw(before));

		var list = await context.MemberRecipeAsync("craft-drive", cancellationToken,
			("mode", "list"), ("index", "0")).ConfigureAwait(false);
		context.Read("craftable", list);
		var index = CraftableIndex(list, Product);
		Require(index >= 0, "no craftable recipe produces " + Product + ": " + Raw(list));

		var made = await context.MemberRecipeAsync("craft-drive", cancellationToken,
			("mode", "make"), ("index", Number(index))).ConfigureAwait(false);
		context.Read("craft", made);
		Require(Boolean(made, "crafted"), "the recipe did not produce an item: " + Raw(made));
		Require(Boolean(made, "materialsAvailable"), "the recipe refused its own materials: " + Raw(made));

		var after = await WaitForMemberProductAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("items-after", after);
		Require(Text(after, "ids").Contains(Product, StringComparison.Ordinal),
			"the crafted item did not land in the member's bag: " + Raw(after));
		Require(!Text(after, "ids").Contains(Material, StringComparison.Ordinal),
			"the material the recipe consumed is still in the bag: " + Raw(after));

		// The host renders the member's bag from the carried-inventory report, which is throttled; the
		// proxy proves the craft is a fact of this player's character, not a local illusion.
		var clone = await WaitForHostProxyAsync(context, memberId, cancellationToken).ConfigureAwait(false);
		context.Read("host-view", clone);
		Require(clone is not null, "the host's view never showed the member's crafted item");
	}

	private static int CraftableIndex(JsonElement? reading, string product)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } list || !list.TryGetProperty("materials", out var materials) || materials.ValueKind != JsonValueKind.Array)
		{
			return -1;
		}

		foreach (var entry in materials.EnumerateArray())
		{
			if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String && string.Equals(result.GetString(), product, StringComparison.Ordinal) && entry.TryGetProperty("index", out var index) && index.ValueKind == JsonValueKind.Number)
			{
				return (int)index.GetDouble();
			}
		}

		return -1;
	}

	private static async Task<JsonElement?> WaitForMemberProductAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			last = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			var ids = Text(last, "ids");
			if (ids.Contains(Product, StringComparison.Ordinal) && !ids.Contains(Material, StringComparison.Ordinal))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task<JsonElement?> WaitForHostProxyAsync(CaseContext context, string memberId, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 40; attempt++)
		{
			var clone = await context.RecipeAsync("container-read", cancellationToken,
				("mode", "clone"), ("guest", memberId)).ConfigureAwait(false);
			if (ShowsProduct(clone, memberId))
			{
				return clone;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static bool ShowsProduct(JsonElement? reading, string memberId)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } clone || !clone.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var entry in items.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("owner", out var owner) || owner.ValueKind != JsonValueKind.String || !string.Equals(owner.GetString(), memberId, StringComparison.Ordinal))
			{
				continue;
			}

			if (entry.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && string.Equals(type.GetString(), Product, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var wait = 0; wait < 20; wait++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length == 1 && members[0].Length > 0)
			{
				return members[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		var final = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(false, "the host's roster did not name exactly one member: " + string.Join(", ", final));
		return string.Empty;
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

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
