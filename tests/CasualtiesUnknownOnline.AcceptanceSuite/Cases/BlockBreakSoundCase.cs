using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A host's own block break plays on the member's machine: the host writes a rock block into the cell the
/// game's own damage roll addresses, the member arms its one-shot recorder for that block kind's own hit
/// clip, and the host rolls the game's damage until the block is gone. The break path plays the broken
/// block's `hitsound` on the side that computed it; this row requires the member's recorder to carry that
/// same clip, which is the audible half the 2026-09-21 report left as a residual.
///
/// The cell is written with `block-set-at` instead of found by `block-find-breakable` because a body that
/// spawns in a shaft has no breakable cell beside it (measured 2026-11-19: the nearest returned cell stood
/// 24 cells above the body). A zero-damage roll first proves which cell the roll addresses, so the row
/// cannot pass by hitting air.
/// </summary>
internal sealed class BlockBreakSoundCase : AcceptanceCase
{
	private const int RockBlock = 1;
	private const string RockClip = "rock";

	public override string Name => "block-break-sound";

	/// <summary>This row breaks or places blocks, so it needs a cell the world can dig under the pair.</summary>
	public override bool NeedsDiggableGround => true;

	public override CaseCost Cost => CaseCost.InRun;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var body = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var localBody = Object(body, "localBody");
		var bodyX = Math.Floor(Number(localBody, "positionX") ?? double.NaN);
		var bodyY = Math.Floor(Number(localBody, "positionY") ?? double.NaN);
		Require(!double.IsNaN(bodyX) && !double.IsNaN(bodyY), "the host's own position was not readable: " + Raw(body));

		// The roll addresses WorldToBlockPos(body) + (dx, dy) in BLOCK coordinates, which are not the body's
		// world position (measured 2026-11-19: a body at world y 487.6 rolls at block cell y 999). So ask the
		// roll for the cell first, stage the rock AT THAT CELL, and re-probe: a probe that then reads the rock
		// in the cell it just named is the proof that the later damage lands on the staged block.
		var offsets = new (int Dx, int Dy)[] { (0, -1), (1, 0), (-1, 0), (0, 1), (1, -1), (-1, -1) };
		(int X, int Y, int Dx, int Dy)? target = null;
		foreach (var offset in offsets)
		{
			var probe = await context.RecipeAsync("block-hit", cancellationToken,
				("dx", offset.Dx.ToString(CultureInfo.InvariantCulture)),
				("dy", offset.Dy.ToString(CultureInfo.InvariantCulture)),
				("dmg", "0")).ConfigureAwait(false);
			context.Read("probe " + offset.Dx.ToString(CultureInfo.InvariantCulture) + "," + offset.Dy.ToString(CultureInfo.InvariantCulture), probe);
			if (Boolean(probe, "ok") != true || Number(probe, "cellX") is not { } cellX || Number(probe, "cellY") is not { } cellY)
			{
				continue;
			}

			if (Number(probe, "blockBefore") != 0)
			{
				// The cell already holds a block; damaging it would read someone else's block, not the row's.
				continue;
			}

			var placed = await context.RecipeAsync("block-set-at", cancellationToken,
				("x", ((int)cellX).ToString(CultureInfo.InvariantCulture)),
				("y", ((int)cellY).ToString(CultureInfo.InvariantCulture)),
				("block", RockBlock.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("staged " + offset.Dx.ToString(CultureInfo.InvariantCulture) + "," + offset.Dy.ToString(CultureInfo.InvariantCulture), placed);
			if (Boolean(placed, "ok") != true)
			{
				continue;
			}

			var confirm = await context.RecipeAsync("block-hit", cancellationToken,
				("dx", offset.Dx.ToString(CultureInfo.InvariantCulture)),
				("dy", offset.Dy.ToString(CultureInfo.InvariantCulture)),
				("dmg", "0")).ConfigureAwait(false);
			context.Read("confirm " + offset.Dx.ToString(CultureInfo.InvariantCulture) + "," + offset.Dy.ToString(CultureInfo.InvariantCulture), confirm);
			if (Number(confirm, "cellX") == cellX && Number(confirm, "cellY") == cellY && Number(confirm, "blockBefore") == RockBlock)
			{
				target = ((int)cellX, (int)cellY, offset.Dx, offset.Dy);
				break;
			}
		}

		Require(target is not null, "no staged rock sat in the cell the host's own damage roll addresses: " + Raw(body));
		context.Notes.Add("the host's damage roll addresses the rock at ("
			+ target!.Value.X.ToString(CultureInfo.InvariantCulture) + ","
			+ target.Value.Y.ToString(CultureInfo.InvariantCulture) + ") through offset ("
			+ target.Value.Dx.ToString(CultureInfo.InvariantCulture) + ","
			+ target.Value.Dy.ToString(CultureInfo.InvariantCulture) + ")");

		var memberArmed = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "start"), ("match", RockClip)).ConfigureAwait(false);
		context.Read("member armed", memberArmed);

		var broke = false;
		for (var attempt = 0; attempt < 4 && !broke; attempt++)
		{
			var hit = await context.RecipeAsync("block-hit", cancellationToken,
				("dx", target.Value.Dx.ToString(CultureInfo.InvariantCulture)),
				("dy", target.Value.Dy.ToString(CultureInfo.InvariantCulture)),
				("dmg", "5000")).ConfigureAwait(false);
			context.Read("hit " + attempt.ToString(CultureInfo.InvariantCulture), hit);
			Require(Boolean(hit, "ok") == true, "the host's own damage roll failed: " + Raw(hit));
			broke = Number(hit, "blockAfter") == 0;
		}

		Require(broke, "the host's own damage roll never removed the staged rock");

		// The break's presentation is a reliable one-shot, not a synchronous return: poll the member's
		// recorder rather than racing one read.
		var heard = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", RockClip)).ConfigureAwait(false);
		var line = Text(heard, "line");
		var deadline = Stopwatch.StartNew();
		while (!line.Contains(RockClip + "@", StringComparison.Ordinal) && deadline.Elapsed < TimeSpan.FromSeconds(8))
		{
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			heard = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", RockClip)).ConfigureAwait(false);
			line = Text(heard, "line");
		}

		context.Read("member heard", heard);
		Require(line.Contains(RockClip + "@", StringComparison.Ordinal),
			"the member's recorder did not carry the broken block's own clip: " + Raw(heard));
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

	private static JsonElement? Object(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
			? value
			: null;

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
			: string.Empty;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
