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

	/// <summary>This row breaks or places blocks, so it needs a cell the world can dig under the pair.</summary>
	public override bool NeedsDiggableGround => true;

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
		var target = await FindBreakableCellAsync(context, false, cancellationToken).ConfigureAwait(false);
		Require(target is not null, "no breakable terrain cell was found below the host at (" + cellX + ", " + cellY + ")");

		var (targetX, targetY, blockBefore) = target!.Value;
		context.Read("target", JsonDocument.Parse("{\"x\":" + targetX + ",\"y\":" + targetY + ",\"block\":" + blockBefore + "}").RootElement.Clone());

		// A player does not take a block out in one blow, and the block's damage is a fact the other client is supposed
		// to see while the block still stands — not only after it is gone. Three real blows through the game's own
		// damage entry, then both clients' own damage reading for that absolute cell must agree.
		for (var blow = 0; blow < 3; blow++)
		{
			var hit = await context.RecipeAsync("block-break-at", cancellationToken,
				("x", targetX.ToString(CultureInfo.InvariantCulture)),
				("y", targetY.ToString(CultureInfo.InvariantCulture)),
				("dmg", "25")).ConfigureAwait(false);
			context.Read("host blow " + (blow + 1).ToString(CultureInfo.InvariantCulture), hit);
			Require(Boolean(hit, "ok") == true, "the host's blow did not reach the block: " + Raw(hit));
			var hostDamage = Number(await context.RecipeAsync("block-read-at", cancellationToken,
				("x", targetX.ToString(CultureInfo.InvariantCulture)),
				("y", targetY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false), "damage");
			var agreed = false;
			for (var poll = 0; poll < 12 && !agreed; poll++)
			{
				var memberDamage = Number(await context.MemberRecipeAsync("block-read-at", cancellationToken,
					("x", targetX.ToString(CultureInfo.InvariantCulture)),
					("y", targetY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false), "damage");
				agreed = memberDamage is not null && hostDamage is not null && Math.Abs(memberDamage.Value - hostDamage.Value) < 0.01;
				if (!agreed)
				{
					await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
				}
			}

			Require(agreed, "after blow " + (blow + 1).ToString(CultureInfo.InvariantCulture) + " the member reads a different damage for the same block: host " + hostDamage!.Value.ToString("0.###", CultureInfo.InvariantCulture));
		}

		var broken = await context.RecipeAsync("block-break-at", cancellationToken,
			("x", targetX.ToString(CultureInfo.InvariantCulture)),
			("y", targetY.ToString(CultureInfo.InvariantCulture)),
			("dmg", "1000")).ConfigureAwait(false); context.Read("host break", broken);
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
		var memberTarget = await FindBreakableCellAsync(context, true, cancellationToken).ConfigureAwait(false);
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
		CaseContext context, bool member, CancellationToken cancellationToken)
	{
		// The live table is local to the client, so the 65x33 band scan is one evaluator call.
		// Reading it cell by cell used to cost 2,145 driver round-trips (46.5 s in the 1.0.2
		// full batch); the recipe returns the first breakable cell.
		var read = member
			? await context.MemberRecipeAsync("block-find-breakable", cancellationToken, ("count", "1"), ("minOffset", "0"), ("radius", "16")).ConfigureAwait(false)
			: await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "1"), ("minOffset", "0"), ("radius", "16")).ConfigureAwait(false);
		if (read is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("cells", out var cells)
			|| cells.ValueKind != JsonValueKind.Array
			|| cells.GetArrayLength() == 0)
		{
			throw new AssertionException("the block-find-breakable recipe answered no cell: " + Raw(read));
		}

		var cell = cells[0];
		var x = Number(cell, "x");
		var y = Number(cell, "y");
		var block = Number(cell, "block");
		if (x is null || y is null || block is null)
		{
			throw new AssertionException("the block-find-breakable cell is incomplete: " + Raw(read));
		}

		context.Read("scan", read);
		return ((int)x.Value, (int)y.Value, (int)block.Value);
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
