using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A block the host breaks is gone on the member's own client at the same cell: the block row from
/// the world domain of the sweep (the block mutation reaches the guest and the re-report agrees).
/// The cell is FOUND first: the one under the player can be air, and the game's own DamageBlock
/// dereferences the block, so an air cell answers a NullReferenceException instead of a row.
/// </summary>
internal sealed class BlockSyncCase : AcceptanceCase
{
	private static readonly (int Dx, int Dy)[] Offsets =
	[
		(0, 0), (0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, -1), (-1, 1), (1, 1),
		(0, -2), (0, 2), (-2, 0), (2, 0),
	];

	public override string Name => "block-sync";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		var body = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		context.Read("host-body", body);
		var bodyX = (int)Math.Floor(Number(body, "positionX"));
		var bodyY = (int)Math.Floor(Number(body, "positionY"));

		// The solid cells beside the host, found with the absolute read; the air cell under the player
		// throws inside the game's own DamageBlock. A solid cell is not necessarily BREAKABLE (the life
		// pod's own shell is not), so the case tries the solid cells in turn and judges the first one the
		// game's damage path actually changes.
		var solidCells = new List<(int X, int Y, long Block)>();
		foreach (var offset in Offsets)
		{
			var probe = await ReadBlockAsync(context, false, bodyX + offset.Dx, bodyY + offset.Dy, cancellationToken).ConfigureAwait(false);
			var value = Integer(probe, "block");
			if (value != 0)
			{
				solidCells.Add((bodyX + offset.Dx, bodyY + offset.Dy, value));
			}
		}

		Require(solidCells.Count > 0, "no solid block within two cells of the host: " + Raw(await ReadBlockAsync(context, false, bodyX, bodyY, cancellationToken).ConfigureAwait(false)));
		context.Note($"solid cells near the host: {solidCells.Count}");

		var x = 0;
		var y = 0;
		long foundBlock = 0;
		JsonElement? broken = null;
		foreach (var cell in solidCells)
		{
			var before = await ReadBlockAsync(context, false, cell.X, cell.Y, cancellationToken).ConfigureAwait(false);
			context.Read("host-before", before);
			var attempt = await context.RecipeAsync("block-break-at", cancellationToken,
				("x", cell.X.ToString(CultureInfo.InvariantCulture)),
				("y", cell.Y.ToString(CultureInfo.InvariantCulture)),
				("dmg", "1000")).ConfigureAwait(false);
			context.Read("host-break", attempt);
			if (Integer(attempt, "blockAfter") != cell.Block)
			{
				x = cell.X;
				y = cell.Y;
				foundBlock = cell.Block;
				broken = attempt;
				break;
			}
		}

		Require(broken is not null, "no solid cell near the host took damage from the game's own break path");
		var after = Integer(broken, "blockAfter");
		Require(after != foundBlock, "the block did not change: " + Raw(broken));

		var hostAfter = await WaitForBlockAsync(context, false, cancellationToken, x, y, value => value != foundBlock).ConfigureAwait(false);
		context.Read("host-after", hostAfter);
		var memberAfter = await WaitForBlockAsync(context, true, cancellationToken, x, y, value => value != foundBlock).ConfigureAwait(false);
		context.Read("member-after", memberAfter);
		Require(Integer(memberAfter, "block") != foundBlock, "the member's client still shows the block the host broke: " + Raw(memberAfter));
	}

	private static async Task<JsonElement?> WaitForBlockAsync(CaseContext context, bool member, CancellationToken cancellationToken, int x, int y, Func<long, bool> predicate)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await ReadBlockAsync(context, member, x, y, cancellationToken).ConfigureAwait(false);
			if (predicate(Integer(last, "block")))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task<JsonElement?> ReadBlockAsync(CaseContext context, bool member, int x, int y, CancellationToken cancellationToken)
	{
		return member
			? await context.MemberRecipeAsync("block-read-at", cancellationToken, ("x", x.ToString(CultureInfo.InvariantCulture)), ("y", y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false)
			: await context.RecipeAsync("block-read-at", cancellationToken, ("x", x.ToString(CultureInfo.InvariantCulture)), ("y", y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String
			? found.GetString() ?? ""
			: "";

	private static long Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found)
			? found.ValueKind switch
			{
				JsonValueKind.Number => found.GetInt64(),
				JsonValueKind.String => long.TryParse(found.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
				_ => 0,
			}
			: 0;

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.Number
			? found.GetDouble()
			: 0;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}

