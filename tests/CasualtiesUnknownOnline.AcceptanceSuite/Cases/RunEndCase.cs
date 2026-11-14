using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The run-end row: when the host leaves the WORLD, the members still standing in it leave with it.
///
/// Measured 2026-11-14 before the fix: the host left, the member stayed in world with the departed host's
/// clone still updating under its inventory slots — nothing to play with and no message to tell it so.
/// The fix sends one run-end broadcast after the host's leave actually ran, and a member answers it through
/// its own exit path (a guest's answer is menu-only, so no cut is written on its side).
///
/// The row's evidence is both sides' own state (in world before, out of world after) plus the member's
/// own log line, so a fixture that merely watched the host's screen could never satisfy it.
/// </summary>
internal sealed class RunEndCase : AcceptanceCase
{
	public override string Name => "run-end";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var hostBefore = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		context.Read("host in world", hostBefore);
		context.Read("member in world", memberBefore);
		Require(IsWorld(hostBefore), "the host is not in a world to leave: " + Raw(hostBefore));
		Require(IsWorld(memberBefore), "the member is not in a world to leave: " + Raw(memberBefore));

		// The host performs the game's own menu return; the run-end broadcast rides the leave.
		await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);

		JsonElement? hostAfter = null;
		JsonElement? memberAfter = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			hostAfter = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			memberAfter = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (!IsWorld(hostAfter) && !IsWorld(memberAfter))
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host after the leave", hostAfter);
		context.Read("member after the leave", memberAfter);
		Require(!IsWorld(hostAfter), "the host did not leave its world: " + Raw(hostAfter));
		Require(!IsWorld(memberAfter),
			"the member is still standing in the world the host left: " + Raw(memberAfter));

		var memberLog = await context.Session.LogTailAsync(context.Session.MemberUrl, "[RunEnd]", 5, cancellationToken).ConfigureAwait(false);
		context.Read("member run-end line", memberLog);
		var sawRunEnd = (memberLog?.GetRawText() ?? string.Empty).Contains("the host's world ended", StringComparison.Ordinal);
		Require(sawRunEnd, "the member left the world without the run-end broadcast being delivered: " + (memberLog?.GetRawText() ?? "null"));
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}

	/// <summary>The driver's state carries <c>inWorld</c> as a JSON boolean.</summary>
	private static bool IsWorld(JsonElement? state) =>
		state is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("inWorld", out var value)
			&& value.ValueKind == JsonValueKind.True;

	private static string Raw(JsonElement? state) => state?.GetRawText() ?? "null";
}
