using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The role row: when the host ends its run, the member's own session ends with it, and a session no
/// lobby owns must leave the member with NO role — otherwise the client is a ghost that refuses every
/// later join until the process restarts (measured 2026-11-13). The row then has the host open a second
/// session on the SAME two processes and the member join it, so a kept role would be caught here.
/// </summary>
internal sealed class RoleClearingCase : AcceptanceCase
{
	public override string Name => "role-clearing";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var hostBefore = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		context.Read("host before", hostBefore);
		context.Read("member before", memberBefore);
		Require(Text(hostBefore, "role") == "Host", "the host is not the host of this session: " + Raw(hostBefore));
		Require(Text(memberBefore, "role") == "Guest", "the member is not a guest of this session: " + Raw(memberBefore));

		// The host leaves the SESSION (not only its world): the member's own session must end with it,
		// because a session that no lobby owns must leave no role behind. Leaving only the world keeps
		// the session alive and is a different row (the member waits for a host that waits for it).
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);

		// The member's session ends with the host's world; the role must go with it. The client's own
		// watchdog allows 15 s of host silence, so the budget is a little above that.
		JsonElement? memberAfter = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			memberAfter = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (Text(memberAfter, "role") == "None")
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("member after the host left", memberAfter);
		Require(Text(memberAfter, "role") == "None",
			"the member kept its role after its session ended, so it is a ghost: " + Raw(memberAfter));

		// Session two, on the same processes: the host opens it, the member joins it.
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "ip.create_host")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "start-run", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

		var hostSecond = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		context.Read("host second session", hostSecond);
		Require(Text(hostSecond, "role") == "Host", "the host did not open a second session: " + Raw(hostSecond));
		Require(Text(hostSecond, "inWorld") == "true", "the host's second run did not enter the world: " + Raw(hostSecond));

		await context.Session.ActionAsync(context.Session.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);

		JsonElement? memberSecond = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			memberSecond = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (Text(memberSecond, "role") == "Guest" && Text(memberSecond, "inWorld") == "true")
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("member second session", memberSecond);
		Require(Text(memberSecond, "role") == "Guest",
			"the member did not rejoin the host's second session without a restart: " + Raw(memberSecond));
		Require(Text(memberSecond, "inWorld") == "true",
			"the member joined the second session but never entered its world: " + Raw(memberSecond));
		context.Notes.Add("the member recreated by the fixture: " + context.Session.MemberRelaunches.ToString(CultureInfo.InvariantCulture));
	}

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
			: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
