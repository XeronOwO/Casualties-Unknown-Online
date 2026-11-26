using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The bandage treatment, read on the patient's own client.
/// <para>
/// The sweep's batch 1 lists the bandage/tourniquet/splint treatments as not yet read. This row takes
/// the bandage: a real wound through the game's own damage entry (<c>Limb.ImpactDamage</c> behind the
/// <c>wound-seed</c> recipe), the bandage started through the game's own wound-view entry
/// (<c>PlayerCamera.ApplyWoundItem</c>, the <c>limb-use</c> recipe), the item's own minigame completed
/// by the staged pointer and attack key the game reads (<c>virtual-input</c> plus <c>pointer-circle</c>),
/// and the limb read back with <c>limb-read</c>.
/// </para>
/// <para>
/// The item's minigame advances over frames, so the row takes as long as the game needs; it restores
/// nothing, so the run pays only that time for it.
/// </para>
/// </summary>
internal sealed class MedicalBandageCase : AcceptanceCase
{
	public override string Name => "medical-bandage";

	/// <summary>The wound the row seeds and the bandage it applies.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Body | CaseLeftovers.Bag | CaseLeftovers.Hands;

	public override CaseCost Cost => CaseCost.InRun;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var hostState = await context.Session
			.WaitUntilAsync(
				context.Session.HostUrl,
				state => SessionDriver.StateTrue(state, "inWorld") && !SessionDriver.StateTrue(state, "gateWaiting"),
				TimeSpan.FromSeconds(60),
				cancellationToken)
			.ConfigureAwait(false);
		Require(SessionDriver.StateTrue(hostState, "inWorld"), "the host is not in a world");

		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
		}

		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberState = await context.Session
			.WaitUntilAsync(
				context.Session.MemberUrl,
				state => SessionDriver.StateTrue(state, "inWorld"),
				TimeSpan.FromSeconds(30),
				cancellationToken)
			.ConfigureAwait(false);
		Require(SessionDriver.StateTrue(memberState, "inWorld"), "the member is not in the world to be bandaged");

		// A real wound: the game's own damage entry writes the limb's bleed fields. The force has to clear
		// The limb layout belongs to the body, and a destroyed or vital limb cannot carry the row: the head is
		// index 0 on the measured body, a previous run's body put the torso at another index, and a limb already
		// at 0 skin health cannot bleed. So the row READS the body and wounds the first healthy, non-vital limb;
		// the search is bounded to eight, which is more limbs than any measured body has.
		var limb = string.Empty;
		JsonElement? wounded = null;
		var bleedBefore = 0d;
		for (var candidate = 0; candidate < 8 && limb.Length == 0; candidate++)
		{
			var index = candidate.ToString(CultureInfo.InvariantCulture);
			var reading = await ReadLimbAsync(context, index, cancellationToken).ConfigureAwait(false);
			if (!Boolean(reading, "ok"))
			{
				continue;
			}

			if (Boolean(reading, "isHead") || Boolean(reading, "isVital") || Boolean(reading, "dismembered"))
			{
				continue;
			}

			if (Number(reading, "skinHealth") < 50)
			{
				continue;
			}

			// `Limb.ImpactDamage` writes bleed only above its force threshold (`force > jumpSpeed * 2.6`,
			// native Limb.cs:442-447) and subtracts `force * 0.33` from the limb's skin; 200 clears the
			// threshold while leaving skin health, where the measured 300 destroyed the limb (skin -12.2).
			var seeded = await context.MemberRecipeAsync(
				"wound-seed",
				cancellationToken,
				("limb", index),
				("force", "200"),
				("infection", "0"))
				.ConfigureAwait(false);
			context.Read("wound-seed on limb " + index, seeded);
			if (!Boolean(seeded, "ok"))
			{
				continue;
			}

			var afterSeed = await ReadLimbAsync(context, index, cancellationToken).ConfigureAwait(false);
			var bleed = Number(afterSeed, "bleedAmount");
			// A destroyed limb (skin 0) cannot be bandaged back: the treatment row needs a wound that still
			// has a limb. `ImpactDamage`'s bleed roll can also come back at zero, so the next limb is tried.
			if (!(bleed > 0.001) || Number(afterSeed, "skinHealth") <= 0.5)
			{
				continue;
			}

			limb = index;
			wounded = afterSeed;
			bleedBefore = bleed;
		}

		context.Read("limb before the bandage", wounded);
		Require(limb.Length > 0, "no limb of the member's body took a bleeding wound, so the treatment has nothing to stop");
		Require(bleedBefore > 0.001, "the seeded wound reads no bleed, so the treatment has nothing to stop: " + Raw(wounded));

		var provided = await context.MemberRecipeAsync(
			"item-provide",
			cancellationToken,
			("type", "bandage"),
			("mode", "create"))
			.ConfigureAwait(false);
		context.Read("bandage provided", provided);
		Require(Boolean(provided, "ok"), "the member could not take a bandage: " + Raw(provided));

		var selected = await context.MemberRecipeAsync("select-limb", cancellationToken, ("limb", limb)).ConfigureAwait(false);
		context.Read("select-limb", selected);
		Require(Boolean(selected, "ok"), "the wound view did not select the wounded limb: " + Raw(selected));

		// Our own window keeps the pointer: with it up, the staged press lands on the panel above the minigame
		// and the roll never turns (measured 2026-11-22 — eighteen attempts, all from the same cause). The row
		// takes the window down before the minigame opens; the shot of the running gesture is what proves the
		// game, not the panel, is receiving it.
		var closed = await context.Session.CloseWindowAsync(context.Session.MemberUrl, cancellationToken).ConfigureAwait(false);
		context.Read("member window closed", closed);
		Require(
			!Boolean(closed, "visible"),
			"the member's Online UI window is still up, so the gesture would press it instead of the minigame: " + Raw(closed));

		var started = await context.MemberRecipeAsync(
			"limb-use",
			cancellationToken,
			("type", "bandage"),
			("mode", "use"))
			.ConfigureAwait(false);
		context.Read("limb-use bandage", started);
		Require(Boolean(started, "ok"), "the game refused the bandage on the selected limb: " + Raw(started));
		Require(
			Boolean(started, "actuallyUsable"),
			"the bandage reports it is not usable on this limb, so the row would measure a refusal: " + Raw(started));

		// The minigame reads the GAME's own input: the pointer circles the roll while the attack key is held.
		// The 2026-11-18 reading drove the minigame's fields directly and got 1200 driven frames with zero
		// game actions, because `BandageMinigame.Update` sets `holdingBandage` only on the frame the game's
		// own click-start edge arrives; the staged input below is the path the game actually reads.
		//
		// The circle has to be centred on the MINIGAME, not on the screen: `BandageMinigame` reads the hand
		// as `(pointer - screenPos) / uiScale` and the roll orbits at `Lerp(253, 390, condition)` from that
		// centre (BandageMinigame.cs:90-94). A screen-centred circle put the hand at 183 degrees when the
		// roll was at 0 (measured 2026-11-21: `circle at 8 degrees`, `handPos -535.979,-31.631`), so the
		// press could never arm the wrap.
		var live = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
		context.Read("minigame before the gesture", live);
		// The roll's own window is about twenty-five degrees wide (`BandageMinigame.cs:77-84`), and the game stamps
		// `holdingBandage` only when the hand sits inside it on the frame the attack key goes down. The hand has to be STEPPED
		// onto the roll, not merely parked on the ring: `.acceptance/band-22` reported `angle:0, handAngle:75, handMagnitude:250`
		// for the whole gesture, so the hand was on the ring 75 degrees away from the window and the press could never arm it.
		var stepToTheRoll = Number(live, "angle") - Number(live, "handAngle");
		Require(Boolean(live, "hasMain") && Text(live, "kind").Length > 0, "the bandage minigame is not open: " + Raw(live));
		var scale = Number(live, "uiScale");
		Require(scale > 0, "the minigame reported no uiScale, so the gesture cannot be mapped to the screen: " + Raw(live));
		var centre = Text(live, "screenPos").Split(',');
		Require(centre.Length == 2, "the minigame reported no screen position: " + Raw(live));
		var centerX = double.Parse(centre[0], CultureInfo.InvariantCulture);
		var centerY = double.Parse(centre[1], CultureInfo.InvariantCulture);
		var radius = 300d * scale;


		var after = wounded;
		JsonElement? minigameAtPress = null;
		try
		{
			// `holdingBandage` is stamped ONLY on the frame the game sees the attack key go down, and only
			// when the hand's direction is within about 25 degrees of the roll at that instant
			// (BandageMinigame.cs:74-80); afterwards it follows the key until the key goes up. Measured
			// 2026-11-21 with the recipe's own `holding` field: a press staged while the hand sat at 152
			// degrees read `holding: false` for the whole run while `handClicking` stayed true, and the roll
			// never turned. So the hand is driven around the ring first (`pointer-circle`, one small step a
			// frame — a frame that walks more than 20 degrees past the roll latches it), and the key goes
			// down on the pass through the roll's own window, which starts at 0 degrees.
			// The patches have to answer `Input.mousePosition` before the circle starts: `pointer-circle`
			// only stages a position, and without the patches the game reads the real cursor — which sits
			// outside the minigame, so the hand runs off the ring (measured 2026-11-21: `pointerX -160,
			// pointerY -491` and `handMagnitude 741` while the circle claimed a centre of 640,360).
			await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "install"),
				("x", "0"),
				("y", "0"),
				("button", "0")).ConfigureAwait(false);
			await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "release-key"),
				("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
				("button", "0")).ConfigureAwait(false);
			// What the input patches actually replaced. `virtual-input mode=report` answers the staged stores, not the call-site
			// replacement, so a hand that never starts had no readable cause (measured 2026-11-26: neither the row's report nor
			// the game log carried a single installation line). `click-state` carries `VirtualInput.InstallReport`, which names
			// every replaced site, including `MinigameBase.UpdateHandVisuals`.
			context.Read(
				"the input patch report",
				await context.MemberRecipeAsync("click-state", cancellationToken).ConfigureAwait(false));
			// The hand has to be on the roll before the key goes down: the game stamps `holdingBandage` only on
			// the click-start frame, and only when the hand is within about twenty-five degrees of the roll
			// (`BandageMinigame.cs:77-84`). So the drive first parks the hand at the roll, the key goes down
			// there, and only then does the hand start walking the ring (measured 2026-11-23: pressing first
			// read `holding: false` for the whole 26 s gesture).
			var park = await context.MemberRecipeAsync(
				"bandage-hand",
				cancellationToken,
				("mode", "start"),
				("radius", "250"),
				("step", "5")).ConfigureAwait(false);
			context.Read("hand parked on the roll", park);
			// Walk the hand INTO the roll's window before the press: `step` is the recipe's per-frame increment (not the angle to
			// arrive at), so a delta of -75 stepped the hand past the window and round to 269 degrees (measured 2026-11-26,
			// band-23). Six degrees a frame reaches the window from 75 in about a dozen frames.
			for (var attempt = 0; attempt < 30; attempt++)
			{
				var now = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
				if (Math.Abs(Number(now, "handAngle") - Number(now, "angle")) <= 20)
				{
					break;
				}

				await Task.Delay(60, cancellationToken).ConfigureAwait(false);
			}
			// The wrap's hold is a KEY bind, not a mouse button: MinigameBase stamps handStartedClicking from
			// Input.GetKeyDown(KeyBinds.GetBind("attack")) and the staged MOUSE never reaches it (2026-11-26: holding
			// stayed false for the whole gesture while a staged mouse press held the pointer).
			context.Read(
			"attack held",
			await context.MemberRecipeAsync(
			"stage-bind",
			cancellationToken,
			("bind", "attack"),
			("action", "down")).ConfigureAwait(false));
			await Task.Delay(300, cancellationToken).ConfigureAwait(false);
			await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "press"),
				("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
				("button", "0")).ConfigureAwait(false);
			await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "press-key"),
				("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
				("button", "0")).ConfigureAwait(false);
			await Task.Delay(150, cancellationToken).ConfigureAwait(false);
			// Which replaced call site took the staged edge, and in which frame: `keyDownAnswers` moves without the hand's own
			// `UpdateHandVisuals` ever seeing it (measured 2026-11-26, band-21), so the trail names the frame of every DOWN probe.
			context.Read(
				"the down probes",
				await context.MemberRecipeAsync("down-trail", cancellationToken).ConfigureAwait(false));
			var drive = await context.MemberRecipeAsync(
				"bandage-hand",
				cancellationToken,
				("mode", "start"),
				("radius", "250"),
				("step", "3")).ConfigureAwait(false);
			context.Read("hand drive started", drive);
			Require(
				Boolean(drive, "ok"),
				"the hand drive could not start: " + Raw(drive) + "; ring radius on screen " + radius.ToString("0.#", CultureInfo.InvariantCulture));

			// The game reads its own start edge inside the minigame (`MinigameBase.cs:167` asks
			// `Input.GetKeyDown(KeyBinds.GetBind("attack"))`), and the press that opened the wrap happened before
			// the wrap existed, so no edge was there to read. The hand now orbits the roll: release and press
			// again so the edge lands where the game is looking (measured 2026-11-26 05:45: the press answers
			// `keyDown:false` in its own frame because `VirtualInput.PressKey` shows the edge on the next frame).
			// The press has to land while the hand is already on the ring: the drive places the hand inside the physics
			// stage, so its first placement can arrive a frame or more after `start`. A fixed wait pressed too early in a
			// slice and the hand was still at its first position, 75 degrees off the roll, where the wrap's own dot
			// product is 0.26 against its > 0.9 window and the start never takes (measured 2026-11-26 08:05: `band-36`
			// alone walks `roll75hand90h1`, `park-3` in a slice stays `roll0hand345h0` for the whole gesture).
			var standby = DateTime.UtcNow + TimeSpan.FromSeconds(5);
			while (DateTime.UtcNow < standby)
			{
				var placed = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
				if (Raw(placed).Contains("pre;", StringComparison.Ordinal))
				{
					break;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken).ConfigureAwait(false);
			}

			// The start edge has to land on the frame the hand is inside the roll's window. The old fixed pair pressed once
			// and hoped the hand was there; bandfix-1 and bandfix-2 (2026-11-27 04:06) both read `holding:false` for the
			// whole gesture while the drive walked the ring — the press had landed with the hand sixty degrees away, so
			// `MinigameBase.Update` computed the dot product as 0.5 against its > 0.9 window. Poll the roll and press only
			// on a pass through the window, and retry while the edge does not take.
			var pressDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
			var pressed = false;
			while (DateTime.UtcNow < pressDeadline && !pressed)
			{
				var pass = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
				// `handAngle` is the visual hand after `UpdateHandVisuals` pulled it toward the staged pointer; the hand the
				// drive places on the physics pass is `driveAngle`. Three readings kept the press from ever landing:
				// slice-16 (gap 3 on `handAngle`) pressed while the drive's trace held the hand at 63 degrees (dot 0.45),
				// and slice-17 (gap on `driveAngle`) never pressed at all — the two angles are only equal on the pass the
				// game reads, so the poll keeps the twenty-degree window and lets the retry loop take the edge there.
				var gap = Math.Abs(Number(pass, "handAngle") - Number(pass, "angle"));
				if (gap <= 20)
				{
					await context.MemberRecipeAsync(
						"virtual-input",
						cancellationToken,
						("mode", "release-key"),
						("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
						("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
						("button", "0")).ConfigureAwait(false);
					await context.MemberRecipeAsync(
						"virtual-input",
						cancellationToken,
						("mode", "press-key"),
						("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
						("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
						("button", "0")).ConfigureAwait(false);
					await Task.Delay(150, cancellationToken).ConfigureAwait(false);
					var held = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
					pressed = Boolean(held, "holding") == true;
					context.Read("press through the roll's window (gap " + gap.ToString("0.#", CultureInfo.InvariantCulture) + ")", held);
				}
				else
				{
					await Task.Delay(40, cancellationToken).ConfigureAwait(false);
				}
			}

			Require(pressed, "the wrap's holding flag never took while the hand passed the roll's window.");

			// The edge only needs the hand near the roll for the one frame it lands; after that the hand can walk the
			// lap at the speed the wrap's own ratchet expects. Step 3 keeps the landing frame inside the dot window even
			// when a slice runs six physics steps per frame; step 15 then makes a wrap action every other pass.
			await context.MemberRecipeAsync(
				"bandage-hand",
				cancellationToken,
				("mode", "start"),
				("radius", "250"),
				("step", "15")).ConfigureAwait(false);

			// The gesture is a path the game integrates itself, so the row records the path: a handful of samples while
			// the client walks the lap say whether the pointer orbits, whether the hand follows it, and where the hand
			// stops relative to the roll (2026-11-22: one 20-degree step in a 21 s gesture, `angle 0.832`, fill 0.002 —
			// the numbers alone could not say which of the three stopped first).
			for (var sample = 0; sample < 8; sample++)
			{
				await Task.Delay(250, cancellationToken).ConfigureAwait(false);
				var stepped = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
				context.Read("gesture sample " + (sample + 1).ToString(CultureInfo.InvariantCulture), stepped);
			}

			// The gesture runs inside the client now — one lap per second, the attack key owned there — so the row waits
			// for the treatment it produces and nothing else. The old shape sampled the hand's angle from outside every
			// 40 ms to catch a pass, then polled for up to sixty seconds (measured 2026-11-22: 66.3 s of case time and the
			// wrap never completed).

			// bandfix-3 (2026-11-27 04:11) pressed on a pass with the hand zero degrees off the roll and read `didWrap:true`
			// only after the old eight-second window had already taken its last sample; the wrap's own eighteen actions are
			// what drives the bleed down, and the row was judging before the last of them. Wait for the limb to settle
			// instead: the game's own action count is the treatment.
			var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
			var step = 0;
			while (DateTime.UtcNow < deadline)
			{
				step++;
				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
				after = await ReadLimbAsync(context, limb, cancellationToken).ConfigureAwait(false);
				if (Number(after, "bleedAmount") <= 0.001)
				{
					break;
				}
			}

			minigameAtPress = await context.MemberRecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
			context.Read("minigame after the wrap", minigameAtPress);

			// The hand's own history: the single reading above says the hand never started, and only the frame-by-frame record
			// says whether the game's own start edge ever fired while the row held the attack (measured 2026-11-26).
			var handFrames = await context.MemberRecipeAsync("minigame-frames", cancellationToken).ConfigureAwait(false);
			var handFramesRaw = Raw(handFrames);
			var handFramesTail = handFramesRaw.Length > 1500 ? handFramesRaw.Substring(handFramesRaw.Length - 1500) : handFramesRaw;
			context.Note("the hand's own frames (tail): " + handFramesTail);
			// Whether the start edge could answer at all on this build: the install report names every patched method and
			// how many key reads it replaced, so a hand that never starts says which side is missing (measured 2026-11-26).
			var install = await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "report"),
				("x", "0"),
				("y", "0"),
				("button", "0")).ConfigureAwait(false);
			context.Note("the input the game sees: " + Raw(install));
		}
		finally
		{
			await context.MemberRecipeAsync(
				"bandage-hand",
				cancellationToken,
				("mode", "stop"),
				("radius", "250"),
				("step", "2")).ConfigureAwait(false);
			await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "release-key"),
				("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
				("button", "0")).ConfigureAwait(false);
		}

		context.Read("limb after the bandage", after);
		var bleedAfter = Number(after, "bleedAmount");
		Require(
			bleedAfter <= 0.001,
			"the bandage did not stop the limb's bleed: before " + bleedBefore.ToString("F3", CultureInfo.InvariantCulture) + ", after " + bleedAfter.ToString("F3", CultureInfo.InvariantCulture) + " — " + Raw(after) + "; minigame at the first press: " + Raw(minigameAtPress));
	}

	private static Task<JsonElement?> ReadLimbAsync(CaseContext context, string limb, CancellationToken cancellationToken)
	{
		return context.MemberRecipeAsync(
			"limb-read",
			cancellationToken,
			("source", "local"),
			("limb", limb),
			("target", "0"));
	}

	/// <summary>
	/// Waits until the staged circle is inside the roll's own window — within about 25 degrees of the
	/// roll's angle, which starts at 0 and is the direction the wrap begins from — and returns the angle
	/// the press landed on. Reading the angle before the press is the difference between arming the
	/// gesture and pressing into a frame the game ignores.
	/// </summary>
	private static async Task<double> WaitForCircleAtRollAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var last = double.NaN;
		for (var attempt = 0; attempt < 60; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var circle = await context.MemberRecipeAsync(
				"pointer-circle",
				cancellationToken,
				("mode", "read"),
				("cx", "0"),
				("cy", "0"),
				("radius", "0"),
				("deg", "0"),
				("frames", "0")).ConfigureAwait(false);
			last = Number(circle, "angle");
			if (!double.IsNaN(last) && (last <= 30d || last >= 330d))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>
	/// Puts the staged pointer on the roll's orbit at <paramref name="degrees"/>. The orbit is centred
	/// on the minigame's own window, not on the screen: `GetMousePos` subtracts
	/// `minigameScreen.transform.position` and divides by `uiScale` (MinigameBase.cs:156-160), so a
	/// circle drawn around the screen centre puts the hand 183 degrees away from a roll at 0 (measured
	/// 2026-11-21).
	/// </summary>
	private static Task<JsonElement?> MovePointerAsync(
		CaseContext context,
		double centerX,
		double centerY,
		double radius,
		double degrees,
		CancellationToken cancellationToken)
	{
		var radians = degrees * Math.PI / 180d;
		var pointerX = centerX + (radius * Math.Cos(radians));
		var pointerY = centerY + (radius * Math.Sin(radians));
		return context.MemberRecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "move"),
			("x", pointerX.ToString("0.###", CultureInfo.InvariantCulture)),
			("y", pointerY.ToString("0.###", CultureInfo.InvariantCulture)),
			("button", "0"));
	}

	private static bool Boolean(JsonElement? reading, string property)
	{
		return reading is { ValueKind: JsonValueKind.Object } value
			&& value.TryGetProperty(property, out var field)
			&& field.ValueKind == JsonValueKind.True;
	}

	private static double Number(JsonElement? reading, string property)
	{
		return reading is { ValueKind: JsonValueKind.Object } value
			&& value.TryGetProperty(property, out var field)
			&& field.ValueKind == JsonValueKind.Number
			&& field.TryGetDouble(out var number)
				? number
				: double.NaN;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Text(JsonElement? reading, string property)
	{
		return reading is { ValueKind: JsonValueKind.Object } value
			&& value.TryGetProperty(property, out var field)
			&& field.ValueKind == JsonValueKind.String
				? field.GetString() ?? ""
				: "";
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
