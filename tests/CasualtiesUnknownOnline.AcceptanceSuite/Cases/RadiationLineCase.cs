using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The radiation line is the host's fact: the layer time sets it, and the member's own client reads
/// the same line without driving it. The row came from `radiation-line-descent-rate-per-side`; the
/// case writes the time, reads both sides, and restores the layer so later cases start clean.
/// </summary>
internal sealed class RadiationLineCase : AcceptanceCase
{
	public override string Name => "radiation-line";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var written = await context.RecipeAsync("layer-time-set", cancellationToken, ("seconds", "600"));
		context.Read("layer-time", written);
		Require(Boolean(written, "ok"), "the layer time was not written: " + Raw(written));

		// The line follows the host's clock; a frame or two of settle keeps both reads on the same tick.
		await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);

		var host = await context.RecipeAsync("radiation-line-state", cancellationToken);
		context.Read("host-line", host);
		Require(Boolean(host, "ok"), "the host cannot read the line: " + Raw(host));
		Require(Boolean(host, "active"), "the host's line is not active after the layer time: " + Raw(host));
		var hostTime = Number(host, "timeGone");

		var member = await context.MemberRecipeAsync("radiation-line-state", cancellationToken);
		context.Read("member-line", member);
		Require(Boolean(member, "ok"), "the member cannot read the line: " + Raw(member));
		Require(Boolean(member, "active"), "the member's line is not active: " + Raw(member));
		var memberTime = Number(member, "timeGone");

		// The host-synced line is one fact: both sides agree to within a tick's worth of drift.
		Require(Math.Abs(hostTime - memberTime) <= 5, $"the two sides disagree on the line ({hostTime} vs {memberTime})");
		Require(Math.Abs(Number(host, "lineY") - Number(member, "lineY")) <= 1, "the two sides disagree on the line's height");

		await context.RecipeAsync("layer-time-set", cancellationToken, ("seconds", "0")).ConfigureAwait(false);
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
		&& value.TryGetDouble(out var number)
			? number
			: double.NaN;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
