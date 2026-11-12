using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The run's clock is the host's, and a member's request reaches it: the guest asks for the fast speed
/// (the production `WorldTimeRequest` send), the host applies it and broadcasts, and both clients' own
/// `PlayerCamera.curTimeScale` read `Fast` — the field the HUD's icon row shows. The row came from the
/// save batch's world-time rows; the clock returns to normal before the case ends.
/// </summary>
internal sealed class WorldClockCase : AcceptanceCase
{
	public override string Name => "world-clock";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var requested = await context.MemberRecipeAsync("world-time-request", cancellationToken, ("speed", "fast")).ConfigureAwait(false);
		context.Read("member request fast", requested);
		Require(Boolean(requested, "ok"), "the member's clock request did not answer: " + Raw(requested));
		Require(Boolean(requested, "sent"), "the member's request was not sent: " + Raw(requested));

		var hostFast = await WaitForScaleAsync(context, "Fast", hostSide: true, cancellationToken).ConfigureAwait(false);
		context.Note("host fast: " + hostFast);
		Require(hostFast, "the host's clock did not become fast after the member's request");
		var memberFast = await WaitForScaleAsync(context, "Fast", hostSide: false, cancellationToken).ConfigureAwait(false);
		context.Note("member fast: " + memberFast);
		Require(memberFast, "the member's own clock did not become fast");

		var restored = await context.MemberRecipeAsync("world-time-request", cancellationToken, ("speed", "normal")).ConfigureAwait(false);
		context.Read("member request normal", restored);
		Require(Boolean(restored, "ok"), "the member could not ask for normal speed: " + Raw(restored));
		var hostNormal = await WaitForScaleAsync(context, "Normal", hostSide: true, cancellationToken).ConfigureAwait(false);
		Require(hostNormal, "the host's clock did not return to normal");
		var memberNormal = await WaitForScaleAsync(context, "Normal", hostSide: false, cancellationToken).ConfigureAwait(false);
		Require(memberNormal, "the member's own clock did not return to normal");
	}

	private static async Task<bool> WaitForScaleAsync(CaseContext context, string expected, bool hostSide, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 12; attempt += 1)
		{
			var read = hostSide
				? await context.RecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false)
				: await context.MemberRecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false);
			var scale = StringValue(read, "curTimeScale");
			if (attempt == 0 || attempt == 11)
			{
				context.Read((hostSide ? "host" : "member") + "-scale-" + attempt, read);
			}

			if (string.Equals(scale, expected, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return false;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string StringValue(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(property, out var value))
		{
			return string.Empty;
		}

		return value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: value.ValueKind == JsonValueKind.Number
				? value.GetDouble().ToString(CultureInfo.InvariantCulture)
				: string.Empty;
	}

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
