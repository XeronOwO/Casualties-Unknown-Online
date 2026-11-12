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

		// The solid cell beside the host, found with the absolute read; the air cell under the player
		// throws inside the game's own DamageBlock.
		var found = (Dx: 0, Dy: 0);
		var foundBlock = 0L;
		foreach (var offset in Offsets)
		{
			var probe = await ReadBlockAsync(context, false, bodyX + offset.Dx, bodyY + offset.Dy, cancellationToken).ConfigureAwait(false);
			var value = Integer(probe, "block");
			if (value != 0)
			{
				found = offset;
				foundBlock = value;
				break;
			}

		}

		Require(foundBlock != 0, "no solid block within two cells of the host: " + Raw(await ReadBlockAsync(context, false, bodyX, bodyY, cancellationToken).ConfigureAwait(false)));
		context.Note($"solid cell at offset ({found.Dx},{found.Dy}) with block {foundBlock}");

		var x = bodyX + found.Dx;
		var y = bodyY + found.Dy;
		var before = await ReadBlockAsync(context, false, x, y, cancellationToken).ConfigureAwait(false);
		context.Read("host-before", before);

		// Break it with the game's own damage path, then read both clients: the member's copy must
		// agree with the host's after the sync.
		// Absolute cell: the relative block-hit path throws inside the game when the body-relative
		// cell resolves unevenly; the absolute break is the same native DamageBlock entry.
		var broken = await context.RecipeAsync("block-break-at", cancellationToken,
			("x", x.ToString(CultureInfo.InvariantCulture)),
			("y", y.ToString(CultureInfo.InvariantCulture)),
			("dmg", "1000")).ConfigureAwait(false);
		context.Read("host-break", broken);
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

