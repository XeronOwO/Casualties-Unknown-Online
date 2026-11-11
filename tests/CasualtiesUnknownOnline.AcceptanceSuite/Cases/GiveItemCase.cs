using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The give path a real drag performs: the host drops an item to the world, drags it onto the
/// teammate's body, and the receiving player's own client picks the world object up — so the item
/// ends in the member's inventory and leaves the world. A row from the items sweep
/// (`give-item-to-another-player`), read on the release build over the sandboxed IP-direct session.
/// </summary>
internal sealed class GiveItemCase : AcceptanceCase
{
	public override string Name => "give-item";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The receiver's own client runs the pickup, so it must stand within reach of the dropped
		// object AND have room: a full bag refuses the pickup, and the member keeps whatever earlier
		// cases left in it.
		await context.ClearMemberBodyAsync(cancellationToken).ConfigureAwait(false);
		var hostBody = await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		context.Read("host-body", hostBody);
		var (hostX, hostY) = Position(hostBody);
		Require(hostX != 0 || hostY > 0, "the host's body has no position yet: " + Raw(hostBody));
		var placed = await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Note("member placed: " + Raw(placed));
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		// A real world copy: the game's own factory creates it and its physics lands it, so the
		// custody row is `World` and the give is the gesture the drag patch makes.
		var dropped = await context.RecipeAsync("item-drop", cancellationToken, ("type", "bandage"), ("height", "1")).ConfigureAwait(false);
		context.Read("dropped", dropped);
		Require(Boolean(dropped, "ok"), "the ground item was not created");

		var give = await context.RecipeAsync("give-probe", cancellationToken,
			("mode", "give-world"),
			("target", context.MemberId),
			("slot", "0"),
			("container", "0"),
			("type", "bandage")).ConfigureAwait(false);
		context.Read("give", give);
		Require(Boolean(give, "ok"), "the give was refused: " + Raw(give));

		// The member's own inventory is the reading; the pickup runs on the receiver's client.
		var listed = false;
		JsonElement? memberBag = null;
		for (var wait = 0; wait < 40 && !listed; wait++)
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
	}

	private static (double X, double Y) Position(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("localBody", out var body)
			|| body.ValueKind != JsonValueKind.Object
			|| !body.TryGetProperty("positionX", out var x)
			|| !body.TryGetProperty("positionY", out var y)
			|| x.ValueKind != JsonValueKind.Number
			|| y.ValueKind != JsonValueKind.Number)
		{
			return (0, 0);
		}

		return (x.GetDouble(), y.GetDouble());
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

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}

