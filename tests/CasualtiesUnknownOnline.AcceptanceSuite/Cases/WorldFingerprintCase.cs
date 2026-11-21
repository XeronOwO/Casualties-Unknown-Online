using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Both clients' live block tables agree: the host and the member re-capture the product's own world
/// fingerprint (the same diagnostic the runtime logs once per world entry) and their totals match.
/// The row is the world-determinism family from the sweep — the worlds must be the same world.
/// </summary>
internal sealed class WorldFingerprintCase(string name = "world-fingerprint") : AcceptanceCase
{
	public override string Name => name;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The device writes the product's own log line on the client it runs on; the line is read
		// back from that client's own log, so the comparison is between two files, not two answers.
		// The count before the device call is what makes the read fresh: every world entry also writes
		// a fingerprint line, and taking the last line without a count would compare an old entry line
		// against the fresh one (measured 2026-11-15: the member's answer was its entry line and the
		// case reported a false divergence).
		var hostBefore = await TailCountAsync(context, false, cancellationToken).ConfigureAwait(false);
		var memberBefore = await TailCountAsync(context, true, cancellationToken).ConfigureAwait(false);

		var hostAnswer = await context.RecipeAsync("world-fingerprint", cancellationToken).ConfigureAwait(false);
		context.Read("host-fingerprint-device", hostAnswer);
		Require(Boolean(hostAnswer, "ok"), "the host's fingerprint device did not answer ok: " + Raw(hostAnswer));

		var memberAnswer = await context.MemberRecipeAsync("world-fingerprint", cancellationToken).ConfigureAwait(false);
		context.Read("member-fingerprint-device", memberAnswer);
		Require(Boolean(memberAnswer, "ok"), "the member's fingerprint device did not answer ok: " + Raw(memberAnswer));

		var hostTail = await PollTailAsync(context, false, hostBefore, cancellationToken).ConfigureAwait(false);
		context.Read("host-fingerprint-line", hostTail);
		var memberTail = await PollTailAsync(context, true, memberBefore, cancellationToken).ConfigureAwait(false);
		context.Read("member-fingerprint-line", memberTail);

		var hostLine = Line0(hostTail);
		var memberLine = Line0(memberTail);
		context.Note("host line: " + hostLine);
		context.Note("member line: " + memberLine);

		var hostTotal = Total(hostLine);
		var memberTotal = Total(memberLine);
		Require(hostTotal.Length > 0, "the host's fingerprint line carries no total: " + hostLine);
		Require(memberTotal.Length > 0, "the member's fingerprint line carries no total: " + memberLine);
		if (!string.Equals(hostTotal, memberTotal, StringComparison.OrdinalIgnoreCase))
		{
			await DiagnoseAsync(context, cancellationToken).ConfigureAwait(false);
			throw new AssertionException($"the worlds differ: host total {hostTotal}, member total {memberTotal}");
		}

