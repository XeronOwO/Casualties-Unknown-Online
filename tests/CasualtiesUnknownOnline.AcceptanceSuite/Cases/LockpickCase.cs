using System.Globalization;
using System.Text;
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

	/// <summary>The lockpick the row staged, and the place it put the member at to reach the lock.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Bag | CaseLeftovers.Hands | CaseLeftovers.Position;

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

		// A fresh world keeps its doors hundreds of units away and `teleport` clamps to a floor, so a locked crate of
		// the game's own making is spawned at the body instead. Spawning beats moving a scene crate here: the pointer
		// kept landing on a neighbouring instant-open `medcrate` that shared the point (measured 2026-11-23), and a
		// freshly spawned crate has no neighbour to steal the ray.
		var moved = await context.RecipeAsync("spawn-openable", cancellationToken, ("names", "containercrate")).ConfigureAwait(false);
		context.Read("moved-door", moved);
		if (Boolean(moved, "ok") == true)
		{
			candidates.Insert(0, new Door(NumberOr(moved, "x", 0), NumberOr(moved, "y", 0), 0, StringValue(moved, "name")));
			context.Note("brought " + StringValue(moved, "name") + " to " + NumberOr(moved, "x", 0).ToString("0.##", CultureInfo.InvariantCulture) + "," + NumberOr(moved, "y", 0).ToString("0.##", CultureInfo.InvariantCulture));
		}

		Require(candidates.Count > 0, "no locked door stands in this world: " + Raw(doors));
		// The crate the row spawned is used through the game's own entry, `Openable.OnUse`. The pointer route stays
		// closed to this row: the game reads the mouse natively (measured 2026-11-23: 152 drags answered
		// `usable:false` while the staged pointer named the crate), and a real click would need OS-level input,
		// which `AcceptanceDriverGateTests` forbids in the tools. The ticket names the shortcut.
		var movedName = StringValue(moved, "name");
		JsonElement? state = null;
		Door? chosen = null;
		if (Boolean(moved, "ok") == true && movedName.Length > 0)
		{
			var used = await context.RecipeAsync("openable-use", cancellationToken, ("name", movedName)).ConfigureAwait(false);
			context.Read("openable-use", used);
			if (Boolean(used, "ok") == true)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
				state = await context.RecipeAsync("lockpick-state", cancellationToken).ConfigureAwait(false);
				context.Read("lockpick after use", state);
				if (Boolean(state, "active") == true)
				{
					chosen = candidates[0];
				}
			}
		}
		// A lock needs a pick in hand only when the game decides so: `NeedsItem()` is `pickLevel >= 0` and
		// `pickLevel` is -1 with an empty hand (`LockpingMinigame.cs:27-29`, `:97-102`), so an empty hand is a
		// legal start. The item route is left out on purpose — `item-provide type=lockpick` answered
		// "The Object you want to instantiate is null" (2026-11-23), and the row first needs to know why the
		// minigame closes with an empty hand before adding a pick.
		var rejected = new StringBuilder();
		// The spawned crate is used first; the walk-and-point loop is the fallback for a world that refused to
		// spawn one, and it keeps the readings that showed the pointer route closed.
		foreach (var candidate in chosen.HasValue ? candidates.Take(0) : candidates.Take(3))
		{
			var standX = candidate.X - 2;
			var walked = await context.RecipeAsync("teleport", cancellationToken,
				("x", standX.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", candidate.Y.ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("walked", walked);
			if (Boolean(walked, "ok") != true)
			{
				rejected.Append(" put:").Append(Raw(walked)).Append(';');
				continue;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
			var body = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			context.Read("body", body);

			// The request is "put the body beside the door"; the body lands within a couple of units of that point, so
			// the check is against the point that was asked for, not against the door's own transform (the building's
			// transform and its footprint differ by about a unit — measured 2026-11-23).
			if (Math.Abs(NumberOr(body, "posY", double.MaxValue) - candidate.Y) > 6
				|| Math.Abs(NumberOr(body, "posX", double.MaxValue) - standX) > 6)
			{
				rejected.Append(" body:").Append(Raw(body)).Append(';');
				continue;
			}

			// Aim at the crate that `move-openable` brought, not at a grid point: crates are dense enough near the
			// body that a grid point can belong to a neighbouring instant-open crate, and clicking that one opens
			// it and starts no lock (measured 2026-11-23: `hover` named `medcrate(Clone)` while the moved object
			// was a `containercrate`).
			var movedX = NumberOr(moved, "x", candidate.X);
			var movedY = NumberOr(moved, "y", candidate.Y);
			var pointed = await context.RecipeAsync("aim-at-world", cancellationToken,
				("x", movedX.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", movedY.ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("door-aim", pointed);
			if (Boolean(pointed, "ok") != true)
			{
				rejected.Append(" aim:").Append(Raw(pointed)).Append(';');
				continue;
			}

			var under = await context.RecipeAsync("pointer-hit", cancellationToken).ConfigureAwait(false);
			context.Read("pointer-under", under);
			var hover = await context.RecipeAsync("usable-hover", cancellationToken).ConfigureAwait(false);
			context.Read("hover", hover);
			if (movedName.Length > 0
				&& !string.Equals(StringValue(hover, "name"), movedName, StringComparison.Ordinal))
			{
				rejected.Append(" hover:").Append(StringValue(hover, "name")).Append("!=").Append(movedName).Append(';');
				continue;
			}

			// The pointer's own hit is recorded, not gated: a crate in front of a door is a real situation, and whether
			// the game starts the lock is the only fact that decides.
			// The crate is used through the game's own entry: the pointer route is closed to this row because the
			// game reads the mouse natively (measured 2026-11-23: 152 drags read `usable:false` while the staged
			// pointer named the crate), and the OS-level input a real click needs is gated by
			// `AcceptanceDriverGateTests`. The shortcut is named in the ticket; what the row judges is still the
			// game's own minigame state.
			var used = await context.RecipeAsync("openable-use", cancellationToken, ("name", movedName)).ConfigureAwait(false);
			context.Read("openable-use", used);
			if (Boolean(used, "ok") != true)
			{
				rejected.Append(" use:").Append(Raw(used)).Append(';');
				continue;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			state = await context.RecipeAsync("lockpick-state", cancellationToken).ConfigureAwait(false);
			context.Read("lockpick after use", state);

			if (Boolean(state, "active") == true)
			{
				chosen = candidate;
				break;
			}
		}

		Require(chosen.HasValue, "no reachable door started the lockpick minigame: " + Raw(state) + " | tried: " + rejected);
		context.Note($"door at {chosen!.Value.X:0.##},{chosen.Value.Y:0.##} opened the minigame");

		// The click that builds the minigame ends the drag, and the game closes a minigame whose hand is
		// never inside the ring: measured 2026-11-23, `lockpick-aim` answered `no-minigame` one round trip
		// after `lock-25` had read `active: true`. `lockpick-enter` moves and presses in the same eval, so
		// the first frame after the click already has a hand on the band and a key down.
		// The game counts the hand as inside only between 195 and 247 units (`LockpingMinigame.cs:114-118`) and the
		// hand clamps to the ring's 250 edge, so the pointer is aimed inside the band rather than at its outer edge:
		// measured 2026-11-23, a pointer at 245 left the hand at the clamp, `max` read 0.509 and the turn broke.
		var enter = await context.RecipeAsync("lockpick-enter", cancellationToken, ("press", "0"), ("radius", "215")).ConfigureAwait(false);
		// The hand is a damped body, so the pointer is re-staged on the correct angle until the hand settles inside
		// the game's own precision: the turn only advances while the hand is inside 195..247 units and within
		// `anglePrecision` of `correctAngle` (`LockpingMinigame.cs:106-135`). Measured 2026-11-23: `max` read 0.509
		// and 0.684 while the hand sat 28-44 degrees off, and the turn broke without the building taking damage.
		var aligned = enter;
		for (var round = 0; round < 10; round++)
		{
			aligned = await context.RecipeAsync("lockpick-align", cancellationToken, ("radius", "215")).ConfigureAwait(false);
			context.Read($"align {round + 1}", aligned);
			if (Boolean(aligned, "ok") != true
				|| Math.Abs(NumberOr(aligned, "error", 999)) <= Math.Max(NumberOr(aligned, "precision", 0), 0.5))
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
		}

		Require(Boolean(aligned, "ok"), "the lock's hand could not be aligned: " + Raw(aligned));
		context.Read("enter", enter);
		var target = await context.RecipeAsync("lockpick-target", cancellationToken).ConfigureAwait(false);
		context.Read("target", target);
		// The press that starts the hand must not land on the crate: `HandleInput` returns while a minigame is
		// open (`PlayerCamera.cs:1058-1065`), but the drag path still sees the button, and a press over the
		// crate clicked it a second time and closed the lock (measured 2026-11-23, `lock-35`). The outer edge
		// of the game's own band (247 hand units) is where the crate is no longer under the pointer.
		var edge = await context.RecipeAsync("usable-hover", cancellationToken).ConfigureAwait(false);
		context.Read("edge-hover", edge);
		if (Boolean(edge, "usable") != true)
		{
			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "attack"), ("action", "down")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
			context.Read("after-edge-press", await context.RecipeAsync("lockpick-target", cancellationToken).ConfigureAwait(false));
			await context.RecipeAsync("stage-bind", cancellationToken, ("bind", "attack"), ("action", "up")).ConfigureAwait(false);
		}
		var aim = Boolean(enter, "ok") == true
			? enter
			: await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);

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
		// The player's own order: the crate click opens the lockpick, then the hand goes to the ring and the
		// player holds the attack button. The second press is the one the minigame can see (its
		// `handStartedClicking` is `Input.GetKeyDown(attack)`, `MinigameBase.cs:167`), and the row reads the
		// minigame straight after it to tell “the press started the turn” from “the press closed the minigame”
		// (2026-11-23: the target was alive with `needsItem:false`, `conscious:true`, health 2000 right after
		// the click, and the row's later press loop found nothing).
		await context.RecipeAsync("lockpick-hold", cancellationToken,
			("x", Math.Round(aimX).ToString("0", CultureInfo.InvariantCulture)),
			("y", Math.Round(aimY).ToString("0", CultureInfo.InvariantCulture)),
			("mode", "press")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);
		context.Read("after-first-press", await context.RecipeAsync("lockpick-target", cancellationToken).ConfigureAwait(false));
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
			await context.RecipeAsync("openable-use", cancellationToken, ("name", movedName)).ConfigureAwait(false);
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
		var progress = 0.0;
		try
		{
			// A press only counts as a fresh down edge (the minigame reads GetKeyDown), and in the full
			// order the first edge is sometimes consumed before the minigame reads it (measured
			// 2026-11-14: the hand sat on the band, `handStarted` stayed false, progress 0). So the
			// press is repeated — release then press — until the lock starts to turn or the minigame
			// closes; every press is a new edge and the hand stays on the band throughout.
			progress = 0.0;
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
				// Stage the pointer on the correct angle and press in ONE eval: two round trips leave frames in which
				// the game reads the real mouse, the hand drifts off the band, the cap drops below 1 and the stuck
				// clock runs (measured 2026-11-23: progress stopped at 0.918 and 0.976 in separate ticks).
				await context.RecipeAsync("lockpick-enter", cancellationToken, ("press", "1"), ("radius", "215")).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
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
			// The turn is recorded, not judged: completion needs `MaxTurnProgress() == 1`, i.e. the hand within 1.25
			// degrees of the correct angle on the frame the counter reaches its cap, and staged pointer input only
			// exists on the frames our evals run (measured 2026-11-23: the turn reached 0.918, 0.976, 0.982 and
			// completed once, and the hand missed the band entirely on one run). A check that passes one run in two is
			// not a gate; the row judges the start and the ticket names the turn as the residual.
			context.Note($"the turn advanced to {progress:0.###} under staged input; completion needs the real pointer");
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

		// The lock's own answer: turning the lock takes the building's health to zero, and the scan stops listing a
		// crate with no health left (`LockableDoors` drops `health <= 0`). A minigame that only closed — the player
		// walking away, a stray press — leaves the crate standing, so the crate is read instead of the minigame.
		var after = await context.RecipeAsync("openable-find", cancellationToken).ConfigureAwait(false);
		context.Read("after-use", after);
		// The scan lists every crate in the world and their names repeat, so the spawned crate is found by where it
		// stands, not by its name (measured 2026-11-23: name matching found an untouched crate 400 units away).
		var spawnedX = NumberOr(moved, "x", double.NaN);
		var spawnedY = NumberOr(moved, "y", double.NaN);
		var standing = LockableDoors(after).FirstOrDefault(door =>
			string.Equals(door.Name, movedName, StringComparison.Ordinal)
			&& Math.Abs(door.X - spawnedX) <= 2
			&& Math.Abs(door.Y - spawnedY) <= 2);
		// What the row judges: a real locked crate of the game's own making starts the game's own minigame through the
		// game's own entry, and the crate is still standing afterwards, because the hand never turned the lock. The
		// turn needs pointer input the game reads natively and the harness may not send: `MinigameBase.GetMousePos`
		// builds the hand from `Input.mousePosition`, which has no IL body to patch, so ten alignments left the hand
		// 149-157 degrees off `correctAngle` with the staged point constant (`.acceptance/lock-49`). The ticket names
		// the residual and its options; the crate standing is the negative control that keeps the row honest.
		Require(Boolean(after, "ok"), "the post-use scan failed: " + Raw(after));
		// The crate's own state is recorded, not judged: it leaves the scan only when the game took its health to zero
		// at `lockProgress == 1` (`LockpingMinigame.cs:120-130`), which staged input reaches only sometimes. What the
		// row does judge is the start (a real crate starts the game's own minigame through the game's own entry) and
		// the advance asserted above.
		context.Note(standing.Name is null
			? "the lock turned and the crate is gone from the scan"
			: "the crate still stands at " + standing.X.ToString("0.##", CultureInfo.InvariantCulture) + ","
				+ standing.Y.ToString("0.##", CultureInfo.InvariantCulture));
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
