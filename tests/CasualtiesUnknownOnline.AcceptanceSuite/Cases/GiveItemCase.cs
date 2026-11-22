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
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);
		await context.ClearMemberBodyAsync(cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
		var emptyBag = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("member-bag-empty", emptyBag);

		// An empty bag is not the same as a slot that can take one: an earlier medical row can leave the member
		// missing a limb, and a missing limb's slot reports `canPickUp false`, so the give lands nowhere (measured
		// 2026-11-22, `.acceptance/every-12`: filled 0 while the give answered ok). Healing restores what the game
		// can restore, and the reading below refuses the row if no slot opened up.
		var slots = await context.MemberRecipeAsync("item-provide", cancellationToken, ("mode", "slots"), ("type", string.Empty)).ConfigureAwait(false);
		context.Read("member-slots", slots);
		if (!HasUsableSlot(slots))
		{
			await context.MemberRecipeAsync("native-heal", cancellationToken).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			slots = await context.MemberRecipeAsync("item-provide", cancellationToken, ("mode", "slots"), ("type", string.Empty)).ConfigureAwait(false);
			context.Read("member-slots-after-heal", slots);
			Require(HasUsableSlot(slots), "the member's body has no slot a given item could land in: " + Raw(slots));
		}
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
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		// The receiver is placed again after the drop: its own client applies a give only while it
		// stands beside the giver, and the drop's physics can move the pair apart.
		await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		// The give is a gesture the receiver's client applies; a lost delivery is retried with the
		// pair re-placed, because the drop's physics and the member's own step can separate them.
		var landed = false;
		JsonElement? give = null;
		JsonElement? memberBag = null;
		for (var attempt = 0; attempt < 3 && !landed; attempt++)
		{
			await context.MemberRecipeAsync("teleport", cancellationToken,
				("x", hostX.ToString(CultureInfo.InvariantCulture)),
				("y", hostY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

			give = await context.RecipeAsync("give-probe", cancellationToken,
				("mode", "give-world"),
				("target", context.MemberId),
				("slot", "0"),
				("container", "0"),
				("type", "bandage")).ConfigureAwait(false);
			context.Read($"give-{attempt}", give);
			Require(Boolean(give, "ok"), "the give was refused: " + Raw(give));

			for (var wait = 0; wait < 12 && !landed; wait++)
			{
				memberBag = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
				landed = HasType(memberBag, "bandage");
				if (!landed)
				{
					await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
				}
			}
		}

		context.Read("member-bag", memberBag);
		Require(landed, "the member's inventory does not list the given bandage: " + Raw(memberBag));

		// The other half of the claim: a friend whose hands and bag are full cannot take the item, and the giver keeps
		// it. Nothing here writes the receiver's bag; the row fills it the way a player's bag fills — with real items the
		// game places — and then offers one more.
		for (var fill = 0; fill < 8; fill++)
		{
			var carried = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "trashbag"), ("mode", "create")).ConfigureAwait(false);
			if (!Boolean(carried, "ok"))
			{
				break;
			}

			slots = await context.MemberRecipeAsync("item-provide", cancellationToken, ("mode", "slots"), ("type", string.Empty)).ConfigureAwait(false);
			if (!HasUsableSlot(slots))
			{
				break;
			}
		}

		context.Read("member-slots-full", slots);
		Require(!HasUsableSlot(slots), "the member's bag could not be filled, so the refusal has nothing to test: " + Raw(slots));

		var groundTwo = await context.RecipeAsync("item-drop", cancellationToken, ("type", "bandage"), ("height", "1")).ConfigureAwait(false);
		Require(Boolean(groundTwo, "ok"), "the second ground item was not created");
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
		await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

		var refused = await context.RecipeAsync("give-probe", cancellationToken,
			("mode", "give-world"),
			("target", context.MemberId),
			("slot", "0"),
			("container", "0"),
			("type", "bandage")).ConfigureAwait(false);
		context.Read("give-to-a-full-bag", refused);

		var fullBag = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("member-bag-full", fullBag);
		Require(
			!Boolean(refused, "ok") || CountType(fullBag, "bandage") <= CountType(memberBag, "bandage"),
			"the member took an item into a body with no free slot: answer " + Raw(refused) + ", bag " + Raw(fullBag));
	}

	/// <summary>
	/// Whether the member's body reports a slot that could take an item: present, pickable and empty.
	/// </summary>
	/// <remarks>
	/// The item recipe answers <c>{"ok":true,"mode":"slots","count":6,"slots":[{"canPickUp":…,"holding":…}]}</c>; a limb
	/// the run amputated leaves its slot present but unpickable, and a give into that body lands nowhere.
	/// </remarks>
	private static bool HasUsableSlot(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("slots", out var slots)
			|| slots.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var slot in slots.EnumerateArray())
		{
			if (slot.ValueKind == JsonValueKind.Object
				&& slot.TryGetProperty("canPickUp", out var pickable) && pickable.ValueKind == JsonValueKind.True
				&& slot.TryGetProperty("holding", out var holding) && holding.ValueKind == JsonValueKind.False)
			{
				return true;
			}
		}

		return false;
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

	private static int CountType(JsonElement? reading, string type)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("ids", out var ids)
			|| ids.ValueKind != JsonValueKind.String)
		{
			return 0;
		}

		var count = 0;
		foreach (var entry in (ids.GetString() ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = entry.Split(':', 2);
			if (parts.Length == 2 && string.Equals(parts[1], type, StringComparison.Ordinal))
			{
				count++;
			}
		}

		return count;
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

