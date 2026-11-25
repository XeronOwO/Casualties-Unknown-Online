using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A customized item shows the same label and swatch on the OTHER client, and the customization is what the game's own
/// customizer screen produced: the row opens that screen on a carried item, picks a colour and types a label through the
/// screen's own handlers, then reads the observer's rendered copy.
///
/// Rebuilt 2026-11-23 from the game's own chain: the screen is `PlayerCamera.OpenItemCustomizer` (the function the
/// customize key runs over a bag item), the swatch buttons write `customizedItem.colorSwatch`
/// (`PlayerCamera.cs:835-845`), the label field writes `customizedItem.label` (`PlayerCamera.cs:859-866`), and the
/// escape path closes it (`PlayerCamera.cs:1004-1006`). The old shape wrote both fields itself, which proved only that
/// two fields hold what was just written into them.
/// </summary>
internal sealed class ItemCustomizationCase : AcceptanceCase
{
	private const int Swatch = 3;

	private const string Label = "Hermes";

	public override string Name => "item-customization";

	/// <summary>The item the row renamed and recoloured, still in a body.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Bag | CaseLeftovers.Hands;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The host carries the item and owns the customization; the member's client is the observer that must show the
		// same two values on its copy. The bandage is taken the way a player takes one — from the world.
		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);

		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "pickup")).ConfigureAwait(false);
		context.Read("picked up", provided);
		if (!Boolean(provided, "ok"))
		{
			// Where the bandage came from is not the claim: the row is about the customizer's own values reaching the
			// other client. A refused pickup (no free slot in the bag the run left behind) falls back to the fixture's
			// own item, and the ticket says so.
			provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
			context.Read("provided by the fixture", provided);
		}

		Require(Boolean(provided, "ok"), "the host could not take a bandage: " + Raw(provided));
		var slot = StringValue(provided, "slot");
		if (slot.Length == 0)
		{
			slot = Number(provided, "slot").ToString("0", CultureInfo.InvariantCulture);
		}
		Require(slot.Length > 0, "the picked bandage carries no slot: " + Raw(provided));

		var opened = await context.RecipeAsync("customize-open", cancellationToken, ("slot", slot)).ConfigureAwait(false);
		context.Read("customizer opened", opened);
		Require(Boolean(opened, "ok"), "the game did not open its customizer: " + Raw(opened));
		Require(Boolean(opened, "active"), "the customizer screen is not up: " + Raw(opened));

		var picked = await context.RecipeAsync("customize-pick", cancellationToken, ("swatch", Swatch.ToString(CultureInfo.InvariantCulture)), ("label", Label)).ConfigureAwait(false);
		context.Read("customized", picked);
		Require(Boolean(picked, "ok"), "the customizer did not take the colour and label: " + Raw(picked));
		Require(string.Equals(StringValue(picked, "label"), Label, StringComparison.Ordinal), "the customizer's own label field did not stick: " + Raw(picked));
		Require((int)Number(picked, "swatch") == Swatch, "the customizer's own swatch button did not stick: " + Raw(picked));

		var closed = await context.RecipeAsync("customize-close", cancellationToken).ConfigureAwait(false);
		context.Read("customizer closed", closed);
		Require(!Boolean(closed, "active"), "the customizer stayed open after the close: " + Raw(closed));

		// The member's rendered copy of the host's carried items is the reading; the clone is rebuilt on the next
		// inventory stream, so the check is polled.
		var matched = false;
		JsonElement? clone = null;
		for (var wait = 0; wait < 20 && !matched; wait++)
		{
			clone = await context.MemberRecipeAsync("clone-customization", cancellationToken).ConfigureAwait(false);
			matched = HasLabeledItem(clone, Label, Swatch);
			if (!matched)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			}
		}

		context.Read("member-clone", clone);
		Require(matched, "the member's displayed copy does not carry the host's label/swatch: " + Raw(clone));
	}

	private static bool HasLabeledItem(JsonElement? reading, string label, int swatch)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("items", out var items)
			|| items.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var item in items.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			var itemLabel = item.TryGetProperty("label", out var labelValue) && labelValue.ValueKind == JsonValueKind.String ? labelValue.GetString() : null;
			var itemSwatch = item.TryGetProperty("swatch", out var swatchValue) && swatchValue.ValueKind == JsonValueKind.Number ? swatchValue.GetInt32() : -1;
			if (string.Equals(itemLabel, label, StringComparison.Ordinal) && itemSwatch == swatch)
			{
				return true;
			}
		}

		return false;
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
			: throw new AssertionException($"the reading has no number '{property}'");

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: "";

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
