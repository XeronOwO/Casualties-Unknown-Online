using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The radiation line: the host's world crosses its own layer-time limit, the game activates the line,
/// and the member's own client shows the same line (the row `radiation-line-descent-rate-per-side`).
/// The limit is a field written at world creation, so the fixture sets it before it sets the layer
/// time; the line itself cannot be switched off, which is why this case sits late in the order.
/// </summary>
internal sealed class RadiationLineCase : AcceptanceCase
{
	public override string Name => "radiation-line";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// The line activates only past the world's own limit; the field is private and written once.
		var limit = await context.RecipeAsync("layer-limit-set", cancellationToken, ("seconds", "0.5")).ConfigureAwait(false);
		context.Read("limit", limit);
		Require(Boolean(limit, "ok"), "the layer limit could not be written: " + Raw(limit));

		var time = await context.RecipeAsync("layer-time-set", cancellationToken, ("seconds", "600")).ConfigureAwait(false);
		context.Read("layer-time", time);
		Require(Boolean(time, "ok"), "the layer time could not be written: " + Raw(time));

		var hostState = await WaitActiveAsync(context, false, cancellationToken).ConfigureAwait(false);
		context.Read("host-line", hostState);
		Require(Boolean(hostState, "active"), "the host's line never activated: " + Raw(hostState));

		var memberState = await WaitActiveAsync(context, true, cancellationToken).ConfigureAwait(false);
		context.Read("member-line", memberState);
		Require(Boolean(memberState, "active"), "the member's own client never shows the line: " + Raw(memberState));
		Require(Number(memberState, "timeGone") > 0, "the member's line has no elapsed time: " + Raw(memberState));

		// A line the host owns: the two views descend together, so their heights are within a few
		// seconds of each other's rate rather than an exact match.
		var hostY = Number(hostState, "lineY");
		var memberY = Number(memberState, "lineY");
		Require(Math.Abs(hostY - memberY) < 40, $"the two views' lines are {Math.Abs(hostY - memberY):0.#} apart: host {hostY:0.#}, member {memberY:0.#}");

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
