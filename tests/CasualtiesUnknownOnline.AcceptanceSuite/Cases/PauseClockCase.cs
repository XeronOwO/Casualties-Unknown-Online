using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A pause is the client's own: the host's own pause stops its clock (`Time.timeScale` 0) and the member's
/// client keeps running, then the host's resume puts the clock back to 1. The row drives the game's own pause
/// binding (the key the ESC handler reads) instead of writing the handler's `isPaused` field, so the two
/// guards the game puts in front of a pause are exercised rather than bypassed.
/// </summary>
internal sealed class PauseClockCase : AcceptanceCase
{
	public override string Name => "pause-clock";

	/// <summary>The pause and the time scale the row sets to separate the two clocks.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Clock;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The game refuses a pause while its world is still generating, a cutscene runs, or the death/self-destruct
		// screens are up (`PauseHandler.disallowPause`, PauseHandler.cs:22-28) — a player presses the key once the world is
		// theirs, so the row waits for that moment instead of pressing into a refusal and calling it a product fault.
		await CloseTheConsoleAsync(context, cancellationToken).ConfigureAwait(false);
		var ready = await WaitForPauseGateAsync(context, cancellationToken).ConfigureAwait(false);
		Require(ready is not null, "the game's pause gates never opened: " + Raw(await context.RecipeAsync("pause-state", cancellationToken).ConfigureAwait(false)));

		var duringPress = await PressPauseAsync(context, expectPaused: !Boolean(await context.RecipeAsync("pause-state", cancellationToken).ConfigureAwait(false), "paused"), cancellationToken).ConfigureAwait(false);

		var pausedScale = await WaitForScaleAsync(context, 0, cancellationToken).ConfigureAwait(false);
		var state = await context.RecipeAsync("pause-state", cancellationToken).ConfigureAwait(false);
		context.Read("pause state after the key", state);
		Require(pausedScale is not null, "the host's clock kept running after the pause key was pressed; the game's own pause gates were: " + Raw(state) + "; while the key was held: " + Raw(duringPress));

		// A pause is the client's own; the member's clock is not stopped by the host's pause.
		var member = await context.MemberRecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false);
		context.Read("member while host paused", member);
		Require(!Boolean(member, "paused"), "the member's own clock paused with the host: " + Raw(member));

		await PressPauseAsync(context, expectPaused: !Boolean(await context.RecipeAsync("pause-state", cancellationToken).ConfigureAwait(false), "paused"), cancellationToken).ConfigureAwait(false);
		var resumedScale = await WaitForScaleAsync(context, 1, cancellationToken).ConfigureAwait(false);
		Require(resumedScale is not null, "the host's clock did not resume after the pause key was pressed again");
	}

	// The game's own pause binding, held until the game has acted on it the way a player's press is held for a moment.
	// A fixed 120 ms was a race: one stutter while the world streams in and the whole press falls between two frames.
	private static async Task<JsonElement?> PressPauseAsync(CaseContext context, bool expectPaused, CancellationToken cancellationToken)
	{
		var down = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "pause"), ("action", "down")).ConfigureAwait(false);
		Require(Boolean(down, "ok"), "the pause key could not be staged down: " + Raw(down));
		JsonElement? during = null;
		for (var attempt = 0; attempt < 15; attempt += 1)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
			var state = await context.RecipeAsync("pause-state", cancellationToken).ConfigureAwait(false);
			during = state;
			if (Boolean(state, "paused") == expectPaused)
			{
				break;
			}
		}

		var up = await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "pause"), ("action", "up")).ConfigureAwait(false);
		Require(Boolean(up, "ok"), "the pause key could not be staged up: " + Raw(up));
		return during;
	}

	// The game's pause gates: a handler that exists, no settings page, and nothing that disallows a pause (a world still
	// generating, a cutscene, the death or self-destruct screens). A player presses the key once the world is theirs.
	private static async Task<JsonElement?> WaitForPauseGateAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 20; attempt += 1)
		{
			var read = await context.RecipeAsync("pause-state", cancellationToken).ConfigureAwait(false);
			if (attempt == 0)
			{
				context.Read("pause gates before the key", read);
			}

			if (Boolean(read, "handler") && !Boolean(read, "settings") && !Boolean(read, "disallowed"))
			{
				return read;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static async Task<double?> WaitForScaleAsync(CaseContext context, double expected, CancellationToken cancellationToken)
	{
		// The game ramps its own pause in (`PauseHandler.pauseProgress`) before timeScale reaches 0, and a client whose
		// world is still streaming can take seconds; the row waits for the number instead of a fixed press window
		// (measured 2026-11-25: the state read after a 3 s wait already said paused, the scale arrived after it).
		for (var attempt = 0; attempt < 30; attempt += 1)
		{
			var read = await context.RecipeAsync("time-scale-read", cancellationToken).ConfigureAwait(false);
			var scale = Number(read, "timeScale");
			if (attempt == 0)
			{
				context.Read("host-scale-" + attempt, read);
			}

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

	/// <summary>
	/// The game's own console owns the keyboard while it is open (`PlayerCamera.HandleInput` returns early on it), so a
	/// pause pressed with the console up is swallowed. A row before this one can leave it up (measured 2026-11-25: the
	/// batch read `handler:true` and nothing else, while `pause-7` alone passed); the player closes it with its own key.
	/// </summary>
	private static async Task CloseTheConsoleAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 10; attempt += 1)
		{
			var clock = await context.RecipeAsync("frame-clock", cancellationToken).ConfigureAwait(false);
			if (!Boolean(clock, "console"))
			{
				return;
			}

			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "console"), ("action", "down")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "console"), ("action", "up")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
		}
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
