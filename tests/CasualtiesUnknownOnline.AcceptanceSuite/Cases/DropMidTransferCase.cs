using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The mid-transfer drop row: the member gives its own carried item onto the host's displayed backpack,
/// the host applies the intent, and the host→member acknowledgement is lost while the member leaves the
/// world. The item must exist exactly once afterwards — on the host — after the member rejoins. The
/// lost acknowledgement is staged with the net-drop-message recipe on the member: the request still
/// reaches the host (the product's apply is not what this row doubts), the reply is swallowed, and the
/// member's own copy of the item is what the rejoin's character restore has to resolve.
///
/// The red run parks the request instead of the reply: no transfer reaches the host, so the same
/// assertions fail. The green run parks the reply. Test-only; no product code is changed.
/// </summary>
internal sealed class DropMidTransferCase : AcceptanceCase
{
	public override string Name => "drop-mid-transfer";

	public override CaseCost Cost => CaseCost.InRun;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var hostState = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var hostId = Text(hostState, "steamId");
		Require(hostId.Length > 0 && hostId != "0", "the host's own steam id is unknown: " + Raw(hostState));
		var memberState = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		Require(string.Equals(Text(memberState, "hostId"), hostId, StringComparison.Ordinal), "the member does not address the host the case reads: " + Raw(memberState));

		var hostBefore = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("host bag before", hostBefore);
		context.Read("member bag before", memberBefore);
		var targetSlot = FreeSlot(hostBefore);
		if (targetSlot < 0)
		{
			targetSlot = await FreeHostSlotAsync(context, hostBefore, cancellationToken).ConfigureAwait(false);
			hostBefore = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		}

		Require(targetSlot >= 0, "the host has no free body slot for the give: " + Raw(hostBefore));

