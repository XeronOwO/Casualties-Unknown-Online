using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A block mutation the host makes reaches the member: the host breaks one real terrain block below
/// its own body (a cell whose block id the game's own <c>BlockInfo</c> table names, never the life
/// pod's shell, which has no info row and makes <c>WorldGeneration.DamageBlock</c> throw), and the
/// member's own world reads the same cell changed. The row is the world-block domain's cross-client
/// half: a break the host applies is the member's block table changing too.
/// </summary>
internal sealed class BlockSyncCase : AcceptanceCase
{
	public override string Name => "block-sync";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var body = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		context.Read("body", body);
		var localBody = body is { ValueKind: JsonValueKind.Object } bodyElement
		&& bodyElement.TryGetProperty("localBody", out var bodyValue)
			? bodyValue
			: (JsonElement?)null;
		var bodyX = Number(localBody, "positionX");
		var bodyY = Number(localBody, "positionY");
		Require(bodyX is not null && bodyY is not null, "the host body has no position: " + Raw(body));

		var cellX = (int)Math.Floor(bodyX!.Value);
		var cellY = (int)Math.Floor(bodyY!.Value) - 1;
		var target = await FindBreakableCellAsync(context, cellX, cellY, cancellationToken).ConfigureAwait(false);
		Require(target is not null, "no breakable terrain cell was found below the host at (" + cellX + ", " + cellY + ")");

		var (targetX, targetY, blockBefore) = target!.Value;
		context.Read("target", JsonDocument.Parse("{\"x\":" + targetX + ",\"y\":" + targetY + ",\"block\":" + blockBefore + "}").RootElement.Clone());

		var broken = await context.RecipeAsync("block-break-at", cancellationToken,
			("x", targetX.ToString(CultureInfo.InvariantCulture)),
			("y", targetY.ToString(CultureInfo.InvariantCulture)),
			("dmg", "1000")).ConfigureAwait(false);
		context.Read("host break", broken);
		Require(Boolean(broken, "ok") == true, "the host's block break failed: " + Raw(broken));
		var blockAfter = Number(broken, "blockAfter");
		Require(blockAfter is not null && blockAfter.Value != blockBefore, "the host's own world did not change the block: " + Raw(broken));

		var memberRead = await context.MemberRecipeAsync("block-read-at", cancellationToken,
			("x", targetX.ToString(CultureInfo.InvariantCulture)),
			("y", targetY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("member read", memberRead);
		Require(Boolean(memberRead, "ok") == true, "the member could not read the cell: " + Raw(memberRead));
		var memberBlock = Number(memberRead, "block");
		Require(memberBlock is not null && memberBlock.Value == blockAfter!.Value,
			"the member still reads block " + memberBlock + " where the host now has " + blockAfter + ": " + Raw(memberRead));
	}

	private static async Task<(int X, int Y, int Block)?> FindBreakableCellAsync(
		CaseContext context, int startX, int startY, CancellationToken cancellationToken)
	{
		// The life pod's own shell is block 14 with no BlockInfo row; the terrain around and below
		// the pod is ordinary soil. Scan a narrow band under and beside the host.
		for (var depth = 0; depth < 14; depth++)
		{
			for (var offset = 0; offset <= 4; offset++)
			{
				foreach (var x in offset == 0 ? new[] { startX } : new[] { startX - offset, startX + offset })
				{
					var y = startY - depth;
					var read = await context.RecipeAsync("block-read-at", cancellationToken,
						("x", x.ToString(CultureInfo.InvariantCulture)),
						("y", y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
					context.Read("scan " + x + "," + y, read);
					var block = Number(read, "block");
					// 0 is air, 14 is the life pod shell whose BlockInfo row does not exist.
					if (block is not null && block.Value != 0 && block.Value != 14)
					{
						return (x, y, (int)block.Value);
					}
				}
			}
		}

		return null;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null,
		};
	}

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
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
