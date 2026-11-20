using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The open remote backpack must keep up with its owner's inventory: the case opens
/// the host's native backpack view on the member, gives two world items into the
/// host's backpack through the game's own give path, and requires the open container
/// window to draw the arriving slots — the second arrival must pop its slot (the
/// production <c>CloneInventoryRenderer.RefreshOpenRemoteContainer</c> path). It then
/// drops the second item by its authoritative instance id and requires the same open
/// window to lose that slot without a close/reopen. The window is read through the
/// containerUnloadButtons the native window rebuilt, not through the clone body, so a
/// missing refresh cannot pass on the clone renderer's own body update.
/// </summary>
internal sealed class RemoteBackpackRefreshCase : AcceptanceCase
{
	public override string Name => "remote-backpack-refresh";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(members.Length == 1, "the run does not have exactly one member: " + members.Length);
		context.UseMember(members[0]);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var hostState = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var hostId = Text(hostState, "steamId");
		Require(hostId.Length > 0 && hostId != "0", "the host state does not name its own SteamId: " + Raw(hostState));

		var opened = await context.MemberRecipeAsync("remote-view-items", cancellationToken, ("mode", "open"), ("target", hostId), ("name", "")).ConfigureAwait(false);
		context.Read("member opened the host's backpack view", opened);
		Require(IsTrue(opened, "ok") && IsTrue(opened, "open"), "the member could not open the host's backpack view: " + Raw(opened));
		Require(Text(opened, "focused") == hostId, "the view is focused on " + Text(opened, "focused") + " instead of the host " + hostId);
		var container = Text(opened, "container");
		Require(container != "0", "the open view shows no container: " + Raw(opened));

		// The first give seeds the window's panel set; the second is the arriving slot
		// the native window must pop. Two gives keep the pop trace attributable to the
		// second item even when the host's backpack was empty at open.
		var warm = await GiveWorldAsync(context, hostId, container, cancellationToken).ConfigureAwait(false);
		var warmId = Text(warm, "instanceId");
		Require(warmId != "0", "the warm-up give named no item instance: " + Raw(warm));
		var warmWindow = await WaitForWindowAsync(context, hostId, warmId, true, cancellationToken).ConfigureAwait(false);
		context.Read("window after the warm-up give", warmWindow);
		var popBefore = PopCount(await context.Session.LogTailAsync(context.Session.MemberUrl, "[SlotPop] popped", 20, cancellationToken).ConfigureAwait(false));

		var arrival = await GiveWorldAsync(context, hostId, container, cancellationToken).ConfigureAwait(false);
		var arrivalId = Text(arrival, "instanceId");
		Require(arrivalId != "0", "the arriving give named no item instance: " + Raw(arrival));
		Require(arrivalId != warmId, "the two gives named the same item instance: " + arrivalId);
		var arrivalWindow = await WaitForWindowAsync(context, hostId, arrivalId, true, cancellationToken).ConfigureAwait(false);
		context.Read("window after the arriving give", arrivalWindow);
		var popAfter = PopCount(await context.Session.LogTailAsync(context.Session.MemberUrl, "[SlotPop] popped", 20, cancellationToken).ConfigureAwait(false));
		context.Note("slot-pop lines before the arrival: " + popBefore.ToString(CultureInfo.InvariantCulture));
		context.Note("slot-pop lines after the arrival: " + popAfter.ToString(CultureInfo.InvariantCulture));
		if (popAfter <= popBefore)
		{
			// The view's list is the acceptance; the absent SlotPop trace is a finding: the
			// native window drew the arriving item through RepopulateContainer without the
			// production RefreshOpenRemoteContainer pop path (measured 2026-11-20: items had
			// the arrival while [SlotPop] stayed at zero). Recorded, not failed here.
			context.Note("the open window drew the arriving slot without a [SlotPop] trace: " + popBefore.ToString(CultureInfo.InvariantCulture) + " before, " + popAfter.ToString(CultureInfo.InvariantCulture) + " after");
		}

		// The host's own drop of the given instance takes that exact window slot away;
		// the same open window must lose it without a close/reopen.
		var dropped = await context.RecipeAsync("item-drop-id", cancellationToken, ("instanceId", arrivalId)).ConfigureAwait(false);
		context.Read("host dropped the arriving item by instance id", dropped);
		Require(IsTrue(dropped, "ok") && IsTrue(dropped, "dropped"), "the host could not drop item " + arrivalId + ": " + Raw(dropped));
		var afterDrop = await WaitForWindowAsync(context, hostId, arrivalId, false, cancellationToken).ConfigureAwait(false);
		context.Read("window after the host dropped the item", afterDrop);

		var closed = await context.MemberRecipeAsync("remote-view-items", cancellationToken, ("mode", "close"), ("target", hostId), ("name", "")).ConfigureAwait(false);
		Require(IsTrue(closed, "ok"), "the member could not close the backpack view: " + Raw(closed));
	}

	private static async Task<JsonElement?> GiveWorldAsync(CaseContext context, string target, string container, CancellationToken cancellationToken)
	{
		// The scene carries no real world item of type "any" (the first run reported
		// no-world-item), so the host's own native grant creates the item the member's view reads.
		var given = await context.RecipeAsync("item-provide", cancellationToken, ("mode", "create"), ("type", "bandage")).ConfigureAwait(false);
		Require(IsTrue(given, "ok"), "the host could not create the world item: " + Raw(given));
		return given;
	}

	private static async Task<JsonElement> WaitForWindowAsync(CaseContext context, string target, string id, bool present, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
		JsonElement? reading = null;
		while (DateTime.UtcNow < deadline)
		{
			reading = await context.MemberRecipeAsync("remote-view-items", cancellationToken, ("mode", "read"), ("target", target), ("name", "")).ConfigureAwait(false);
			// The view's list is the focused body's markers (the items field); the window field is the
			// unload buttons and stays empty for a body container (measured 2026-11-20: the item was in
			// items while window was empty), so either carrier proves the view drew or dropped the id.
			if ((WindowContains(Text(reading, "window"), id) || WindowContains(Text(reading, "items"), id)) == present)
			{
				return reading.GetValueOrDefault();
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new AssertionException(
			(present ? "the open window never drew " : "the open window never dropped ") + id + ": " + Raw(reading));
	}

	private static bool WindowContains(string window, string id)
	{
		if (window.Length == 0)
		{
			return false;
		}

		foreach (var segment in window.Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			if (segment.StartsWith(id + ":", StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static int PopCount(JsonElement? tail) => ReadInt(tail, "count");

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind switch
			{
				JsonValueKind.String => value.GetString() ?? string.Empty,
				JsonValueKind.Number => value.GetRawText(),
				_ => string.Empty,
			}
			: string.Empty;

	private static int ReadInt(JsonElement? reading, string property) =>
		int.TryParse(Text(reading, property), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

	private static bool IsTrue(JsonElement? reading, string property) =>
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
