using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A mine the host sets off destroys the crate the staging placed beside it, and the member's own
/// scene loses the same object: the run's staging places a landmine and a crate on real soil next to
/// the host, the host presses the mine, and both clients' own object census must lose the crate's own
/// count. The blast does not carve its own block cell (measured 2026-11-13), so the witness is the
/// building, not the terrain.
/// </summary>
internal sealed class MineBlastCase : AcceptanceCase
{
	public override string Name => "mine-blast";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var body = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var x = NumberIn(body, "localBody", "positionX");
		var y = NumberIn(body, "localBody", "positionY");
		Require(x is not null && y is not null, "the host's own position is unknown: " + Raw(body));

		var target = await context.FindBreakableCellAsync((int)Math.Round(x!.Value), (int)Math.Round(y!.Value), cancellationToken).ConfigureAwait(false);
		Require(target is not null, "no breakable terrain cell was found near the host, so a mine there could not be staged: (" + x + ", " + y + ")");
		var (cellX, cellY, _) = target!.Value;

		var hostBefore = await CensusAsync(context, false, cancellationToken).ConfigureAwait(false);
		var memberBefore = await CensusAsync(context, true, cancellationToken).ConfigureAwait(false);
		context.Read("host census before", hostBefore);
		context.Read("member census before", memberBefore);

		var staged = await context.RecipeAsync("trap-stage", cancellationToken,
			("x", cellX.ToString(CultureInfo.InvariantCulture)),
			("y", cellY.ToString(CultureInfo.InvariantCulture)),
			("dx", "2"),
			("crate", "containercrate")).ConfigureAwait(false);
		context.Read("staged", staged);
		Require(Boolean(staged, "ok"), "the mine was not staged: " + Raw(staged));

		var mineX = Number(staged, "mineX");
		var mineY = Number(staged, "mineY");
		Require(mineX is not null && mineY is not null, "the staged mine names no position: " + Raw(staged));

		var fired = await context.RecipeAsync("trap-trigger", cancellationToken,
			("x", mineX!.Value.ToString("0.##", CultureInfo.InvariantCulture)),
			("y", mineY!.Value.ToString("0.##", CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("trigger", fired);
		Require(Boolean(fired, "ok"), "the mine did not fire: " + Raw(fired));

		var hostAfter = await PollCrateCountAsync(context, false, CrateCount(hostBefore) + 1, cancellationToken).ConfigureAwait(false);
		context.Read("host census after", hostAfter);
		var memberAfter = await PollCrateCountAsync(context, true, CrateCount(memberBefore) + 1, cancellationToken).ConfigureAwait(false);
		context.Read("member census after", memberAfter);

		Require(CrateCount(hostAfter) < CrateCount(hostBefore) + 1,
			"the host's own scene kept the staged crate after the blast: before=" + CrateCount(hostBefore) + " after=" + CrateCount(hostAfter));
		Require(CrateCount(memberAfter) < CrateCount(memberBefore) + 1,
			"the member's own scene kept the staged crate after the blast: before=" + CrateCount(memberBefore) + " after=" + CrateCount(memberAfter));
	}

	/// <summary>The crate's own count in a census answer, or -1 when the group is absent.</summary>
	private static double CrateCount(JsonElement? census)
	{
		if (census is not { ValueKind: JsonValueKind.Object } root || !root.TryGetProperty("groups", out var groups))
		{
			return -1;
		}

		if (groups.ValueKind == JsonValueKind.Object && groups.TryGetProperty("containercrate", out var count) && count.ValueKind == JsonValueKind.Number)
		{
			return count.GetDouble();
		}

		if (groups.ValueKind == JsonValueKind.String)
		{
			foreach (var entry in (groups.GetString() ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = entry.Split(':', 2);
				if (parts.Length == 2 && string.Equals(parts[0], "containercrate", StringComparison.Ordinal)
					&& double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
				{
					return parsed;
				}
			}
		}

		return -1;
	}

	private static async Task<JsonElement?> CensusAsync(CaseContext context, bool member, CancellationToken cancellationToken) =>
		member
			? await context.MemberRecipeAsync("world-object-census", cancellationToken).ConfigureAwait(false)
			: await context.RecipeAsync("world-object-census", cancellationToken).ConfigureAwait(false);

	/// <summary>Poll until the crate's count falls under the staged ceiling (the staging adds one crate).</summary>
	private static async Task<JsonElement?> PollCrateCountAsync(CaseContext context, bool member, double ceiling, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 16; attempt++)
		{
			last = await CensusAsync(context, member, cancellationToken).ConfigureAwait(false);
			if (CrateCount(last) < ceiling)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() == "true"));

	private static double? NumberIn(JsonElement? reading, string outer, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(outer, out var inner) && inner.ValueKind == JsonValueKind.Object
		&& inner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
