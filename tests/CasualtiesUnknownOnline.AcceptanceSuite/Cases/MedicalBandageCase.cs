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
/// It runs by name: the item's minigame advances over frames, so the row takes as long as the game
/// needs, and the shared batch's wall time has a recorded baseline.
/// </para>
/// </summary>
internal sealed class MedicalBandageCase : AcceptanceCase
{
	public override string Name => "medical-bandage";

	public override CaseCost Cost => CaseCost.InRun;

	public override bool RunsInBatch => false;

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
		var screen = await context.MemberRecipeAsync("screen-size", cancellationToken).ConfigureAwait(false);
		context.Read("screen", screen);
		var centerX = Number(screen, "centerX");
		var centerY = Number(screen, "centerY");
		Require(!double.IsNaN(centerX) && !double.IsNaN(centerY), "the client did not report its screen size: " + Raw(screen));

		var pressed = await context.MemberRecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "press-key"),
			("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
			("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
			("button", "0"))
			.ConfigureAwait(false);
		context.Read("attack key pressed", pressed);
		Require(Boolean(pressed, "ok"), "the attack key could not be staged: " + Raw(pressed));

		var after = wounded;
		try
		{
			var circle = await context.MemberRecipeAsync(
				"pointer-circle",
				cancellationToken,
				("mode", "start"),
				("cx", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
				("cy", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
				("radius", "320"),
				("deg", "8"),
				("frames", "900"))
				.ConfigureAwait(false);
			context.Read("pointer circle", circle);
			Require(Boolean(circle, "ok"), "the pointer circle did not start: " + Raw(circle));

			// The game performs one action per ~20 degrees and the minigame ends on its own; read the limb
			// until the bleed stops, bounded by the measured treatment time rather than a fixed sleep.
			// The press is REPEATED inside the loop: the game's started edge is a single-frame read, so one
			// staged press can land after the minigame's own Update and never arm it (measured 2026-11-18:
			// one press left `angle 0` for 40 s, a press every 400 ms advanced `lastActionAngle` 20 → 40).
			var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(40);
			while (DateTime.UtcNow < deadline)
			{
				await context.MemberRecipeAsync(
					"virtual-input",
					cancellationToken,
					("mode", "press-key"),
					("x", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
					("y", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
					("button", "0")).ConfigureAwait(false);
				after = await ReadLimbAsync(context, limb, cancellationToken).ConfigureAwait(false);
				if (Number(after, "bleedAmount") <= 0.001)
				{
					break;
				}

				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			await context.MemberRecipeAsync(
				"pointer-circle",
				cancellationToken,
				("mode", "stop"),
				("cx", centerX.ToString("0.###", CultureInfo.InvariantCulture)),
				("cy", centerY.ToString("0.###", CultureInfo.InvariantCulture)),
				("radius", "320"),
				("deg", "8"),
				("frames", "900")).ConfigureAwait(false);
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
			"the bandage did not stop the limb's bleed: before " + bleedBefore.ToString("F3", CultureInfo.InvariantCulture) + ", after " + bleedAfter.ToString("F3", CultureInfo.InvariantCulture) + " — " + Raw(after));
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

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
