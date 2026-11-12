using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A mine the host sets off carves the same cell on the member's own copy: the run's staging places a landmine
/// (and a crate beside it) next to the host, the host presses it, and both clients' own `block-read-at` at that
/// absolute cell must agree on the new block. The traps row of the world batch, read through the product's own
/// block relay.
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
		Require(target is not null, "no breakable terrain cell was found near the host, so a mine there could not carve: (" + x + ", " + y + ")");

		var (cellX, cellY, _) = target!.Value;
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
		var mineCellX = Int(mineX!.Value);
		var mineCellY = Int(mineY!.Value);

		var hostBefore = await context.RecipeAsync("block-read-at", cancellationToken, ("x", mineCellX), ("y", mineCellY)).ConfigureAwait(false);
		var memberBefore = await context.MemberRecipeAsync("block-read-at", cancellationToken, ("x", mineCellX), ("y", mineCellY)).ConfigureAwait(false);
		context.Read("host block before", hostBefore);
		context.Read("member block before", memberBefore);

		var fired = await context.RecipeAsync("trap-trigger", cancellationToken, ("x", Int(mineX.Value)), ("y", Int(mineY.Value))).ConfigureAwait(false);
		context.Read("trigger", fired);
		Require(Boolean(fired, "ok"), "the mine did not fire: " + Raw(fired));

		double? hostAfter = null;
		for (var attempt = 0; attempt < 12; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var read = await context.RecipeAsync("block-read-at", cancellationToken, ("x", mineCellX), ("y", mineCellY)).ConfigureAwait(false);
			hostAfter = Number(read, "block");
			if (hostAfter is not null && hostAfter != Number(hostBefore, "block"))
			{
				context.Read("host block after", read);
				break;
			}
		}

		Require(hostAfter is not null && hostAfter != Number(hostBefore, "block"),
			"the host's own block at the mine cell did not change: before=" + Number(hostBefore, "block") + " after=" + hostAfter);

		double? memberAfter = null;
		for (var attempt = 0; attempt < 12; attempt++)
		{
			var read = await context.MemberRecipeAsync("block-read-at", cancellationToken, ("x", mineCellX), ("y", mineCellY)).ConfigureAwait(false);
			memberAfter = Number(read, "block");
			if (memberAfter == hostAfter)
			{
				context.Read("member block after", read);
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		Require(memberAfter == hostAfter,
			"the member's own block at the blast cell did not follow the host: host=" + hostAfter + " member=" + memberAfter);
	}

	private static string Int(double value) => ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() == "true"));

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static double? NumberIn(JsonElement? reading, string outer, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(outer, out var inner) && inner.ValueKind == JsonValueKind.Object
		&& inner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
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
