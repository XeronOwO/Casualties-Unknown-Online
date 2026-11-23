using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The radiation line: the host's world crosses its own layer-time limit, the game activates the line, and the
/// member's own client shows the same line at the same rate.
///
/// The player cannot write a layer limit, so the fixture sets the world's own limit and layer time (declared as a
/// shortcut below). What the row judges is the part a player sees: both clients draw the line, its clock runs, its
/// height falls, and the two heights fall at the same rate — the older row compared one absolute height with
/// <c>&lt; 40</c>, which two lines starting at different moments can satisfy while descending at different rates.
/// </summary>
internal sealed class RadiationLineCase : AcceptanceCase
{
	private const int GapMilliseconds = 1200;

	public override string Name => "radiation-line";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// Fixture shortcut: the limit is a private field written at world creation, and the line cannot be switched off.
		var limit = await context.RecipeAsync("layer-limit-set", cancellationToken, ("seconds", "0.5")).ConfigureAwait(false);
		context.Read("limit", limit);
		Require(Boolean(limit, "ok"), "the layer limit could not be written: " + Raw(limit));

		var time = await context.RecipeAsync("layer-time-set", cancellationToken, ("seconds", "600")).ConfigureAwait(false);
		context.Read("layer-time", time);
		Require(Boolean(time, "ok"), "the layer time could not be written: " + Raw(time));

		// The line starts when each side sees the world past its limit; read each side until it is up.
		var hostFirst = await WaitActiveAsync(context, false, cancellationToken).ConfigureAwait(false);
		context.Read("host-line", hostFirst);
		Require(Boolean(hostFirst, "active"), "the host's line never activated: " + Raw(hostFirst));

		JsonElement? memberFirst = null;
		for (var wait = 0; wait < 20; wait++)
		{
			memberFirst = await context.MemberRecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false);
			if (Boolean(memberFirst, "active"))
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("member-line", memberFirst);
		Require(Boolean(memberFirst, "active"), "the member's own client never shows the line: " + Raw(memberFirst));

		// Two reads over one window: the clock must run and the height must fall on both sides.
		var hostBefore = await context.RecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.MemberRecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(GapMilliseconds), cancellationToken).ConfigureAwait(false);
		var hostAfter = await context.RecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false);
		var memberAfter = await context.MemberRecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false);

		context.Read("host-before", hostBefore);
		context.Read("host-after", hostAfter);
		context.Read("member-before", memberBefore);
		context.Read("member-after", memberAfter);

		var hostClock = Number(hostAfter, "timeGone") - Number(hostBefore, "timeGone");
		var memberClock = Number(memberAfter, "timeGone") - Number(memberBefore, "timeGone");
		Require(hostClock > 0, $"the host's line clock did not run in {GapMilliseconds} ms: {Raw(hostBefore)} → {Raw(hostAfter)}");
		Require(memberClock > 0, $"the member's line clock did not run in {GapMilliseconds} ms: {Raw(memberBefore)} → {Raw(memberAfter)}");

		var hostFall = Number(hostBefore, "lineY") - Number(hostAfter, "lineY");
		var memberFall = Number(memberBefore, "lineY") - Number(memberAfter, "lineY");
		Require(hostFall > 0, $"the host's line did not come down in {GapMilliseconds} ms: {Raw(hostBefore)} → {Raw(hostAfter)}");
		Require(memberFall > 0, $"the member's line did not come down in {GapMilliseconds} ms: {Raw(memberBefore)} → {Raw(memberAfter)}");

		// The same line: the two falls are the same within a quarter of the larger one.
		var larger = Math.Max(hostFall, memberFall);
		var apart = Math.Abs(hostFall - memberFall);
		Require(
			apart <= Math.Max(2.0, larger * 0.25),
			"the two views' lines fall at different rates over " + GapMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms: host " + hostFall.ToString("0.##", CultureInfo.InvariantCulture) + ", member " + memberFall.ToString("0.##", CultureInfo.InvariantCulture));

		context.Note("host fell " + hostFall.ToString("0.##", CultureInfo.InvariantCulture) + " and member " + memberFall.ToString("0.##", CultureInfo.InvariantCulture) + " over " + GapMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms");

		// The limit is restored so a later case is not surprised by a world that already crossed it.
		await context.RecipeAsync("layer-limit-set", cancellationToken, ("seconds", "10800")).ConfigureAwait(false);
	}

	private static async Task<JsonElement?> WaitActiveAsync(CaseContext context, bool member, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = member
				? await context.MemberRecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false)
				: await context.RecipeAsync("radiation-line-state", cancellationToken).ConfigureAwait(false);
			if (Boolean(last, "active"))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.True;

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
