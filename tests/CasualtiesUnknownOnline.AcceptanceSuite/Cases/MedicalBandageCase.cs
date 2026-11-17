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
/// by the committed <c>bandage-rub6</c> declaration, and the limb read back with <c>limb-read</c>.
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
		// `Limb.ImpactDamage`'s thresholds (`force > jumpSpeed * 2.6` before it writes bleed, native
		// Limb.cs:442-447), so it is well above a body's jump speed.
		var seeded = await context.MemberRecipeAsync(
			"wound-seed",
			cancellationToken,
			("limb", "1"),
			("force", "300"),
			("infection", "0"))
			.ConfigureAwait(false);
		context.Read("wound-seed", seeded);
		Require(Boolean(seeded, "ok"), "the game's damage entry did not wound the limb: " + Raw(seeded));

		var wounded = await ReadLimbAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("limb before the bandage", wounded);
		var bleedBefore = Number(wounded, "bleedAmount");
		Require(bleedBefore > 0.001, "the seeded wound reads no bleed, so the treatment has nothing to stop: " + Raw(wounded));

		var provided = await context.MemberRecipeAsync(
			"item-provide",
			cancellationToken,
			("type", "bandage"),
			("mode", "create"))
			.ConfigureAwait(false);
		context.Read("bandage provided", provided);
		Require(Boolean(provided, "ok"), "the member could not take a bandage: " + Raw(provided));

		var selected = await context.MemberRecipeAsync("select-limb", cancellationToken, ("limb", "1")).ConfigureAwait(false);
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

		var rub = await DriveMinigameAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("bandage minigame", rub);
		Require(Boolean(rub, "ran"), "the bandage minigame never appeared after the game accepted the item: " + Raw(rub));

		// The minigame advances while the game runs; read the patient's limb until the bleed stops.
		var after = wounded;
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(40);
		while (DateTime.UtcNow < deadline)
		{
			after = await ReadLimbAsync(context, cancellationToken).ConfigureAwait(false);
			if (Number(after, "bleedAmount") <= 0.001)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("limb after the bandage", after);
		var bleedAfter = Number(after, "bleedAmount");
		Require(
			bleedAfter <= 0.001,
			"the bandage did not stop the limb's bleed: before " + bleedBefore.ToString("F3", CultureInfo.InvariantCulture) + ", after " + bleedAfter.ToString("F3", CultureInfo.InvariantCulture) + " — " + Raw(after));
	}

	/// <summary>
	/// Drives the minigame one frame per driver round trip. The in-client declaration channel is broken on
	/// this build (a sent type never appears in the evaluator, measured 2026-11-18 on <c>bandage-rub6</c>
	/// and <c>run-ending-a</c>), so the gesture is staged by a recipe that the game itself compiles into
	/// the batch channel; each call lands one game frame, which is what <c>PhysicsUpdate</c> measures.
	/// </summary>
	private static async Task<JsonElement> DriveMinigameAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var appeared = false;
		var actions = 0d;
		for (var step = 0; step < 160; step++)
		{
			var reading = await context.MemberRecipeAsync("bandage-step", cancellationToken, ("degrees", "6")).ConfigureAwait(false);
			var stage = Text(reading, "stage");
			if (string.Equals(stage, "driving", StringComparison.Ordinal))
			{
				appeared = true;
				actions = Number(reading, "actions");
			}
			else if (appeared)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken).ConfigureAwait(false);
		}

		return JsonSerializer.SerializeToElement(new { ran = appeared, actions });
	}

	private static Task<JsonElement?> ReadLimbAsync(CaseContext context, CancellationToken cancellationToken)
	{
		return context.MemberRecipeAsync(
			"limb-read",
			cancellationToken,
			("source", "local"),
			("limb", "1"),
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

	private static string Text(JsonElement? reading, string property)
	{
		return reading is { ValueKind: JsonValueKind.Object } value
			&& value.TryGetProperty(property, out var field)
			&& field.ValueKind == JsonValueKind.String
				? field.GetString() ?? string.Empty
				: string.Empty;
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
