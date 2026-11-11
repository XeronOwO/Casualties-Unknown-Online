using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The 1.0 sweep's enemy row, read the way the ticket defines it: the host's authoritative enemy table
/// holds N rows, and the member's own client logs that it adopted those N rows aligned. The scene's own
/// generation output is NOT the comparison — each client generates its layer locally by design
/// (docs/design/game/worldgen-and-layers.md: a member's scene starts before the host's generation
/// finishes), so a member's scene holds hundreds of animals the host's table never tracks.
/// </summary>
internal sealed class EnemyPairingCase : AcceptanceCase
{
	public override string Name => "enemy-pairing";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		var table = await context.RecipeAsync("enemy-table-read", cancellationToken).ConfigureAwait(false);
		context.Read("host-table", table);
		Require(Boolean(table, "ok"), "the host's enemy table read failed: " + Raw(table));
		var hostCount = Integer(table, "count");

		var memberLog = await context.MemberLogTailAsync("[Enemy]", 30, cancellationToken).ConfigureAwait(false);
		context.Read("member-enemy-log", memberLog);
		var roster = LastLine(memberLog, "roster adopted");
		Require(roster is not null, "the member never logged a roster adoption: " + Raw(memberLog));

		var rosterLine = roster ?? "";
		Require(rosterLine.Contains("aligned=True", StringComparison.Ordinal), "the member's roster adoption did not align: " + rosterLine);
		var adopted = NumberAfter(rosterLine, "adopted: ");
		Require(adopted == hostCount, $"the member adopted {adopted} rows, the host's table holds {hostCount}: {rosterLine}");
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.True;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.Number
			? found.GetInt32()
			: 0;

	private static string? LastLine(JsonElement? element, string needle)
	{
		if (element is not { ValueKind: JsonValueKind.Object } answer)
		{
			return null;
		}

		string? last = null;
		foreach (var property in answer.EnumerateObject())
		{
			if (!property.Name.StartsWith("line", StringComparison.Ordinal) || property.Value.ValueKind != JsonValueKind.String)
			{
				continue;
			}

			var line = property.Value.GetString() ?? "";
			if (line.Contains(needle, StringComparison.Ordinal))
			{
				last = line;
			}
		}

		return last;
	}

	private static int? NumberAfter(string line, string marker)
	{
		var at = line.IndexOf(marker, StringComparison.Ordinal);
		if (at < 0)
		{
			return null;
		}

		var start = at + marker.Length;
		var end = start;
		while (end < line.Length && char.IsDigit(line[end]))
		{
			end++;
		}

		return end > start && int.TryParse(line.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
