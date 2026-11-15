using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host-to-member block-table reconcile (<c>WorldStateMessageService.ReconcileBlockTable</c>). A
/// normal host write is broadcast live, so this case injects the one shape the live path cannot stage:
/// the host table changes and the difference table records it, but the live write is not sent (the
/// <c>block-force</c> driver action writes the table and reports it without calling
/// <c>WorldGeneration.SetBlock</c>). The 60 s pump must then carry exactly those two rows to the member,
/// whose own <c>block-read-at</c> must read both new values. The idle/no-message half is the
/// <c>BlockTableReconcilerTests.AnUnchangedTableSendsNothing</c> fact; this case judges the live send.
/// </summary>
internal sealed class BlockReconcileCase : AcceptanceCase
{
	public override string Name => "block-reconcile";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(members.Length == 1, "the run does not have exactly one member on the host: " + members.Length);
		context.UseMember(members[0]);

		var body = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		context.Read("body", body);
		var localBody = body is { ValueKind: JsonValueKind.Object } bodyElement
		&& bodyElement.TryGetProperty("localBody", out var bodyValue)
			? bodyValue
			: (JsonElement?)null;
		var bodyX = Number(localBody, "positionX");
		var bodyY = Number(localBody, "positionY");
		Require(bodyX is not null && bodyY is not null, "the host body has no position: " + Raw(body));

		var firstX = (int)Math.Floor(bodyX!.Value);
		var firstY = (int)Math.Floor(bodyY!.Value) - 1;
		var secondX = firstX + 1;
		var secondY = firstY;

		var hostFirstBefore = await ReadAsync(context, false, firstX, firstY, cancellationToken).ConfigureAwait(false);
		var hostSecondBefore = await ReadAsync(context, false, secondX, secondY, cancellationToken).ConfigureAwait(false);
		var memberFirstBefore = await ReadAsync(context, true, firstX, firstY, cancellationToken).ConfigureAwait(false);
		var memberSecondBefore = await ReadAsync(context, true, secondX, secondY, cancellationToken).ConfigureAwait(false);
		context.Read("host:first-before", hostFirstBefore);
		context.Read("host:second-before", hostSecondBefore);
		context.Read("member:first-before", memberFirstBefore);
		context.Read("member:second-before", memberSecondBefore);

		var firstOld = Int(hostFirstBefore, "block");
		var secondOld = Int(hostSecondBefore, "block");
		Require(firstOld >= 0 && secondOld >= 0, "the host could not read the two cells: " + Raw(hostFirstBefore) + " " + Raw(hostSecondBefore));
		Require(Int(memberFirstBefore, "block") == firstOld && Int(memberSecondBefore, "block") == secondOld,
			"the two clients do not start on the same value: host=" + firstOld + "/" + secondOld + " member=" + Raw(memberFirstBefore) + "/" + Raw(memberSecondBefore));

		var firstNew = firstOld == 0 ? 1 : 0;
		var secondNew = secondOld == 0 ? 2 : 0;

		var firstForced = await context.Session.ActionAsync(
			context.Session.HostUrl,
			"block-force",
			cancellationToken,
			("-Text", firstX.ToString(CultureInfo.InvariantCulture) + "," + firstY.ToString(CultureInfo.InvariantCulture) + "," + firstNew.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		var secondForced = await context.Session.ActionAsync(
			context.Session.HostUrl,
			"block-force",
			cancellationToken,
			("-Text", secondX.ToString(CultureInfo.InvariantCulture) + "," + secondY.ToString(CultureInfo.InvariantCulture) + "," + secondNew.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("host:first-forced", firstForced);
		context.Read("host:second-forced", secondForced);
		Require(Boolean(firstForced, "ok") == true && Boolean(secondForced, "ok") == true,
			"the host could not force the two table writes: " + Raw(firstForced) + " " + Raw(secondForced));

		var hostFirstAfter = await ReadAsync(context, false, firstX, firstY, cancellationToken).ConfigureAwait(false);
		var hostSecondAfter = await ReadAsync(context, false, secondX, secondY, cancellationToken).ConfigureAwait(false);
		var memberFirstStale = await ReadAsync(context, true, firstX, firstY, cancellationToken).ConfigureAwait(false);
		var memberSecondStale = await ReadAsync(context, true, secondX, secondY, cancellationToken).ConfigureAwait(false);
		context.Read("host:first-after", hostFirstAfter);
		context.Read("host:second-after", hostSecondAfter);
		context.Read("member:first-stale", memberFirstStale);
		context.Read("member:second-stale", memberSecondStale);
		Require(Int(hostFirstAfter, "block") == firstNew && Int(hostSecondAfter, "block") == secondNew,
			"the host's own table did not take the forced writes: " + Raw(hostFirstAfter) + " " + Raw(hostSecondAfter));
		Require(Int(memberFirstStale, "block") == firstOld && Int(memberSecondStale, "block") == secondOld,
			"the member was not stale before the reconcile: " + Raw(memberFirstStale) + " " + Raw(memberSecondStale));

		// The production pump runs on the real 60 s clock; the case waits one interval and reads the
		// member's own table, not the host's answer.
		JsonElement? memberFirst = null;
		JsonElement? memberSecond = null;
		for (var attempt = 0; attempt < 140; attempt++)
		{
			memberFirst = await ReadAsync(context, true, firstX, firstY, cancellationToken).ConfigureAwait(false);
			memberSecond = await ReadAsync(context, true, secondX, secondY, cancellationToken).ConfigureAwait(false);
			if (Int(memberFirst, "block") == firstNew && Int(memberSecond, "block") == secondNew)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("member:first-after", memberFirst);
		context.Read("member:second-after", memberSecond);
		Require(Int(memberFirst, "block") == firstNew,
			"the member's first cell stayed at " + Int(memberFirst, "block") + " after one reconcile interval: " + Raw(memberFirst));
		Require(Int(memberSecond, "block") == secondNew,
			"the member's second cell stayed at " + Int(memberSecond, "block") + " after one reconcile interval: " + Raw(memberSecond));

		// Tie the line to THIS member: the host's log also carries reconciles for members of earlier
		// sessions, and a substring alone would accept one of those. The member's table is updated by
		// the network frame before the host's log line reaches the file, so poll the file briefly
		// instead of reading it once at the instant the member's read succeeds.
		var expectedLine = "Reconciled 2 block row(s) to " + context.MemberId;
		JsonElement? reconciled = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			reconciled = await context.Session.LogTailAsync(context.Session.HostUrl, expectedLine, 1, cancellationToken).ConfigureAwait(false);
			if (Raw(reconciled).Contains(expectedLine, StringComparison.Ordinal))
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host:reconcile-log", reconciled);
		Require(Raw(reconciled).Contains(expectedLine, StringComparison.Ordinal),
			"the host did not log the two-row reconcile to this member: " + Raw(reconciled));
	}

	private static async Task<JsonElement?> ReadAsync(CaseContext context, bool member, int x, int y, CancellationToken cancellationToken)
	{
		var xText = x.ToString(CultureInfo.InvariantCulture);
		var yText = y.ToString(CultureInfo.InvariantCulture);
		return member
			? await context.MemberRecipeAsync("block-read-at", cancellationToken, ("x", xText), ("y", yText)).ConfigureAwait(false)
			: await context.RecipeAsync("block-read-at", cancellationToken, ("x", xText), ("y", yText)).ConfigureAwait(false);
	}

	private static int Int(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
		&& value.TryGetInt32(out var number)
			? number
			: -1;

	private static bool? Boolean(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null,
		};
	}

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