		context.Note($"both clients' block tables read total {hostTotal}");
	}

	/// <summary>
	/// A failing total must name the shape, not just the hash: walk the eight 128-row bands and count how many rows
	/// differ in each, then for the first and the last differing row count the differing cells and how many of them the
	/// member reads as 00. One side empty across a band means a band still filling in; a handful of cells means a
	/// structural divergence. The readings are recorded before the assertion throws, so the ticket gets the extent even
	/// though the case fails.
	/// </summary>
	private static async Task DiagnoseAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var walked = 0;
		for (var y0 = 0; y0 < 1024; y0 += 128)
		{
			var y1 = Math.Min(1024, y0 + 128);
			var host = await context.RecipeAsync("block-band-digest", cancellationToken,
				("y0", y0.ToString()), ("y1", y1.ToString()), ("width", "1024")).ConfigureAwait(false);
			var member = await context.MemberRecipeAsync("block-band-digest", cancellationToken,
				("y0", y0.ToString()), ("y1", y1.ToString()), ("width", "1024")).ConfigureAwait(false);
			var hostRows = Rows(host, "host");
			var memberRows = Rows(member, "member");
			var differing = new List<int>();
			for (var i = 0; i < Math.Min(hostRows.Length, memberRows.Length); i++)
			{
				if (!string.Equals(hostRows[i], memberRows[i], StringComparison.Ordinal))
				{
					differing.Add(y0 + i);
				}
			}

			if (differing.Count == 0)
			{
				continue;
			}

			context.Note($"band {y0}..{y1}: {differing.Count} of {hostRows.Length} row(s) differ, first {differing[0]}, last {differing[^1]}");
			if (walked++ > 0)
			{
				continue;
			}

			foreach (var y in new[] { differing[0], differing[^1] })
			{
				var hostRow = await context.RecipeAsync("block-row-dump", cancellationToken,
					("y", y.ToString()), ("width", "1024")).ConfigureAwait(false);
				var memberRow = await context.MemberRecipeAsync("block-row-dump", cancellationToken,
					("y", y.ToString()), ("width", "1024")).ConfigureAwait(false);
				var hostValues = StringValue(hostRow, "values");
				var memberValues = StringValue(memberRow, "values");
				var cells = Math.Min(hostValues.Length, memberValues.Length) / 2;
				var differingCells = 0;
				var memberZeros = 0;
				var firstX = -1;
				for (var x = 0; x < cells; x++)
				{
					if (hostValues[x * 2] == memberValues[x * 2] && hostValues[x * 2 + 1] == memberValues[x * 2 + 1])
					{
						continue;
					}

					differingCells++;
					if (memberValues[x * 2] == '0' && memberValues[x * 2 + 1] == '0')
					{
						memberZeros++;
					}

					if (firstX < 0)
					{
						firstX = x;
						context.Note($"row {y}: first differing cell x={x}, host={hostValues.Substring(x * 2, 2)}, member={memberValues.Substring(x * 2, 2)}");
					}
				}

				context.Note($"row {y}: {differingCells} of {cells} cell(s) differ, member 00 in {memberZeros} of them, first x={firstX}");
			}
		}
	}

	private static string[] Rows(JsonElement? answer, string side)
	{
		if (answer is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("rows", out var rows)
			|| rows.ValueKind != JsonValueKind.Array)
		{
			throw new AssertionException($"the {side} band digest answered no rows: " + Raw(answer));
		}

		var values = new string[rows.GetArrayLength()];
		var index = 0;
		foreach (var row in rows.EnumerateArray())
		{
			values[index++] = row.ValueKind == JsonValueKind.String ? row.GetString() ?? string.Empty : row.GetRawText();
		}

		return values;
	}

	private static string StringValue(JsonElement? answer, string property) =>
		answer is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	/// <summary>
	/// The device's own line lands in the client's file a moment after the answer (BepInEx flushes on
	/// its own cadence), so the tail is polled until a line WITH a total appears; the last such line is
	/// the freshest capture.
	/// </summary>
	private static async Task<JsonElement?> PollTailAsync(CaseContext context, bool member, int before, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await TailAsync(context, member, cancellationToken).ConfigureAwait(false);
			if (Count(last) > before && Total(Line0(last)).Length > 0)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new AssertionException($"the {(member ? "member" : "host")} fingerprint device wrote no new line (count stayed {before}): " + Raw(last));
	}

	private static async Task<int> TailCountAsync(CaseContext context, bool member, CancellationToken cancellationToken) =>
		Count(await TailAsync(context, member, cancellationToken).ConfigureAwait(false));

	private static int Count(JsonElement? answer) =>
		answer is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty("count", out var value)
		&& value.ValueKind is JsonValueKind.String or JsonValueKind.Number
		&& int.TryParse(value.ToString(), out var count)
			? count
			: 0;

	private static async Task<JsonElement?> TailAsync(CaseContext context, bool member, CancellationToken cancellationToken) =>
		await context.Session
			.ActionAsync(member ? context.Session.MemberUrl : context.Session.HostUrl, "log-tail", cancellationToken, ("-Argument", "[WorldFingerprint]"), ("-Text", "1"))
			.ConfigureAwait(false);

	private static string Line0(JsonElement? answer) =>
		answer is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty("line0", out var line)
		&& line.ValueKind == JsonValueKind.String
			? line.GetString() ?? string.Empty
			: string.Empty;

	/// <summary>The `total &lt;hash&gt;` field of a fingerprint line, or empty.</summary>
	private static string Total(string line)
	{
		var at = line.LastIndexOf("total ", StringComparison.OrdinalIgnoreCase);
		if (at < 0)
		{
			return string.Empty;
		}

		var start = at + "total ".Length;
		var end = start;
		while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '-' || line[end] == '_'))
		{
			end++;
		}

		return end > start ? line[start..end] : string.Empty;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
