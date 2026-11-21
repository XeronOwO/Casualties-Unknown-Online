using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Batch 1's CPR controls row: the operator's own panel carries both channels (compressions and
/// ventilation) for a patient in arrest, each press is the player's click, and the patient's own
/// client shows the outcome — pressure built by the compressions until the heart restarts, oxygen
/// added by the breaths.
///
/// The chain, read before the fixture: the controls are the clones
/// <c>CprButtonCoordinator</c> places beside the wound view's own <c>specialUseButton</c>
/// (<c>src/CasualtiesUnknownOnline.GameAdapter/Character/CprButtonCoordinator.cs</c>); their state
/// comes from <c>CprButtonPolicy</c> (Hidden / Start / Compress, and Start only while the patient's
/// <c>Body.inCardiacArrest</c> is true, i.e. <c>heartRate &lt; 20</c>); a press enters the operation
/// as that operation's input and the host applies it through <c>CprApplier</c>, which reads the
/// game's own anchors (<c>CprRhythmPolicy.cs:28-47</c>: six pressure per well-timed compression,
/// eight oxygen per breath, the restart at 45 pressure, the arrest line at 20 heart rate).
///
/// The arrest and the low oxygen are staged through <c>body-force</c> (a setup substitution); the
/// presses, the session start and the restart are the real ones. The row does not assert a human
/// rhythm: the suite's presses are driver round-trips apart, so the verdict is not the subject —
/// the controls, the two channels and the patient's own values are.
/// </summary>
internal sealed class MedicalCprCase : AcceptanceCase
{
	public override string Name => "medical-cpr";

	public override CaseCost Cost => CaseCost.NewWorld;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		// The patient is put in the state the patient-side gate asks for: an arrested heart with
		// almost no pressure, and oxygen low enough that two breaths cannot reach the game's cap.
		var arrest = await context.MemberRecipeAsync("body-force", cancellationToken, BodyForce(-1f, -1f, 0f, 5f, 20f)).ConfigureAwait(false);
		context.Read("patient-in-arrest", arrest);
		Require(Number(arrest, "heartRate") < 20f, "the patient did not enter cardiac arrest: " + Raw(arrest));

		var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", memberId), ("name", string.Empty)).ConfigureAwait(false);
		context.Read("medical-open", opened);
		Require(Boolean(opened, "opened"), "the member's medical view did not open on the host: " + Raw(opened));

		// The control is created on the first frame the panel is maintained with a focus open
		// (CprButtonCoordinator.Update), so it is polled rather than read once.
		var compress = await WaitForControlAsync(context, "compress", true, cancellationToken).ConfigureAwait(false);
		context.Read("compress-control-before-start", compress);
		Require(Boolean(compress, "found") && Boolean(compress, "active") && Boolean(compress, "interactable"),
			"the compression control is not usable before a session: " + Raw(compress));

		var started = await context.RecipeAsync("cpr-press", cancellationToken, ("mode", "press"), ("control", "compress"), ("count", "1"), ("interval", "0")).ConfigureAwait(false);
		context.Read("compress-start-press", started);
		Require(Number(started, "pressed") >= 1f, "the start press did not reach the compression control: " + Raw(started));

		// The ventilation control is the second channel and only exists while the session runs.
		var breath = await WaitForControlAsync(context, "breath", true, cancellationToken).ConfigureAwait(false);
		context.Read("breath-control-after-start", breath);
		Require(Boolean(breath, "found") && Boolean(breath, "active") && Boolean(breath, "interactable"),
			"the ventilation control did not appear with the running session: " + Raw(breath));

		// Two breaths move the patient's own oxygen; the patient's client is the judge.
		var oxygenBefore = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
		var oxygenStart = Number(oxygenBefore, "bloodOxygen");
		for (var index = 0; index < 2; index++)
		{
			await context.RecipeAsync("cpr-press", cancellationToken, ("mode", "press"), ("control", "breath"), ("count", "1"), ("interval", "0")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken).ConfigureAwait(false);
		}

		var oxygenAfter = oxygenStart;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var read = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
			oxygenAfter = Number(read, "bloodOxygen");
			if (oxygenAfter > oxygenStart + 1f)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("patient-oxygen", JsonSerializer.SerializeToElement(new { before = oxygenStart, after = oxygenAfter }));
		Require(oxygenAfter > oxygenStart + 1f,
			"the breaths did not raise the patient's oxygen: " + oxygenStart.ToString("0.###", CultureInfo.InvariantCulture) +
			" -> " + oxygenAfter.ToString("0.###", CultureInfo.InvariantCulture));

		// Enough well-spaced compressions build the pressure past the restore threshold (6 per press
		// from 5, restart at 45: seven presses would do it; the cap allows twelve).
		var pressure = 5f;
		var heartRate = 0f;
		var pressed = 0;
		for (var attempt = 0; attempt < 14 && pressure < 45f; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await context.RecipeAsync("cpr-press", cancellationToken, ("mode", "press"), ("control", "compress"), ("count", "1"), ("interval", "0")).ConfigureAwait(false);
			pressed++;
			await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

			var read = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
			pressure = Number(read, "bloodPressure");
			heartRate = Number(read, "heartRate");
		}

		context.Read("patient-after-compressions", JsonSerializer.SerializeToElement(new
		{
			presses = pressed,
			heartRate,
			bloodPressure = pressure,
			restarted = heartRate >= 20f && pressure >= 45f,
		}));
		Require(heartRate >= 20f && pressure >= 45f,
			"the compressions did not restart the patient's heart after " + pressed.ToString(CultureInfo.InvariantCulture) +
			" press(es): heartRate " + heartRate.ToString("0.###", CultureInfo.InvariantCulture) +
			", bloodPressure " + pressure.ToString("0.###", CultureInfo.InvariantCulture));
	}

	/// <summary>The local body's own values on the patient's client (the CPR effect is applied there).</summary>
	private static Task<JsonElement?> ReadBodyAsync(CaseContext context, CancellationToken cancellationToken) =>
		context.MemberRecipeAsync("body-force", cancellationToken, BodyForce(-1f, -1f, -1f, -1f, -1f));

	/// <summary>
	/// The panel's CPR control, read until it satisfies the state asked for: the coordinator creates the
	/// clone on the first frame the remote focus is open and hides it while the policy says Hidden, so a
	/// read taken in the same frame as <c>medical-open</c> can precede both.
	/// </summary>
	private static async Task<JsonElement?> WaitForControlAsync(CaseContext context, string control, bool wantPresent, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await context.RecipeAsync("cpr-press", cancellationToken, ("mode", "read"), ("control", control), ("count", "1"), ("interval", "0")).ConfigureAwait(false);
			var present = Boolean(last, "found") && Boolean(last, "active") && Boolean(last, "interactable");
			if (present == wantPresent)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>The eleven <c>body-force</c> arguments, with the untouched ones negative.</summary>
	private static (string Key, string Value)[] BodyForce(float sleep, float consciousness, float heartRate, float bloodPressure, float bloodOxygen) =>
	[
		("sleep", Value(sleep)),
		("consciousness", Value(consciousness)),
		("energy", "-1"),
		("brainHealth", "-1"),
		("badSleepAmount", "-1"),
		("idleTime", "-1"),
		("intelligence", "-1"),
		("heartRate", Value(heartRate)),
		("bloodPressure", Value(bloodPressure)),
		("bloodOxygen", Value(bloodOxygen)),
		("strength", "-1"),
	];

	private static string Value(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	/// <summary>The host's roster names the one member; the roster is what its own commands resolve a member through.</summary>
	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var wait = 0; wait < 20; wait++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length == 1 && members[0].Length > 0)
			{
				return members[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		var final = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(false, "the host's roster did not name exactly one member: " + string.Join(", ", final));
		return string.Empty;
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static float Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Number
			? (float)propertyValue.GetDouble()
			: float.NaN;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
