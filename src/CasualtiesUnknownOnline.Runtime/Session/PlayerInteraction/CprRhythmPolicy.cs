using System;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The CPR rhythm's physiology, as one pure rule (ticket `remote-medical-cpr.md`, the user's 2026-10-24 rulings):
/// compressions act on BLOOD PRESSURE, ventilation acts on BLOOD OXYGEN, and the heart restart is the single shared
/// outcome — first to the host wins, the later claimant is corrected rather than replayed.
///
/// <para>
/// The numbers are anchored on the game's own scales rather than invented: `Body.brainDying` is
/// `bloodPressure &lt; 10 &amp;&amp; consciousness &lt; 5` (the arrest CPR is for), `Body.fibrillationRising` starts at
/// `bloodPressure &lt; 88`, and a normal pressure sits around 100-120. So a compression lifts pressure toward
/// <see cref="CompressionCeiling"/> only while the body is still in arrest territory, a good cadence is worth more
/// than a fast one, and the restart needs enough perfusion to be plausible — <see cref="RestartPressure"/>, the value
/// this ticket records as the tuning point.
/// </para>
///
/// <para>
/// The ceilings the assessment named are real: a patient whose blood volume is gone or whose chest wound is open cannot
/// be perfused no matter how well the operator compresses, so the ceiling itself moves with the patient's own state
/// (<see cref="CeilingFor"/>), and a rhythm that reaches it stops helping instead of implying progress.
/// </para>
/// </summary>
public static class CprRhythmPolicy
{
	/// <summary>The minimum pressure a compression may lift a patient toward before the chest is perfused enough to matter (`brainDying`'s own line).</summary>
	public const float ArrestPressure = 10f;

	/// <summary>Where the game's own fibrillation trigger begins (`Body.fibrillationRising`): perfusing past it is real recovery.</summary>
	public const float FibrillationPressure = 88f;

	/// <summary>Perfusion needed for the heart to take over again — the restart threshold this ticket records as the tuning point.</summary>
	public const float RestartPressure = 45f;

	/// <summary>The pressure one well-timed compression lifts a body by, at the best cadence.</summary>
	public const float CompressionGain = 6f;

	/// <summary>The oxygen one ventilation adds, at the best cadence (the user's ruling: ventilation is a real oxygen intervention, not decoration).</summary>
	public const float VentilationGain = 8f;

	/// <summary>The best interval between compressions in seconds (about 110 presses a minute).</summary>
	public const float IdealInterval = 0.55f;

	/// <summary>Beyond this interval a press is too late to keep circulation going: it adds nothing.</summary>
	public const float TooSlowInterval = 1.6f;

	/// <summary>The pressure a compression may not lift past while the body is still in arrest (above it the chest is working).</summary>
	public const float CompressionCeiling = FibrillationPressure;

	/// <summary>
	/// How much pressure one compression adds for an interval: full <see cref="CompressionGain"/> near
	/// <see cref="IdealInterval"/>, the same gain scaled down when the presses are too fast (recoil is lost) or too slow
	/// (circulation falls between them), nothing at all past <see cref="TooSlowInterval"/>.
	/// </summary>
	public static float PressureGain(float intervalSeconds, float currentPressure, float pressureCeiling)
	{
		if (intervalSeconds <= 0f || intervalSeconds >= TooSlowInterval || currentPressure >= pressureCeiling)
		{
			return 0f;
		}

		// A press that arrives too fast earns half (there is no time for the chest to refill), one near the ideal
		// interval earns the full gain, and one that is late scales down linearly to nothing at the slow line.
		var cadence = intervalSeconds < IdealInterval
			? 0.5f
			: 1f - ((intervalSeconds - IdealInterval) / (TooSlowInterval - IdealInterval));

		return CompressionGain * Clamp01(cadence);
	}

	/// <summary>
	/// How much oxygen one ventilation adds: the same cadence shape, and nothing at all when the patient's oxygen is at
	/// its own ceiling (a chest that cannot exchange air gains nothing from more air).
	/// </summary>
	public static float OxygenGain(float intervalSeconds, float currentOxygen, float oxygenCeiling)
	{
		if (intervalSeconds <= 0f || intervalSeconds >= TooSlowInterval || currentOxygen >= oxygenCeiling)
		{
			return 0f;
		}

		var cadence = intervalSeconds < IdealInterval
			? 0.5f
			: 1f - ((intervalSeconds - IdealInterval) / (TooSlowInterval - IdealInterval));

		return VentilationGain * Clamp01(cadence);
	}

	/// <summary>
	/// The pressure ceiling this patient's own body allows: a body the game already marks as losing its blood or with an
	/// open chest cannot be perfused past the fibrillation line, so the ceiling is lowered by the shortfall instead of
	/// promising a pressure the physiology cannot hold.
	/// </summary>
	/// <param name="bloodVolumeFraction">The patient's `bloodVolumePercentage` — the game returns 0.5 + volume/200, a FRACTION where 1 is full.</param>
	/// <param name="hemothoraxProgress">The patient's chest-wound progress (0 = none).</param>
	public static float CeilingFor(float bloodVolumeFraction, float hemothoraxProgress)
	{
		var ceiling = CompressionCeiling;

		if (bloodVolumeFraction < 1f)
		{
			// The game's own scale is a fraction, so a body at three quarters of its volume keeps three quarters of the
			// ceiling CPR could otherwise build.
			ceiling *= Clamp01(bloodVolumeFraction);
		}

		if (hemothoraxProgress > 0f)
		{
			// An open chest vents the pressure it builds: the wound caps the ceiling at the fibrillation line's half.
			ceiling = Math.Min(ceiling, FibrillationPressure * 0.5f);
		}

		return Math.Max(ArrestPressure, ceiling);
	}

	/// <summary>
	/// The game's own blood-volume fraction for the synced `BloodVolume` field: `Body.bloodVolumePercentage` is
	/// `0.5f + bloodVolume / 200f`, so a full body (volume 100) reads 1 and an empty one reads 0.5.
	/// </summary>
	public static float BloodVolumeFraction(float bloodVolume) => 0.5f + (bloodVolume / 200f);

	/// <summary>
	/// The heart rate a restarted heart is set to: above the game's own arrest line (`inCardiacArrest` is `heartRate &lt;
	/// 20`), so the model's heart-rate and pressure block runs again and restores the pressure itself. 40 is a
	/// perfusing rate rather than a normal one — the patient is alive, not healthy.
	/// </summary>
	public const float PerfusingHeartRate = 40f;

	/// <summary>
	/// Whether this compression restarts the heart: the perfusion reached <see cref="RestartPressure"/> (or the patient's
	/// own lower ceiling when that is below it) and the rhythm is still being kept. The HOST arbitrates the first claim
	/// (`MedicalOperationClaims`'s one-resolving-unit shape) — this only answers whether THIS compression would.
	/// </summary>
	public static bool RestartsTheHeart(float pressureAfter, float pressureCeiling) =>
		pressureAfter >= Math.Min(RestartPressure, pressureCeiling);

	/// <summary>net48 has no <c>Math.Clamp</c>; the rule's own bounds are all [0, 1] fractions.</summary>
	private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
