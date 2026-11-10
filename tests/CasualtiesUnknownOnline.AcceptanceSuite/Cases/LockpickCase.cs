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
		// The CUO window covers the screen centre; the game's own Escape closes it.
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

		var opened = await context.RecipeAsync("lockpick-open", cancellationToken).ConfigureAwait(false);
		context.Read("open", opened);
		Require(Boolean(opened, "ok"), "the lockpick minigame did not open");

		var aim = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);
		context.Read("aim", aim);
		Require(Boolean(aim, "ok"), "the aim did not compute");
		var targetX = Number(aim, "targetX");
		var targetY = Number(aim, "targetY");

		// Calibrate: the hand follows the pointer linearly per axis, but the scale and the y sign
		// belong to the window, not to the recipe. Two probes give both.
		var (originX, originY) = await MoveAndReadHand(context, ProbeX, ProbeY, cancellationToken).ConfigureAwait(false);
		var (eastX, _) = await MoveAndReadHand(context, ProbeX + ProbeStep, ProbeY, cancellationToken).ConfigureAwait(false);
		var (_, northY) = await MoveAndReadHand(context, ProbeX, ProbeY + ProbeStep, cancellationToken).ConfigureAwait(false);
		var scaleX = (eastX - originX) / ProbeStep;
		var scaleY = (northY - originY) / ProbeStep;
		context.Note($"hand mapping: origin ({originX:0.#}, {originY:0.#}) scale ({scaleX:0.####}, {scaleY:0.####})");
		Require(Math.Abs(scaleX) > 1e-4 && Math.Abs(scaleY) > 1e-4, "the pointer does not move the minigame hand");

		var aimX = ProbeX + (targetX - originX) / scaleX;
		var aimY = ProbeY + (targetY - originY) / scaleY;
		var (handX, handY) = await MoveAndReadHand(context, aimX, aimY, cancellationToken).ConfigureAwait(false);
		var magnitude = Math.Sqrt((handX * handX) + (handY * handY));
		context.Note($"aimed at ({aimX:0.#}, {aimY:0.#}); hand ({handX:0.#}, {handY:0.#}) magnitude {magnitude:0.#}");
		if (magnitude is < 195 or > 247)
		{
			var correctedX = aimX + (targetX - handX) / scaleX;
			var correctedY = aimY + (targetY - handY) / scaleY;
			aimX = correctedX;
			aimY = correctedY;
			(handX, handY) = await MoveAndReadHand(context, correctedX, correctedY, cancellationToken).ConfigureAwait(false);
			magnitude = Math.Sqrt((handX * handX) + (handY * handY));
			context.Note($"corrected at ({correctedX:0.#}, {correctedY:0.#}); hand ({handX:0.#}, {handY:0.#}) magnitude {magnitude:0.#}");
		}

		// The game reads the pointer every frame, and a separate move/press leaves a frame in which
		// the hand falls back to the real mouse. One eval stages the pointer and presses in the same
		// frame; retry a few times in case the first press lands during a scene tick.
		var started = false;
		for (var attempt = 0; attempt < 10 && !started; attempt++)
		{
			var pressed = await context.RecipeAsync("lockpick-press", cancellationToken,
				("x", Math.Round(aimX).ToString("0", CultureInfo.InvariantCulture)),
				("y", Math.Round(aimY).ToString("0", CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("press", pressed);
			await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
			var probe = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
			started = Boolean(probe, "handStarted");
			context.Note($"press {attempt}: started={started} {Number(probe, "handMagnitude")}");
		}

		Require(started, "the minigame never marked the hold as started on the ring");
		await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

		var live = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
		context.Read("live", live);

		var sounds = await context.RecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "unlock")).ConfigureAwait(false);
		context.Read("sounds", sounds);
		var line = sounds is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("line", out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: "";
		Require(line.Contains("unlock@", StringComparison.Ordinal), $"the game did not play its unlock sound: {line}");
	}

	private static async Task<(double X, double Y)> MoveAndReadHand(CaseContext context, double x, double y, CancellationToken cancellationToken)
	{
		await context.RecipeAsync("virtual-input", cancellationToken,
			("mode", "move"),
			("x", Math.Round(x).ToString("0", CultureInfo.InvariantCulture)),
			("y", Math.Round(y).ToString("0", CultureInfo.InvariantCulture)),
			("button", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
		var live = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);
		var handX = Number(live, "handX");
		var handY = Number(live, "handY");
		return (handX, handY);
	}

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: throw new AssertionException($"the reading has no number '{property}'");

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
