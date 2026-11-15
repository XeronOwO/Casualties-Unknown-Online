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
		var target = await FindBreakableCellAsync(context, false, cellX, cellY, cancellationToken).ConfigureAwait(false);
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

		// The relay lands asynchronously: the member's own read is polled, not taken once.
		JsonElement? memberRead = null;
		double? memberBlock = null;
		for (var attempt = 0; attempt < 12; attempt++)
		{
			memberRead = await context.MemberRecipeAsync("block-read-at", cancellationToken,
				("x", targetX.ToString(CultureInfo.InvariantCulture)),
				("y", targetY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			memberBlock = Number(memberRead, "block");
			if (memberBlock is not null && memberBlock.Value == blockAfter!.Value)
			{
				context.Read("member read", memberRead);
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(Boolean(memberRead, "ok") == true, "the member could not read the cell: " + Raw(memberRead));
		Require(memberBlock is not null && memberBlock.Value == blockAfter!.Value,
			"the member still reads block " + memberBlock + " where the host now has " + blockAfter + ": " + Raw(memberRead));

		// The reverse direction: a break the member applies must reach the host's own table.
		var memberBody = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		context.Read("member body", memberBody);
		var memberLocalBody = memberBody is { ValueKind: JsonValueKind.Object } memberBodyElement
		&& memberBodyElement.TryGetProperty("localBody", out var memberBodyValue)
			? memberBodyValue
			: (JsonElement?)null;
		var memberX = Number(memberLocalBody, "positionX");
		var memberY = Number(memberLocalBody, "positionY");
		Require(memberX is not null && memberY is not null, "the member body has no position: " + Raw(memberBody));

		var memberCellX = (int)Math.Floor(memberX!.Value);
		var memberCellY = (int)Math.Floor(memberY!.Value) - 1;
		var memberTarget = await FindBreakableCellAsync(context, true, memberCellX, memberCellY, cancellationToken).ConfigureAwait(false);
		Require(memberTarget is not null, "no breakable terrain cell was found below the member at (" + memberCellX + ", " + memberCellY + ")");

		var (memberTargetX, memberTargetY, memberBlockBefore) = memberTarget!.Value;
		context.Read("member target", JsonDocument.Parse("{\"x\":" + memberTargetX + ",\"y\":" + memberTargetY + ",\"block\":" + memberBlockBefore + "}").RootElement.Clone());

		var memberBroken = await context.MemberRecipeAsync("block-break-at", cancellationToken,
			("x", memberTargetX.ToString(CultureInfo.InvariantCulture)),
			("y", memberTargetY.ToString(CultureInfo.InvariantCulture)),
			("dmg", "1000")).ConfigureAwait(false);
		context.Read("member break", memberBroken);
		Require(Boolean(memberBroken, "ok") == true, "the member's block break failed: " + Raw(memberBroken));
		var memberBlockAfter = Number(memberBroken, "blockAfter");
		Require(memberBlockAfter is not null && memberBlockAfter.Value != memberBlockBefore,
			"the member's own world did not change the block: " + Raw(memberBroken));

		JsonElement? hostRead = null;
		double? hostBlock = null;
		for (var attempt = 0; attempt < 12; attempt++)
		{
			hostRead = await context.RecipeAsync("block-read-at", cancellationToken,
				("x", memberTargetX.ToString(CultureInfo.InvariantCulture)),
				("y", memberTargetY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			hostBlock = Number(hostRead, "block");
			if (hostBlock is not null && hostBlock.Value == memberBlockAfter!.Value)
			{
				context.Read("host read", hostRead);
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(Boolean(hostRead, "ok") == true, "the host could not read the member's cell: " + Raw(hostRead));
		Require(hostBlock is not null && hostBlock.Value == memberBlockAfter!.Value,
			"the host still reads block " + hostBlock + " where the member now has " + memberBlockAfter + ": " + Raw(hostRead));
	}

	private static async Task<(int X, int Y, int Block)?> FindBreakableCellAsync(
		CaseContext context, bool member, int startX, int startY, CancellationToken cancellationToken)
	{
		// The life pod's own shell is block 14 with no BlockInfo row; the terrain around and below
		// the pod is ordinary soil, but a host that spawned inside the pod's cave sees air below it
		// and the shell on every side (measured 2026-11-13: 504 cells, all 0 or 14), so the band
		// reaches above the body as well and widens before it gives up.
		for (var y = startY + 24; y >= startY - 40; y--)
		{
			for (var offset = 0; offset <= 16; offset++)
			{
				foreach (var x in offset == 0 ? new[] { startX } : [startX - offset, startX + offset])
				{
					var read = member
						? await context.MemberRecipeAsync("block-read-at", cancellationToken,
							("x", x.ToString(CultureInfo.InvariantCulture)),
							("y", y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false)
						: await context.RecipeAsync("block-read-at", cancellationToken,
							("x", x.ToString(CultureInfo.InvariantCulture)),
							("y", y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
					var block = Number(read, "block");
					// 0 is air, 14 is the life pod shell whose BlockInfo row does not exist.
					if (block is not null && block.Value != 0 && block.Value != 14)
					{
						context.Read("scan " + x + "," + y, read);
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
