using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The enemy pairing row: the host's authoritative enemy table and the member's own scene must name the same
/// animals in the same places.
///
/// The row used to read our own adoption log alone (`roster adopted … aligned=True … adopted: N`), which an
/// empty table satisfies — 0 adopted, 0 aligned — and which says nothing about what the member's screen holds.
/// It now takes the host's table, requires it to be non-empty and bound to the host's own scene, and then looks
/// for each prefab within three units of where the host saw it on the member's own scene. Prefab and place are
/// the identity: Unity instance ids differ per process (2026-11-24: the member held 47 animals, none with the
/// host's id).
/// </summary>
internal sealed class EnemyPairingCase : AcceptanceCase
{
	private const double Reach = 3.0;

	private const int WaitMilliseconds = 12000;

	public override string Name => "enemy-pairing";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// The host's table is filled from its own scene every frame; a fresh world needs a moment.
		JsonElement? table = null;
		var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(WaitMilliseconds);
		while (DateTime.UtcNow < deadline)
		{
			table = await context.RecipeAsync("enemy-table-read", cancellationToken).ConfigureAwait(false);
			if (Boolean(table, "ok") && Integer(table, "count") >= 1)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host-table", table);
		Require(Boolean(table, "ok"), "the host's enemy table read failed: " + Raw(table));
		var hostCount = Integer(table, "count");
		Require(hostCount >= 1, "the host's own scene holds no enemy for the table to pair: " + Raw(table));

		var hostScene = await context.RecipeAsync("enemy-scene", cancellationToken).ConfigureAwait(false);
		context.Read("host-scene", hostScene);
		Require(Boolean(hostScene, "ok"), "the host's own scene read failed: " + Raw(hostScene));

		// The member's own scene, read the player's way: the animal of that prefab standing where the host saw it.
		var memberScene = await context.MemberRecipeAsync("enemy-scene", cancellationToken).ConfigureAwait(false);
		context.Read("member-scene", memberScene);
		Require(Boolean(memberScene, "ok"), "the member's own scene read failed: " + Raw(memberScene));

		var pairs = 0;
		foreach (var row in Rows(table, "enemies"))
		{
			var prefab = Text(row, "prefab");
			Require(prefab.Length > 0, "a host enemy row carries no prefab: " + row.GetRawText());
			// The row carries the place it was captured at; the host's own scene must hold the animal there,
			// and the member's own scene must hold that prefab at the same place.
			var at = (X: Double(row, "x"), Y: Double(row, "y"));
			if (SeenWithin(hostScene, prefab, at.X, at.Y, Reach) is null)
			{
				continue;
			}

			pairs++;
			Require(
				SeenWithin(memberScene, prefab, at.X, at.Y, Reach) is not null,
				"the member's own scene carries no '" + prefab + "' within " + Reach.ToString("0.#", CultureInfo.InvariantCulture) + " of where the host saw it (" + at.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + at.Y.ToString("0.##", CultureInfo.InvariantCulture) + "): " + Tail(Raw(memberScene)));
		}

		Require(pairs >= 1, "none of the host's table rows is bound to an animal the host's own scene holds: " + Raw(table));

		// The adoption line stays as a second reading: when it exists it must agree with the table.
		var memberLog = await context.MemberLogTailAsync("[Enemy]", 30, cancellationToken).ConfigureAwait(false);
		context.Read("member-enemy-log", memberLog);
		var roster = LastLine(memberLog, "roster adopted");
		if (roster is not null)
		{
			Require(roster.Contains("aligned=True", StringComparison.Ordinal), "the member's roster adoption did not align: " + roster);
			Require(NumberAfter(roster, "adopted: ") == hostCount, $"the member adopted a different row count than the host's table: {roster} vs {hostCount}");
		}

		context.Note("paired " + pairs.ToString(CultureInfo.InvariantCulture) + " host table row(s) with the member's own scene");
	}

	private static (double X, double Y)? FirstOfPrefab(JsonElement? scene, string prefab)
	{
		foreach (var animal in Rows(scene, "animals"))
		{
			if (string.Equals(Text(animal, "id"), prefab, StringComparison.Ordinal))
			{
				return (Double(animal, "x"), Double(animal, "y"));
			}
		}

		return null;
	}

	private static (double X, double Y)? SeenWithin(JsonElement? scene, string prefab, double x, double y, double tolerance)
	{
		foreach (var animal in Rows(scene, "animals"))
		{
			if (!string.Equals(Text(animal, "id"), prefab, StringComparison.Ordinal))
			{
				continue;
			}

			var ax = Double(animal, "x");
			var ay = Double(animal, "y");
			if (Math.Abs(ax - x) <= tolerance && Math.Abs(ay - y) <= tolerance)
			{
				return (ax, ay);
			}
		}

		return null;
	}

	private static IEnumerable<JsonElement> Rows(JsonElement? reading, string property)
	{
		var element = reading ?? default;
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
		{
			yield break;
		}

		foreach (var row in list.EnumerateArray())
		{
			yield return row;
		}
	}

	private static string Text(JsonElement? reading, string property)
	{
		var element = reading ?? default;
		return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() ?? "" : "";
	}

	private static double Double(JsonElement? reading, string property)
	{
		var element = reading ?? default;
		return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.Number ? found.GetDouble() : 0;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Tail(string text) => text.Length <= 400 ? text : "…" + text.Substring(text.Length - 400);

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.True;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.Number ? found.GetInt32() : 0;

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

