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
				("step", "0")).ConfigureAwait(false);
			context.Read("hand parked on the roll", park);
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
			var drive = await context.MemberRecipeAsync(
				"bandage-hand",
				cancellationToken,
				("mode", "start"),
				("radius", "250"),
				("step", "15")).ConfigureAwait(false);
			context.Read("hand drive started", drive);
			Require(
				Boolean(drive, "ok"),
				"the hand drive could not start: " + Raw(drive) + "; ring radius on screen " + radius.ToString("0.#", CultureInfo.InvariantCulture));

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
