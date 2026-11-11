using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The 1.0 sweep's enemy row: the host's scene and the member's bound copies are one set, and the
/// member's own client logs that it adopted the host's roster (its row count equals the host's).
/// </summary>
internal sealed class EnemyPairingCase : AcceptanceCase
{
	public override string Name => "enemy-pairing";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		var hostScene = await context.RecipeAsync("enemy-count", cancellationToken).ConfigureAwait(false);
		context.Read("host-scene", hostScene);
		Require(Boolean(hostScene, "ok"), "the host's scene read failed: " + Raw(hostScene));
		var hostCount = Integer(hostScene, "count");

		var memberScene = await context.MemberRecipeAsync("enemy-count", cancellationToken).ConfigureAwait(false);
		context.Read("member-scene", memberScene);
		Require(Boolean(memberScene, "ok"), "the member's scene read failed: " + Raw(memberScene));
		var memberCount = Integer(memberScene, "count");

		// The member renders the host's set: a surplus animal would be a local-only enemy the session
		// never bound, and a missing one is a copy the member lost.
		Require(memberCount == hostCount, $"the member's scene holds {memberCount} animals, the host's {hostCount}");

		var memberLog = await context.MemberLogTailAsync("[Enemy]", 20, cancellationToken).ConfigureAwait(false);
		context.Read("member-enemy-log", memberLog);
		var roster = LastLine(memberLog, "roster adopted");
		if (hostCount > 0)
		{
			Require(roster is not null, "the member never logged a roster adoption for a layer that has enemies: " + Raw(memberLog));
			var rosterLine = roster ?? "";
			Require(rosterLine.Contains("aligned=True", StringComparison.Ordinal), "the member's roster adoption did not align: " + rosterLine);
			var adopted = NumberAfter(rosterLine, "adopted: ");
			Require(adopted == hostCount, $"the member adopted {adopted} rows, the host's scene holds {hostCount}: {rosterLine}");
		}
	}

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

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.True;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String
			? found.GetString() ?? ""
			: "";

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.Number
			? found.GetInt32()
			: 0;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}