		// The green row loses the acknowledgement; the red row loses the request. The request is a
		// different failure and is used only as the deliberate red break, never as the green staging.
		var red = string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Name, StringComparison.Ordinal);
		// The host applies the intent and broadcasts the item change inside the kernel envelope; the
		// member's copy of that envelope is the acknowledgement this row loses. The red run parks the
		// request on the host instead, so no transfer reaches it at all.
		var armMessage = red ? "RemoteInventoryIntentRequest" : "KernelEnvelope";
		await ArmAsync(context, armMessage, cancellationToken, host: red).ConfigureAwait(false);

		var give = await context.MemberRecipeAsync("give-probe", cancellationToken,
			("mode", "give"),
			("target", hostId),
			("slot", targetSlot.ToString(CultureInfo.InvariantCulture)),
			("container", "0"),
			("type", "any")).ConfigureAwait(false);
		context.Read("give", give);
		Require(Boolean(give, "ok"), "the member's give was refused: " + Raw(give));
		var givenType = Text(give, "item");
		Require(givenType.Length > 0, "the give names no item type: " + Raw(give));

		var hostAfter = await WaitForHostSlotAsync(context, targetSlot, givenType, cancellationToken).ConfigureAwait(false);
		context.Read("host bag after apply", hostAfter);
		Require(Text(hostAfter, "ids").Contains(targetSlot.ToString(CultureInfo.InvariantCulture) + ":" + givenType + "|", StringComparison.Ordinal)
			|| Text(hostAfter, "ids").EndsWith(targetSlot.ToString(CultureInfo.InvariantCulture) + ":" + givenType, StringComparison.Ordinal),
			"the host's applied give did not land in the free slot " + targetSlot + ": " + Raw(hostAfter));

		var status = await DropAsync(context, red, "status", armMessage, cancellationToken).ConfigureAwait(false);
		context.Read("dropped acknowledgement", status);
		Require(DoubleValue(status, "dropped") >= 1, "the " + armMessage + " was never dropped, so the row did not stage a lost acknowledgement: " + Raw(status));

		// The lost acknowledgement was the premise; the member's leave below needs a working
		// session. While the member's KernelEnvelope drop stays armed, the host's leave reply is
		// swallowed and the member's active session never clears (measured A1, 2026-11-20).
		await ArmAsync(context, armMessage, cancellationToken, host: red, off: true).ConfigureAwait(false);

		// The member leaves with the acknowledgement lost. Its own copy of the item is still local
		// until the rejoin; the host's world already carries the applied transfer.
		await context.Session.ActionAsync(context.Session.MemberUrl, "leave-world-intent", cancellationToken).ConfigureAwait(false);
		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member after rejoin: " + rejoined);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
		}

		var memberAfter = await WaitForMemberBagAsync(context, cancellationToken).ConfigureAwait(false);
		var hostFinal = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("host bag after rejoin", hostFinal);
		context.Read("member bag after rejoin", memberAfter);

		// Conservation by the item's own type: the host gained one, the member lost one, and neither
		// side shows a second copy. The slot check above already proved the host's destination.
		var beforeHostCount = TypeCount(hostBefore, givenType);
		var afterHostCount = TypeCount(hostFinal, givenType);
		var beforeMemberCount = TypeCount(memberBefore, givenType);
		var afterMemberCount = TypeCount(memberAfter, givenType);
		Require(afterHostCount == beforeHostCount + 1,
			"the host holds " + afterHostCount + " " + givenType + " after the transfer instead of " + (beforeHostCount + 1) + ": " + Raw(hostFinal));
		Require(afterMemberCount == beforeMemberCount - 1,
			"the member holds " + afterMemberCount + " " + givenType + " after rejoining instead of " + (beforeMemberCount - 1) + ": " + Raw(memberAfter));

		// The lost reply must not leave the member's own copy behind as a second copy elsewhere.
		Require(afterHostCount + afterMemberCount == beforeHostCount + beforeMemberCount,
			"the transfer changed the pair's total count of " + givenType + ": " + beforeHostCount + "+" + beforeMemberCount
			+ " became " + afterHostCount + "+" + afterMemberCount);

		await ArmAsync(context, "KernelEnvelope", cancellationToken, host: red, off: true).ConfigureAwait(false);
	}

	private static async Task<int> FreeHostSlotAsync(CaseContext context, JsonElement? before, CancellationToken cancellationToken)
	{
		var slots = IntValue(before, "slots");
		var filled = FilledSlots(before);
		foreach (var type in Types(before))
		{
			var dropped = await context.RecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "drop")).ConfigureAwait(false);
			if (!Boolean(dropped, "ok"))
			{
				continue;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var after = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			if (IntValue(after, "slots") != slots)
			{
				continue;
			}

			var nowFilled = FilledSlots(after);
			for (var slot = 0; slot < slots; slot++)
			{
				if (!nowFilled.Contains(slot))
				{
					context.Note("dropped " + type + " from the host to free a slot for the give");
					return slot;
				}
			}
		}

		return -1;
	}

	private static async Task ArmAsync(CaseContext context, string message, CancellationToken cancellationToken, bool host, bool off = false)
	{
		// `off` answers not-armed as a normal JSON result; a driver failure is a finding, not a case
		// to swallow. The call is made before every arm so a previous run's window cannot leak in.
		var cleared = await DropAsync(context, host, "off", message, cancellationToken).ConfigureAwait(false);
		context.Read("cleared " + message, cleared);
		if (off)
		{
			return;
		}

		var armed = await DropAsync(context, host, "arm", message, cancellationToken).ConfigureAwait(false);
		context.Read("armed " + message, armed);
		Require(Boolean(armed, "armed"), "the " + message + " drop was not armed: " + Raw(armed));
	}

	private static Task<JsonElement?> DropAsync(CaseContext context, bool host, string mode, string message, CancellationToken cancellationToken) =>
		host
			? context.RecipeAsync("net-drop-message", cancellationToken, ("mode", mode), ("msg", message), ("seconds", "60"))
			: context.MemberRecipeAsync("net-drop-message", cancellationToken, ("mode", mode), ("msg", message), ("seconds", "60"));

	private static async Task<JsonElement?> WaitForHostSlotAsync(CaseContext context, int slot, string type, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
		var reading = default(JsonElement?);
		while (DateTime.UtcNow < deadline)
		{
			reading = await context.RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			if (SlotCarries(reading, slot, type))
			{
				return reading;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return reading;
	}

	private static async Task<JsonElement?> WaitForMemberBagAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
		var reading = default(JsonElement?);
		while (DateTime.UtcNow < deadline)
		{
			reading = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			if (Boolean(reading, "ok"))
			{
				return reading;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return reading;
	}

	private static bool SlotCarries(JsonElement? reading, int slot, string type) =>
		Text(reading, "ids").Split('|', StringSplitOptions.RemoveEmptyEntries)
			.Any(entry => string.Equals(entry, slot.ToString(CultureInfo.InvariantCulture) + ":" + type, StringComparison.Ordinal));

	private static int FreeSlot(JsonElement? reading)
	{
		var slots = IntValue(reading, "slots");
		var filled = FilledSlots(reading);
		for (var slot = 0; slot < slots; slot++)
		{
			if (!filled.Contains(slot))
			{
				return slot;
			}
		}

		return -1;
	}

	private static HashSet<int> FilledSlots(JsonElement? reading)
	{
		var slots = new HashSet<int>();
		foreach (var entry in Text(reading, "ids").Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = entry.Split(':', 2);
			if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slot))
			{
				slots.Add(slot);
			}
		}

		return slots;
	}

	private static List<string> Types(JsonElement? reading)
	{
		var types = new List<string>();
		foreach (var entry in Text(reading, "ids").Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = entry.Split(':', 2);
			if (parts.Length == 2)
			{
				types.Add(parts[1]);
			}
		}

		return types;
	}

	private static int TypeCount(JsonElement? reading, string type) =>
		Types(reading).Count(candidate => string.Equals(candidate, type, StringComparison.Ordinal));

	private static int IntValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: 0;

	private static double DoubleValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: 0;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
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
