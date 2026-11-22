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

	// Needs a lockable the run has not picked yet and a hold long enough to fill the game's progress;
	// the registry places it before the cases that open containers for their own readings.

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The player walks up to a locked door and uses it; the game's own entry (`Openable.OnUse`, `Openable.cs:8-31`)
		// decides whether a minigame starts and which pick it hands the lock. Nothing here constructs the minigame.
		var doors = await context.RecipeAsync("openable-find", cancellationToken).ConfigureAwait(false);
		context.Read("doors", doors);
		Require(Boolean(doors, "ok"), "the door scan failed: " + Raw(doors));
		// A door is only usable when the pointer's own raycast lands on it: a crate in front reads exactly like a door
		// that refuses the key (`.acceptance/lock-8`: the pointer named `containercrate(Clone)`). So each candidate is
		// reached, pointed at, and read before the key goes down.
		var candidates = LockableDoors(doors);
		Require(candidates.Count > 0, "no locked door stands in this world: " + Raw(doors));
		JsonElement? state = null;
		Door? chosen = null;
		foreach (var candidate in candidates.Take(3))
		{
			var walked = await context.RecipeAsync("teleport", cancellationToken,
				("x", (candidate.X - 2).ToString("0.###", CultureInfo.InvariantCulture)),
				("y", candidate.Y.ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("walked", walked);
			if (Boolean(walked, "ok") != true)
			{
				continue;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
			var body = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			context.Read("body", body);
			if (Math.Abs(NumberOr(body, "posY", double.MaxValue) - candidate.Y) > 8
				|| Math.Abs(NumberOr(body, "posX", double.MaxValue) - candidate.X) > 6)
			{
				continue;
			}

			// The door's usable collider sits at the door's own transform, not at the centre of the block under it
			// (measured 2026-11-23: aiming at the block named `containercrate(Clone)` under the pointer, so the use key
			// landed on nothing and no minigame started).
			var pointed = await context.RecipeAsync("aim-at-world", cancellationToken,
				("x", candidate.X.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", candidate.Y.ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("door-aim", pointed);
			if (Boolean(pointed, "ok") != true)
			{
				continue;
			}

			var under = await context.RecipeAsync("pointer-hit", cancellationToken).ConfigureAwait(false);
			context.Read("pointer-under", under);

			// The pointer's own hit is recorded, not gated: a crate in front of a door is a real situation, and whether
			// the game starts the lock is the only fact that decides — the key press is harmless when it lands on nothing.
			// A door is opened by the mouse: the game's own start-dragging path (`PlayerCamera.HandleStartDragging`,
			// `PlayerCamera.cs:1521`) runs on the left button, finds the usable object under the pointer and sends it
			// `OnUse` (`:1601-1606`). Pressing the use *key* never reaches it (measured 2026-11-23: three doors, key
			// pressed, `lockpick-state.active` stayed false).
			var pressed = await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "press"), ("button", "0")).ConfigureAwait(false);

			Require(Boolean(pressed, "ok"), "the harness could not click the door: " + Raw(pressed));
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "release"), ("button", "0")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			state = await context.RecipeAsync("lockpick-state", cancellationToken).ConfigureAwait(false);
			context.Read("lockpick", state);
			if (Boolean(state, "active") == true)
			{
				chosen = candidate;
				break;
			}
		}

		Require(chosen.HasValue, "no reachable door started the lockpick minigame: " + Raw(state));
		context.Note($"door at {chosen!.Value.X:0.##},{chosen.Value.Y:0.##} opened the minigame");

		var aim = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);

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
		// The aim's scale is close, not exact, and the hand clamps past the ring's edge, so a fixed
		// correction cannot be trusted (2026-11-14: one run landed on the band, the next at magnitude
		// 314). The live answer carries the mapping the game itself uses — `screenPos` and `uiScale`,
		// hand = (pointer - screenPos) / uiScale on both axes — so the correction is computed from the
		// game's own numbers each time.
		for (var fix = 0; fix < 4; fix++)
		{
			if (Math.Abs(handX - targetX) < 5 && Math.Abs(handY - targetY) < 5)
			{
				break;
			}

			var live = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
			var screen = StringValue(live, "screenPos").Split(',');
			// A minigame that closed between the aim and this read carries no scale at all: the reading
			// below is the one that decides whether it has to be reopened, so this loop breaks instead
			// of reporting a missing scale as the row's verdict (2026-11-21: the throw was the whole
			// message the row gave).
			var ui = NumberOr(live, "uiScale", 0);
			if (screen.Length != 2 || ui <= 0)
			{
				break;
			}

			aimX = double.Parse(screen[0], CultureInfo.InvariantCulture) + (targetX * ui);
			aimY = double.Parse(screen[1], CultureInfo.InvariantCulture) + (targetY * ui);
			(handX, handY) = await MoveAndReadHand(context, aimX, aimY, cancellationToken).ConfigureAwait(false);
		}

		var liveCheck = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
		if (NumberOr(liveCheck, "uiScale", 0) <= 0)
		{
			// The minigame can close between the aim and the press (a stray click, a timeout): the player
			// would use the door again, so the row does the same instead of constructing the minigame.
			context.Note("the minigame closed during the aim; using the door again");
			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "iteminteract"), ("action", "down")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "iteminteract"), ("action", "up")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
			var reopened = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);
			Require(Boolean(reopened, "ok"), "the door did not start the minigame again: " + Raw(reopened));
			aimX = Number(reopened, "screenX");
			aimY = Number(reopened, "screenY");
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
			// A press only counts as a fresh down edge (the minigame reads GetKeyDown), and in the full
			// order the first edge is sometimes consumed before the minigame reads it (measured
			// 2026-11-14: the hand sat on the band, `handStarted` stayed false, progress 0). So the
			// press is repeated — release then press — until the lock starts to turn or the minigame
			// closes; every press is a new edge and the hand stays on the band throughout.
			var progress = 0.0;
			for (var attempt = 0; attempt < 20; attempt++)
			{
				await context.RecipeAsync("lockpick-hold", cancellationToken, ("x", hold.X), ("y", hold.Y), ("mode", "release")).ConfigureAwait(false);
				await context.RecipeAsync("lockpick-hold", cancellationToken, ("x", hold.X), ("y", hold.Y), ("mode", "press")).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
				var pressed = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
				progress = NumberOr(pressed, "lockProgress", progress);
				var pressedKind = StringValue(pressed, "kind");
				if (progress > 0 || !string.Equals(pressedKind, "LockpingMinigame", StringComparison.Ordinal))
				{
					break;
				}
			}

			for (var tick = 0; tick < 60; tick++)
			{
				// Keep the pointer ON the band every tick: the game reads the pointer each frame, and a
				// poll that only reads lets the hand fall back to the real mouse between evals (measured
				// 2026-11-14: progress stalled at 0.734 while the poll read only).
				await context.RecipeAsync("lockpick-hold", cancellationToken, ("x", hold.X), ("y", hold.Y), ("mode", "press")).ConfigureAwait(false);
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
		// The minigame closing is the game's own completion signal; the clips it plays are index-named
		// and only some opens drop a crate (measured 2026-11-14), so the sound is recorded, not judged.
		context.Note($"clips {line}");
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

	private readonly record struct Door(double X, double Y, double Distance, string Name);

	// Every door that is a lock rather than a keypad or an open frame, nearest level first.
	private static List<Door> LockableDoors(JsonElement? reading)
	{
		var doors = new List<Door>();
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("doors", out var list)
			|| list.ValueKind != JsonValueKind.Array)
		{
			return doors;
		}

		foreach (var door in list.EnumerateArray())
		{
			if (door.ValueKind != JsonValueKind.Object
				|| (door.TryGetProperty("instant", out var instant) && instant.ValueKind == JsonValueKind.True)
				|| (door.TryGetProperty("keypad", out var keypad) && keypad.ValueKind == JsonValueKind.True)
				|| (door.TryGetProperty("health", out var health) && health.ValueKind == JsonValueKind.Number && health.GetDouble() <= 0))
			{
				continue;
			}

			if (!door.TryGetProperty("x", out var x) || x.ValueKind != JsonValueKind.Number
				|| !door.TryGetProperty("y", out var y) || y.ValueKind != JsonValueKind.Number)
			{
				continue;
			}

			var distance = door.TryGetProperty("distance", out var away) && away.ValueKind == JsonValueKind.Number ? away.GetDouble() : double.MaxValue;
			var name = door.TryGetProperty("name", out var label) && label.ValueKind == JsonValueKind.String ? label.GetString() ?? "" : "";
			doors.Add(new Door(x.GetDouble(), y.GetDouble(), distance, name));
		}

		return [.. doors.OrderBy(candidate => candidate.Distance)];
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

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
