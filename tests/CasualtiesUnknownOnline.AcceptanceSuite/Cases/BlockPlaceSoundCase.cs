using System.Diagnostics;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A host's own block placement plays on the member's machine: the host is given metal scrap, the member
/// arms its one-shot sound recorder, and the host places the scrap through the game's own use path (the
/// item's placement action aims at the look position, raycasts the ground and plays the scrapmetal clip).
/// The member's recorder must carry that clip — this is the row the 2026-09-06 report called silent on the
/// guest, and its audible half still had no device.
/// </summary>
internal sealed class BlockPlaceSoundCase : AcceptanceCase
{
	public override string Name => "block-place-sound";

	public override CaseCost Cost => CaseCost.InRun;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.ClearMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "scrapmetal"), ("mode", "create")).ConfigureAwait(false);
		context.Read("provided", provided);
		Require(Boolean(provided, "ok") == true, "the host could not be given metal scrap: " + Raw(provided));

		var memberArmed = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "start"), ("match", "scrapmetal")).ConfigureAwait(false);
		context.Read("member armed", memberArmed);

		// The placement action aims five metres along the look direction, raycasts the ground and places the
		// block where the ray hits. Aiming straight down reaches the floor the body stands on.
		var placed = await context.RecipeAsync("item-use", cancellationToken,
			("type", "scrapmetal"),
			("aim-x", "0"),
			("aim-y", "-1")).ConfigureAwait(false);
		context.Read("host placed", placed);
		Require(Boolean(placed, "ok") == true, "the host could not place the scrap: " + Raw(placed));

		// The placement's sound is a reliable one-shot, not a synchronous return: the host's use answer can
		// arrive before the member has replayed the clip. Poll the recorder instead of racing one read.
		var heard = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "scrapmetal")).ConfigureAwait(false);
		var line = Text(heard, "line");
		var deadline = Stopwatch.StartNew();
		while (!line.Contains("scrapmetal@", StringComparison.Ordinal) && deadline.Elapsed < TimeSpan.FromSeconds(8))
		{
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			heard = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "scrapmetal")).ConfigureAwait(false);
			line = Text(heard, "line");
		}

		context.Read("member heard", heard);
		Require(line.Contains("scrapmetal@", StringComparison.Ordinal),
			"the member's recorder did not carry the placement's sound: " + Raw(heard));
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
