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

		// The clock the claim is about is the one that moves the world: `Time.timeScale`, not the enum beside it. The host
		// presses the game's own speed key (the game's Ultra setting is 20x, `PlayerCamera.cs:756`), and both clients must
		// end up running at that scale — the host decides, so the member's own Unity clock follows the broadcast instead of
		// keeping whatever it had.
		var hostBefore = await ScaleAsync(context, hostSide: true, cancellationToken).ConfigureAwait(false);
		var memberBefore = await ScaleAsync(context, hostSide: false, cancellationToken).ConfigureAwait(false);
		context.Note($"before the host's speed key: host={hostBefore} member={memberBefore}");
		await PressBindAsync(context, hostSide: true, "speed3", cancellationToken).ConfigureAwait(false);
		var hostUltra = await WaitForUnityScaleAsync(context, "20", hostSide: true, cancellationToken).ConfigureAwait(false);
		var memberUltra = await WaitForUnityScaleAsync(context, "20", hostSide: false, cancellationToken).ConfigureAwait(false);
		context.Note($"after the host's speed key: host={hostUltra} member={memberUltra}");
		Require(hostUltra, "the host's own Unity clock did not reach the game's Ultra scale of 20");
		Require(memberUltra, "the member's Unity clock did not follow the host's Ultra scale — the world runs at two speeds");

		// Slowing back down is pressed a few times with a recording, because the game applies a speed on the key's own
		// down frame and a single missed frame would read as a product refusal.
		var hostBack = false;
		for (var attempt = 0; attempt < 3 && !hostBack; attempt++)
		{
			await PressBindAsync(context, hostSide: true, "speed1", cancellationToken).ConfigureAwait(false);
			var reading = await ScaleAsync(context, hostSide: true, cancellationToken).ConfigureAwait(false);
			context.Note($"slow-down attempt {attempt}: host={reading?.GetRawText() ?? "null"}");
			hostBack = await WaitForUnityScaleAsync(context, "1", hostSide: true, cancellationToken).ConfigureAwait(false);
		}

		var memberBack = await WaitForUnityScaleAsync(context, "1", hostSide: false, cancellationToken).ConfigureAwait(false);
		var hostEdges = await context.RecipeAsync("key-edges", cancellationToken).ConfigureAwait(false);
		var memberEdges = await context.MemberRecipeAsync("key-edges", cancellationToken).ConfigureAwait(false);
		context.Note($"staged key edges: host={hostEdges?.GetRawText() ?? "null"} member={memberEdges?.GetRawText() ?? "null"}");
		Require(hostBack, "the host's clock did not return to the game's normal scale of 1; the batch's staged key edges were: " + (hostEdges?.GetRawText() ?? "null"));
		Require(memberBack, "the member's clock did not return to the game's normal scale of 1");
	}

	// The game's own speed key, pressed and released through the same binding the player uses.
	private static async Task PressBindAsync(CaseContext context, bool hostSide, string bind, CancellationToken cancellationToken)
	{
		await BindAsync(context, hostSide, bind, "down", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
		await BindAsync(context, hostSide, bind, "up", cancellationToken).ConfigureAwait(false);
	}

	private static Task<JsonElement?> BindAsync(CaseContext context, bool hostSide, string bind, string action, CancellationToken cancellationToken) =>
		hostSide
			? context.RecipeAsync("stage-bind", cancellationToken, ("bind", bind), ("action", action))
			: context.MemberRecipeAsync("stage-bind", cancellationToken, ("bind", bind), ("action", action));

	private static Task<JsonElement?> ScaleAsync(CaseContext context, bool hostSide, CancellationToken cancellationToken) =>
		hostSide
			? context.RecipeAsync("time-scale-read", cancellationToken)
			: context.MemberRecipeAsync("time-scale-read", cancellationToken);

	private static async Task<bool> WaitForUnityScaleAsync(CaseContext context, string expected, bool hostSide, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 24; attempt++)
		{
			var reading = await ScaleAsync(context, hostSide, cancellationToken).ConfigureAwait(false);
			if (StringValue(reading, "timeScale") == expected)
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return false;
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

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
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
