using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The two clients' worlds are the same world. Each client generates its own layer — that is the game's design — so the
/// player's claim is not "one world object count exists" but "both players stand in the same world": the objects the game
/// itself built, counted by the prefab id each one carries, must match on the host and on the member. The old row read the
/// host's count and asked for `>= 1`, which a divergent member world passes (fault sheet, 2026-11-23).
/// </summary>
internal sealed class WorldCensusCase : AcceptanceCase
{
	public override string Name => "world-census";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		// Generation is not instant on either client, so the row gives each world the same brief settle and then reads
		// both: a difference at that point is a real difference, not one client still carving its terrain.
		JsonElement? host = null;
		JsonElement? member = null;
		for (var attempt = 0; attempt < 20; attempt += 1)
		{
			host = await context.RecipeAsync("world-object-census", cancellationToken).ConfigureAwait(false);
			member = await context.MemberRecipeAsync("world-object-census", cancellationToken).ConfigureAwait(false);
			var hostTotal = Counted(host);
			var memberTotal = Counted(member);
			if (hostTotal >= 1 && hostTotal == memberTotal && Groups(host) == Groups(member))
			{
				context.Read("census", host);
				context.Note($"both clients hold the same {hostTotal} building object(s)");
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host census", host);
		context.Read("member census", member);
		Require(Boolean(host, "ok") == true, "the host's world census could not be read: " + Raw(host));
		Require(Integer(host, "total") >= 1, "the host's own world holds no building object: " + Raw(host));
		throw new AssertionException(
			"the two clients are not in the same world: host " + Counted(host) + " object(s), member " + Counted(member)
			+ "; host groups " + Trim(Groups(host)) + ", member groups " + Trim(Groups(member)));
	}

	private static string Groups(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("groups", out var value))
		{
			return string.Empty;
		}

		if (value.ValueKind == JsonValueKind.String)
		{
			return value.GetString() ?? string.Empty;
		}

		if (value.ValueKind != JsonValueKind.Array)
		{
			return string.Empty;
		}

		// The census reports groups as an array of { id, count }: the earlier string-only reader returned an
		// empty string for both clients, so this half of the comparison never ran (measured 2026-11-25).
		var text = new StringBuilder();
		foreach (var group in value.EnumerateArray())
		{
			var id = group.TryGetProperty("id", out var idValue) ? idValue.GetString() : "?";
			if (IsLocalDebris(id))
			{
				continue;
			}

			if (text.Length > 0)
			{
				text.Append(';');
			}

			text.Append(id)
				.Append(':')
				.Append(group.TryGetProperty("count", out var count) && count.TryGetInt32(out var number) ? number : 0);
		}

		return text.ToString();
	}

	/// <summary>
	/// Debris the game spawns per client when a block breaks is not world identity: `glassshards` is block 7's drop
	/// (`src/CasualtiesUnknownOnline.GameAdapter/World/GameBlockDropTable.cs:48`) and CUO carries no path that recreates
	/// it on the other side, so a world where one client broke glass reads one shard apart (measured 2026-11-27,
	/// `.acceptance/freezeprefix-1`: host 2 against member 3, totals 1891 against 1892 while every other group agreed).
	/// </summary>
	private static bool IsLocalDebris(string? id) => id == "glassshards";

	private static long Counted(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("groups", out var value)
			|| value.ValueKind != JsonValueKind.Array)
		{
			return Integer(reading, "total");
		}

		var total = 0L;
		foreach (var group in value.EnumerateArray())
		{
			var id = group.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
			if (IsLocalDebris(id))
			{
				continue;
			}

			total += group.TryGetProperty("count", out var count) && count.TryGetInt32(out var number) ? number : 0;
		}

		return total;
	}

	private static string Trim(string text) => text.Length <= 300 ? text : text[..300] + "…";

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static long Integer(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return -1;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number => value.GetInt64(),
			JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
			_ => -1,
		};
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
