using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The give path: the host releases a carried item into the member's inventory, and the member's own
/// client lists it. A row from the items sweep (`give-item-to-another-player`), read on the release
/// build over the sandboxed IP-direct session.
/// </summary>
internal sealed class GiveItemCase : AcceptanceCase
{
	public override string Name => "give-item";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);
		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
		context.Read("provided", provided);
		Require(Boolean(provided, "ok"), "the host could not take a bandage");
		var slot = StringValue(provided, "slot");
		Require(slot.Length > 0, "the provided bandage carries no slot: " + Raw(provided));

		var give = await context.RecipeAsync("give-probe", cancellationToken,
			("mode", "give"),
			("target", context.MemberId),
			("slot", slot),
			("container", "0"),
			("type", "bandage")).ConfigureAwait(false);
		context.Read("give", give);
		Require(Boolean(give, "ok"), "the give was refused: " + Raw(give));

		// The member's own inventory is the reading; the transfer crosses on the next custody stream.
		var listed = false;
		JsonElement? memberBag = null;
		for (var wait = 0; wait < 20 && !listed; wait++)
		{
			memberBag = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			listed = HasType(memberBag, "bandage");
			if (!listed)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			}
		}

		context.Read("member-bag", memberBag);
		Require(listed, "the member's inventory does not list the given bandage: " + Raw(memberBag));

		// The giver no longer carries it: the same give moves the item, it does not copy it.
		var hostBag = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("host-bag", hostBag);
		Require(!HasType(hostBag, "bandage"), "the host still carries the given bandage: " + Raw(hostBag));
	}

	private static bool HasType(JsonElement? reading, string type)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("ids", out var ids)
			|| ids.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		foreach (var entry in (ids.GetString() ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = entry.Split(':', 2);
			if (parts.Length == 2 && string.Equals(parts[1], type, StringComparison.Ordinal))
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

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
