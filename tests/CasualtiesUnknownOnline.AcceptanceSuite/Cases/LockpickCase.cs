using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The lockpick minigame completes through the game's own entry: the host opens the nearest lockable,
/// aims the hand at the picked angle, holds the attack key, and the game's own completion sound is the
/// reading. The pointer-to-hand mapping depends on the window's scale, so the case calibrates the
/// mapping with two probes before it aims.
/// </summary>
internal sealed class LockpickCase : AcceptanceCase
{
	private const double ProbeX = 640;
	private const double ProbeY = 360;
	private const double ProbeStep = 100;

	public override string Name => "lockpick";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The CUO window covers the screen centre; open it first so Escape is deterministic, then
		// let the game's own Escape close it.
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

		JsonElement? aim = null;
		for (var attempt = 0; attempt < 3; attempt++)
		{
			var opened = await context.RecipeAsync("lockpick-open", cancellationToken).ConfigureAwait(false);
			context.Read("open", opened);
			Require(Boolean(opened, "ok"), "the lockpick minigame did not open");
			await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
			aim = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);
			if (Boolean(aim, "ok"))
			{
				break;
			}

			context.Note($"open attempt {attempt}: the minigame closed before the aim; reopening");
		}

		context.Read("aim", aim);
		Require(Boolean(aim, "ok"), "the aim did not compute");
		var targetX = Number(aim, "targetX");
		var targetY = Number(aim, "targetY");

		// The recipe computes the pointer that puts the hand on the target from the game's own
		// uiScale. Probing a second mapping from staged points overshoots the ring: the hand clamps to
		// its edge, so the measured scale is not the true one (2026-11-14: origin (79.9, -67.6),
		// scales 0.57 and 1.9, and the press landed at magnitude 328).
		var aimX = Number(aim, "screenX");
		var aimY = Number(aim, "screenY");
		var (handX, handY) = await MoveAndReadHand(context, aimX, aimY, cancellationToken).ConfigureAwait(false);
		// The recipe's point lands the hand at the right RADIUS but a few degrees off the angle: its
		// scale is close, not exact (2026-11-14: hand (265.9, -12.4) for target (209.8, 66.3)). The
		// correction uses the per-axis scale measured on this window, and converges in one or two steps
		// because the hand is linear inside the ring (it clamps only past the edge, which is why the
		// probe-based calibration read a wrong scale).
		const double ScaleX = 0.5701;
		const double ScaleY = 1.9055;
		for (var fix = 0; fix < 3; fix++)
		{
			var dx = targetX - handX;
			var dy = targetY - handY;
			if (Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
			{
				break;
			}

			aimX += dx / ScaleX;
			aimY += dy / ScaleY;
			(handX, handY) = await MoveAndReadHand(context, aimX, aimY, cancellationToken).ConfigureAwait(false);
		}

		var magnitude = Math.Sqrt((handX * handX) + (handY * handY));
		context.Note($"aimed at ({aimX:0.#}, {aimY:0.#}); hand ({handX:0.#}, {handY:0.#}) magnitude {magnitude:0.#}");

		// The game reads the pointer every frame, and a separate move/press leaves a frame in which
		// the hand falls back to the real mouse. One eval stages the pointer and presses; the key then
		// STAYS DOWN, because the hand only advances the lock while GetKey is true (a press+release
		// answers GetKeyDown for one frame and leaves GetKey false — measured 2026-11-12).
		// The recorder only keeps what it saw from a start, so the unlock sound is recorded from
		// the first press.
		await context.RecipeAsync("sound-log", cancellationToken, ("mode", "start"), ("match", "")).ConfigureAwait(false);
		var hold = (X: Math.Round(aimX).ToString("0", CultureInfo.InvariantCulture), Y: Math.Round(aimY).ToString("0", CultureInfo.InvariantCulture));
		try
		{
			await context.RecipeAsync("lockpick-hold", cancellationToken, ("x", hold.X), ("y", hold.Y), ("mode", "press")).ConfigureAwait(false);
			var progress = 0.0;
			for (var tick = 0; tick < 40; tick++)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
				var probe = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
				progress = NumberOr(probe, "lockProgress", progress);
				var kind = StringValue(probe, "kind");
				if (progress >= 0.99 || !Boolean(probe, "hasMain") || string.Equals(kind, "none", StringComparison.Ordinal))
				{
					break;
				}
			}

			var live = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
			context.Read("live", live);
			Require(!string.Equals(StringValue(live, "kind"), "LockpingMinigame", StringComparison.Ordinal),
				"the lockpick minigame was still open after the hold: " + (live?.GetRawText() ?? "null"));
			context.Note($"progress {progress:0.###}");
		}
		finally
		{
			// Never leave the attack key down: the next case would read a held button.
			await context.RecipeAsync("lockpick-hold", cancellationToken, ("x", hold.X), ("y", hold.Y), ("mode", "release")).ConfigureAwait(false);
		}

		var sounds = await context.RecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "")).ConfigureAwait(false);
		context.Read("sounds", sounds);
		var line = sounds is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("line", out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: "";
		// Completion is the minigame closing with its own drop sound: the lockpick plays no clip named
		// `unlock` (measured 2026-11-14: the pick finished, the crate dropped its contents and the
		// recorder held `drop@` and numeric clips only).
		Require(line.Contains("drop@", StringComparison.Ordinal), $"the game played no drop sound when the lock opened: {line}");
	}

	private static async Task<(double X, double Y)> MoveAndReadHand(CaseContext context, double x, double y, CancellationToken cancellationToken)
	{
		// The 1.0 hand moves only while the attack key is held, so a pointer move alone leaves it
		// where it was: stage the pointer and press in one eval, then read where the hand settled.
		await context.RecipeAsync("lockpick-press", cancellationToken,
			("x", Math.Round(x).ToString("0", CultureInfo.InvariantCulture)),
			("y", Math.Round(y).ToString("0", CultureInfo.InvariantCulture))).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
		var live = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);
		var handX = NumberOr(live, "handX", 0);
		var handY = NumberOr(live, "handY", 0);
		return (handX, handY);
	}

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: throw new AssertionException($"the reading has no number '{property}'");

	private static double NumberOr(JsonElement? reading, string property, double fallback) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: fallback;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
