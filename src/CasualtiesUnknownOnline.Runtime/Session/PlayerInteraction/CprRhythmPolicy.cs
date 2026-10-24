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

	/// <summary>
	/// The stamina one compression costs its operator, on the game's own 0-100 stamina scale. The reference charges stamina
	/// per press; this is CUO's own number, sized so a full bar covers a realistic resuscitation attempt (about twenty
	/// presses at the band's rate before the operator needs to breathe).
	/// </summary>
	public const float StaminaPerPress = 2.5f;

	/// <summary>
	/// The body temperature one compression costs its operator, in degrees: CPR is physical work and the reference charges a
	/// little heat per press. Small on purpose — it accumulates over a long attempt, it does not matter for one press.
	/// </summary>
	public const float TemperaturePerPress = 0.02f;

	/// <summary>
	/// The stamina floor a compression needs to be effective: at zero stamina the press is exhausted muscle and buys no
	/// perfusion, which is the incentive the reference's stamina cost creates.
	/// </summary>
	public const float MinimumStamina = 1f;

	/// <summary>The best interval between compressions in seconds (about 110 presses a minute).</summary>
	public const float IdealInterval = 0.55f;

	/// <summary>
	/// The slow edge of the rhythm band: 65 compressions a minute, the reference implementation's own floor. A press later
	/// than this adds nothing — circulation has fallen between the presses, so the effort buys no perfusion.
	/// </summary>
	public const float SlowestInterval = 60f / 65f;

	/// <summary>
	/// The fast edge of the rhythm band: 220 compressions a minute. A press faster than this is a shove — the chest never
	/// recoils, so it earns half AND breaks the patient's rib (<see cref="IsAnOverPress"/>).
	/// </summary>
	public const float FastestInterval = 60f / 220f;

	/// <summary>The pressure a compression may not lift past while the body is still in arrest (above it the chest is working).</summary>
	public const float CompressionCeiling = FibrillationPressure;

	/// <summary>
	/// How much pressure one compression adds for an interval, on the reference's own rhythm band (65–220 a minute, the
	/// user's 2026-10-24 ruling to follow it): full <see cref="CompressionGain"/> inside the band, half below the fast edge
	/// (a shove, which also costs a rib), nothing past the slow edge, and nothing once the chest is at this patient's own
	/// ceiling.
	/// </summary>
	public static float PressureGain(float intervalSeconds, float currentPressure, float pressureCeiling)
	{
		if (intervalSeconds <= 0f || intervalSeconds > SlowestInterval || currentPressure >= pressureCeiling)
		{
			return 0f;
		}

		return CompressionGain * CadenceFactor(intervalSeconds);
	}

	/// <summary>
	/// How much oxygen one ventilation adds: the same band shape, and nothing at all when the patient's oxygen is at its
	/// own ceiling (a chest that cannot exchange air gains nothing from more air).
	/// </summary>
	public static float OxygenGain(float intervalSeconds, float currentOxygen, float oxygenCeiling)
	{
		if (intervalSeconds <= 0f || intervalSeconds > SlowestInterval || currentOxygen >= oxygenCeiling)
		{
			return 0f;
		}

		return VentilationGain * CadenceFactor(intervalSeconds);
	}

	/// <summary>
	/// The band's own share of a press: a full one inside 65–220 a minute, half below the fast edge. The reference scores
	/// the RATE, so this is a band rather than a slope — with the slow half of the range (65 to about 110 a minute) already
	/// scoring full, which is why the ideal interval sits inside it instead of at its edge.
	/// </summary>
	public static float CadenceFactor(float intervalSeconds) =>
		intervalSeconds < FastestInterval ? 0.5f : 1f;

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
	/// The oxygen ceiling this patient's own body allows: the game's circulation rule clamps oxygen at
	/// `100 - hemothorax * 0.3` (`Body.HandleCirculation`'s own chest-wound cap), so a ventilation cannot promise more
	/// than an open chest would let the blood carry.
	/// </summary>
	public static float OxygenCeilingFor(float hemothoraxProgress) => Math.Max(0f, 100f - (hemothoraxProgress * 0.3f));

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
	/// The verdict for one press, in the same terms the band already scores it: inside the band is Good, below the fast edge
	/// is TooFast (a shove), past the slow edge is TooSlow (nothing). The operator's screen reads this, which is why it is
	/// derived from the SAME two edges rather than from a second set of numbers.
	/// </summary>
	public static CprRhythmVerdict JudgePress(float intervalSeconds)
	{
		if (intervalSeconds <= 0f)
		{
			return CprRhythmVerdict.TooFast;
		}

		if (intervalSeconds < FastestInterval)
		{
			return CprRhythmVerdict.TooFast;
		}

		return intervalSeconds > SlowestInterval ? CprRhythmVerdict.TooSlow : CprRhythmVerdict.Good;
	}

	/// <summary>
	/// The intelligence the reference sets for a clear screen (`SMART_INT_REQUIREMENT = 12`): an operator below it keeps the
	/// miss feedback and loses the affirmation and the detailed guide, which is its "harder screen".
	/// </summary>
	public const float SmartIntRequirement = 12f;

	/// <summary>
	/// Whether this verdict is shown to this operator: a low-intelligence operator never sees <see cref="CprRhythmVerdict.Good"/>
	/// (the reference suppresses exactly that one branch), and everyone sees the two misses.
	/// </summary>
	public static bool ShowsTheVerdict(CprRhythmVerdict verdict, float intelligence) =>
		verdict != CprRhythmVerdict.None
		&& (verdict != CprRhythmVerdict.Good || intelligence >= SmartIntRequirement);

	/// <summary>
	/// Whether this press was fast enough to hurt: faster than the band's own fast edge (220 a minute) the chest gets no
	/// time to recoil, so the press is a shove — half the gain AND the patient's rib (the applier breaks the chest limb).
	/// The edge is the reference band's, not a separate invented line.
	/// </summary>
	public static bool IsAnOverPress(float intervalSeconds) =>
		intervalSeconds > 0f && intervalSeconds < FastestInterval;

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
