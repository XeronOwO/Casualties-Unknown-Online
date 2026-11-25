using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host-to-member block-table reconcile (<c>WorldStateMessageService.ReconcileBlockTable</c>). A
/// normal host write is broadcast live, so this case injects the one shape the live path cannot stage:
/// the host table changes and the difference table records it, but the live write is not sent (the
/// <c>block-force</c> driver action writes the table and reports it without calling
/// <c>WorldGeneration.SetBlock</c>). The production reconcile body must then carry exactly those two
/// rows to the member, whose own <c>block-read-at</c> must read both new values. The case forces the
/// due time instead of waiting the 60 s step; the interval is the reconciler's unit fact.
/// </summary>
internal sealed class BlockReconcileCase : AcceptanceCase
{
	public override string Name => "block-reconcile";

	/// <summary>This row breaks or places blocks, so it needs a cell the world can dig under the pair.</summary>
	public override bool NeedsDiggableGround => true;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(members.Length == 1, "the run does not have exactly one member on the host: " + members.Length);
		context.UseMember(members[0]);

		// A long run's host can stand in rock a previous row already broke, so a narrow
		// ring around it is not guaranteed to hold four cells (measured in the one run:
		// one cell at radius 8). The player walks to rock; the row asks a wider ring, and
		// gives chunks a moment to arrive before it calls the world empty.
		var scan = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "4"), ("minOffset", "0"), ("radius", "24")).ConfigureAwait(false);
		for (var attempt = 0; attempt < 8; attempt++)
		{
			var count = Count(scan);
			if (count >= 4)
			{
				break;
			}

			await Task.Delay(500, cancellationToken).ConfigureAwait(false);
			scan = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "4"), ("minOffset", "0"), ("radius", "24")).ConfigureAwait(false);
		}

		context.Read("scan", scan);
		JsonElement? cells = null;
		if (scan is { ValueKind: JsonValueKind.Object } scanElement
			&& scanElement.TryGetProperty("cells", out var scannedCells)
			&& scannedCells.ValueKind == JsonValueKind.Array)
		{
			cells = scannedCells;
		}

		var resolvedCells = cells.GetValueOrDefault();
		Require(resolvedCells.ValueKind == JsonValueKind.Array && resolvedCells.GetArrayLength() >= 4,
			"the host has fewer than four breakable cells: " + Raw(scan));

		var firstX = 0;
		var firstY = 0;
		var secondX = 0;
		var secondY = 0;
		var firstOld = -1;
		var secondOld = -1;
		var firstNew = -1;
		var secondNew = -1;
		var stale = false;
		for (var attempt = 0; attempt < 2 && !stale; attempt++)
		{
			var firstCell = resolvedCells[attempt * 2];
			var secondCell = resolvedCells[(attempt * 2) + 1];
			firstX = firstCell.GetProperty("x").GetInt32();
			firstY = firstCell.GetProperty("y").GetInt32();
			secondX = secondCell.GetProperty("x").GetInt32();
			secondY = secondCell.GetProperty("y").GetInt32();

			var hostFirstBefore = await ReadAsync(context, false, firstX, firstY, cancellationToken).ConfigureAwait(false);
			var hostSecondBefore = await ReadAsync(context, false, secondX, secondY, cancellationToken).ConfigureAwait(false);
			var memberFirstBefore = await ReadAsync(context, true, firstX, firstY, cancellationToken).ConfigureAwait(false);
			var memberSecondBefore = await ReadAsync(context, true, secondX, secondY, cancellationToken).ConfigureAwait(false);
			context.Read("host:first-before", hostFirstBefore);
			context.Read("host:second-before", hostSecondBefore);
			context.Read("member:first-before", memberFirstBefore);
			context.Read("member:second-before", memberSecondBefore);

			firstOld = Int(hostFirstBefore, "block");
			secondOld = Int(hostSecondBefore, "block");
			Require(firstOld >= 0 && secondOld >= 0, "the host could not read the two cells: " + Raw(hostFirstBefore) + " " + Raw(hostSecondBefore));
			Require(Int(memberFirstBefore, "block") == firstOld && Int(memberSecondBefore, "block") == secondOld,
				"the two clients do not start on the same value: host=" + firstOld + "/" + secondOld + " member=" + Raw(memberFirstBefore) + "/" + Raw(memberSecondBefore));

			firstNew = firstOld == 0 ? 1 : 0;
			secondNew = secondOld == 0 ? 2 : 0;

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

			// The reconcile body can run on its 60 s timer while this pair is staged. Wait,
			// bounded, for a pair the member has not received; when the timer beat this pair,
			// force a second pair instead of accepting a non-stale member.
			for (var wait = 0; wait < 20; wait++)
			{
				var memberFirstStale = await ReadAsync(context, true, firstX, firstY, cancellationToken).ConfigureAwait(false);
				var memberSecondStale = await ReadAsync(context, true, secondX, secondY, cancellationToken).ConfigureAwait(false);
				context.Read("member:first-stale", memberFirstStale);
				context.Read("member:second-stale", memberSecondStale);
				if (Int(memberFirstStale, "block") == firstOld && Int(memberSecondStale, "block") == secondOld)
				{
					stale = true;
					break;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			}
		}

		Require(stale, "the member was not stale before the reconcile after two forced pairs");
		var hostFirstAfter = await ReadAsync(context, false, firstX, firstY, cancellationToken).ConfigureAwait(false);
		var hostSecondAfter = await ReadAsync(context, false, secondX, secondY, cancellationToken).ConfigureAwait(false);
		context.Read("host:first-after", hostFirstAfter);
		context.Read("host:second-after", hostSecondAfter);
		Require(Int(hostFirstAfter, "block") == firstNew && Int(hostSecondAfter, "block") == secondNew,
			"the host's own table did not take the forced writes: " + Raw(hostFirstAfter) + " " + Raw(hostSecondAfter));

		// The 60 s step is the reconciler's unit fact; this action calls the production reconcile
		// body at a due timestamp, so the wire diff/send path is exercised without a production
		// interval of idle suite time. The member's own table is still the judge.
		var reconciledNow = await context.Session.ActionAsync(
			context.Session.HostUrl,
			"block-reconcile-now",
			cancellationToken).ConfigureAwait(false);
		context.Read("host:reconcile-now", reconciledNow);
		Require(Boolean(reconciledNow, "ok") == true, "the host could not run the reconcile body: " + Raw(reconciledNow));

		JsonElement? memberFirst = null;
		JsonElement? memberSecond = null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			memberFirst = await ReadAsync(context, true, firstX, firstY, cancellationToken).ConfigureAwait(false);
			memberSecond = await ReadAsync(context, true, secondX, secondY, cancellationToken).ConfigureAwait(false);
			if (Int(memberFirst, "block") == firstNew && Int(memberSecond, "block") == secondNew)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("member:first-after", memberFirst);
		context.Read("member:second-after", memberSecond);
		Require(Int(memberFirst, "block") == firstNew,
			"the member's first cell stayed at " + Int(memberFirst, "block") + " after one reconcile interval: " + Raw(memberFirst));
		Require(Int(memberSecond, "block") == secondNew,
			"the member's second cell stayed at " + Int(memberSecond, "block") + " after one reconcile interval: " + Raw(memberSecond));

		// Tie the line to THIS member: the host's log also carries reconciles for members of earlier
		// sessions, and a substring alone would accept one of those. The message carries every row
		// that changed since the previous reconcile, so the two forced rows can travel with an
		// unrelated live row (measured 2026-11-15: a healthy message carried four). The member's own
		// two reads above are the proof that the forced rows arrived; the line only proves a reconcile
		// message to this member carried at least those two rows. The network frame lands before the
		// host's log line reaches the file, so poll the file briefly instead of reading it once.
		var memberSuffix = " to " + context.MemberId;
		JsonElement? reconciled = null;
		var reconciledRows = 0;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			reconciled = await context.Session.LogTailAsync(context.Session.HostUrl, "Reconciled ", 20, cancellationToken).ConfigureAwait(false);
			reconciledRows = ReconcileRows(reconciled, memberSuffix);
			if (reconciledRows >= 2)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host:reconcile-log", reconciled);
		Require(reconciledRows >= 2,
			"the host did not log a reconcile carrying at least two rows to this member: " + Raw(reconciled));
	}

	/// <summary>The largest reconcile row count addressed to this member among the returned log lines.</summary>
	private static int ReconcileRows(JsonElement? tail, string memberSuffix)
	{
		if (tail is not { ValueKind: JsonValueKind.Object } element
			|| !int.TryParse(StringValue(element, "count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var matched)
			|| matched <= 0)
		{
			return 0;
		}

		var best = 0;
		for (var index = 0; index < Math.Min(matched, 20); index++)
		{
			var line = StringValue(element, "line" + index.ToString(CultureInfo.InvariantCulture));
			if (line is null || !line.Contains(memberSuffix, StringComparison.Ordinal))
			{
				continue;
			}

			const string marker = "Reconciled ";
			var start = line.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
			var end = start;
			while (end < line.Length && char.IsDigit(line[end]))
			{
				end++;
			}

			if (end > start && int.TryParse(line.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows))
			{
				best = Math.Max(best, rows);
			}
		}

		return best;
	}

	private static string? StringValue(JsonElement element, string property)
	{
		if (!element.TryGetProperty(property, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString(),
			JsonValueKind.Number => value.GetRawText(),
			_ => null,
		};
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

	/// <summary>How many cells a scan answered with — zero when the recipe answered without a listing.</summary>
	private static int Count(JsonElement? scan) =>
		scan is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty("cells", out var cells)
		&& cells.ValueKind == JsonValueKind.Array
			? cells.GetArrayLength()
			: 0;
}
