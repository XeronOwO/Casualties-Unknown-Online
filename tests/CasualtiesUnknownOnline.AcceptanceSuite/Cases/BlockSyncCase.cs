using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A block the host breaks is gone on the member's own client at the same cell: the block row from
/// the world domain of the sweep (the block mutation reaches the guest and the re-report agrees).
/// </summary>
internal sealed class BlockSyncCase : AcceptanceCase
{
	public override string Name => "block-sync";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The host's own cell is the only one the fixture can break without guessing the ground: a
		// zero-offset hit with no damage reads where the host stands.
		var where = await context.RecipeAsync("block-hit", cancellationToken, ("dx", "0"), ("dy", "0"), ("dmg", "0")).ConfigureAwait(false);
		context.Read("host-cell", where);
		var x = Integer(where, "cellX");
		var y = Integer(where, "cellY");

		var before = await ReadBlockAsync(context, false, x, y, cancellationToken).ConfigureAwait(false);
		context.Read("host-before", before);
		var hostBlock = Text(before, "block");

		// Break it with the game's own damage path, then read both clients: the member's copy must
		// agree with the host's after the sync.
		var broken = await context.RecipeAsync("block-hit", cancellationToken, ("dx", "0"), ("dy", "0"), ("dmg", "1000")).ConfigureAwait(false);
		context.Read("host-break", broken);
		Require(Text(broken, "blockAfter") != hostBlock || Text(broken, "blockBefore") != hostBlock, "the block under the host did not change");

		var hostAfter = await WaitForBlockAsync(context, false, cancellationToken, x, y, value => value != hostBlock).ConfigureAwait(false);
		context.Read("host-after", hostAfter);
		var memberAfter = await WaitForBlockAsync(context, true, cancellationToken, x, y, value => value != hostBlock).ConfigureAwait(false);
		context.Read("member-after", memberAfter);
		Require(Text(memberAfter, "block") != hostBlock, "the member's client still shows the block the host broke");
	}

	private static async Task<JsonElement?> WaitForBlockAsync(CaseContext context, bool member, CancellationToken cancellationToken, int x, int y, Func<string, bool> predicate)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await ReadBlockAsync(context, member, x, y, cancellationToken).ConfigureAwait(false);
			if (last is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("block", out var value) && value.ValueKind == JsonValueKind.String && predicate(value.GetString() ?? ""))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static Task<JsonElement?> ReadBlockAsync(CaseContext context, bool member, int x, int y, CancellationToken cancellationToken)
	{
		var coordinates = new[]
		{
			("x", x.ToString(CultureInfo.InvariantCulture)),
			("y", y.ToString(CultureInfo.InvariantCulture)),
		};
		return member
			? context.MemberRecipeAsync("block-read-at", cancellationToken, coordinates)
			: context.RecipeAsync("block-read-at", cancellationToken, coordinates);
	}

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: throw new AssertionException($"the reading has no text '{property}': {reading?.GetRawText() ?? "null"}");

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: throw new AssertionException($"the reading has no number '{property}': {reading?.GetRawText() ?? "null"}");

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}


