using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The pause is the client's own: the host's own pause stops its clock (`Time.timeScale` 0) and the
/// member's client keeps running, then the host's resume puts the clock back to 1. The row came from
/// the world-acceleration rows; the recipe runs the same call the game's ESC handler makes.
/// </summary>
internal sealed class PauseClockCase : AcceptanceCase
{
	public override string Name => "pause-clock";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The game's own `TogglePause` refuses while its settings menu exists or while the player is in
		// a state that disallows pausing, so the row closes the CUO window first (the game's own Escape)
		// and waits for the pause container's own frame before it reads the clock.
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		var paused = await context.RecipeAsync("pause-toggle", cancellationToken);
		context.Read("host pause", paused);
		Require(Boolean(paused, "ok"), "the pause toggle did not answer: " + Raw(paused));
		var hostPaused = await WaitForPausedAsync(context, true, cancellationToken).ConfigureAwait(false);
		Require(hostPaused, "the host did not pause: " + Raw(paused));
		var pausedScale = await WaitForScaleAsync(context, 0, cancellationToken).ConfigureAwait(false);
		Require(pausedScale is not null, "the host's clock kept running while paused");

		// A pause is the client's own; the member's clock is not stopped by the host's pause.
		var member = await context.MemberRecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false);
		context.Read("member while host paused", member);
		Require(!Boolean(member, "paused"), "the member's own clock paused with the host: " + Raw(member));

		var resumed = await context.RecipeAsync("pause-toggle", cancellationToken);
		context.Read("host resume", resumed);
		Require(Boolean(resumed, "ok"), "the resume did not answer: " + Raw(resumed));
		var hostResumed = await WaitForPausedAsync(context, false, cancellationToken).ConfigureAwait(false);
		Require(hostResumed, "the host stayed paused: " + Raw(resumed));
		var resumedScale = await WaitForScaleAsync(context, 1, cancellationToken).ConfigureAwait(false);
		Require(resumedScale is not null, "the host's clock did not resume");
	}

	private static async Task<bool> WaitForPausedAsync(CaseContext context, bool expected, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 8; attempt += 1)
		{
			var read = await context.RecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false);
			if (attempt == 0) { context.Read("host-paused-" + attempt, read); }
			if (Boolean(read, "paused") == expected)
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
		}

		return false;
	}

	private static async Task<double?> WaitForScaleAsync(CaseContext context, double expected, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 8; attempt += 1)
		{
			var read = await context.RecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false);
			var scale = Number(read, "timeScale");
			if (attempt == 0) { context.Read("host-scale-" + attempt, read); }
			if (scale is not null && Math.Abs(scale.Value - expected) < 0.05)
			{
				return scale;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static double? Number(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(property, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number => value.GetDouble(),
			JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
			_ => null,
		};
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
