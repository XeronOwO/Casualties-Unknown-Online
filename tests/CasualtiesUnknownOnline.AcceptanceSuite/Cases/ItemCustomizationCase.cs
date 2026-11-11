using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A customized item shows the same label and swatch on the OTHER client: the carrier's own values
/// are what the displayed copy renders. A row from the items sweep (`1.0-item-customization-truth`),
/// read on the release build over the sandboxed IP-direct session.
/// </summary>
internal sealed class ItemCustomizationCase : AcceptanceCase
{
	public override string Name => "item-customization";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The host carries the item and owns the customization; the member's client is the observer
		// that must show the same two values on its copy. The case owns its room in the bag.
		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);

		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
		context.Read("provided", provided);
		Require(Boolean(provided, "ok"), "the host could not take a bandage");
		var id = FirstNonEmpty(StringValue(provided, "instanceId"), StringValue(provided, "id"));
		var slot = StringValue(provided, "slot");
		Require(id.Length > 0 && slot.Length > 0, "the provided bandage carries no id/slot: " + Raw(provided));
		var customized = await context.RecipeAsync("item-customize", cancellationToken, ("slot", slot), ("label", "Hermes"), ("swatch", "3")).ConfigureAwait(false);
		context.Read("customized", customized);
		Require(Boolean(customized, "ok"), "the host could not customize its bandage: " + Raw(customized));
		Require(string.Equals(StringValue(customized, "label"), "Hermes", StringComparison.Ordinal), "the customization did not stick on the host: " + Raw(customized));

		// The member's rendered copy of the host's carried items is the reading; the clone is
		// rebuilt on the next inventory stream, so the check is polled.
		var matched = false;
		JsonElement? clone = null;
		for (var wait = 0; wait < 20 && !matched; wait++)
		{
			clone = await context.MemberRecipeAsync("clone-customization", cancellationToken).ConfigureAwait(false);
			matched = HasLabeledItem(clone, "Hermes", 3);
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

	private static string FirstNonEmpty(string first, string second) => first.Length > 0 ? first : second;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string StringValue(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(property, out var value))
		{
			return string.Empty;
		}

		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? string.Empty,
			JsonValueKind.Number => value.GetRawText(),
			_ => string.Empty,
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

