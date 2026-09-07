using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The single remote-display projection seam for player presentation. It is
/// used by three surfaces that used to each maintain per-field ad hoc rules:
/// the remote render clone (face/body pose), the remote WoundView display-only
/// body (medical readouts), and the 1 Hz/limb-event capture that fills the wire
/// <see cref="CharacterHealthMsg"/> with the owner-side presentation facts.
///
/// The wire payload remains the canonical remote-view source; this class is the
/// only adapter-side projector that turns that source into the game-native
/// fields a remote view reads. Per-domain helpers that existed before
/// (CloneFacePresentation, CloneBodyPosePresentation, RemoteMedicalDisplayProjection)
/// have been absorbed here so the family cannot drift again.
/// </summary>
internal static class RemoteCharacterDisplayProjection
{
	// ===== Capture: owner live Body -> wire presentation fields =====

	internal static void Capture(Body body, CharacterHealthMsg health)
	{
		// Face: the owner's live mouth choice and the FacialExpression child
		// latches that Mapster cannot see.
		health.EatTime = body.eatTime;
		health.HeadMouth = HeadMouthRule.Evaluate(
			body.disfigured,
			health.EatTime,
			body.HoldingItem(2),
			body.limbs.Length > 0 && body.limbs[0].dislocated);

		var face = body.GetComponentInChildren<FacialExpression>();
		if (face != null) // Unity object — ==
		{
			health.DisfiguredIndex = face.disfiguredIndex;
			health.DisfiguredTimeFullSkin = face.disfiguredTimeFullSkin;
			health.EyeTimeHealed = face.eyeTimeHealed;
		}

		// Body pose: the owner's computed leg-speed multiplier drives the
		// weakness/slouch CrouchAmount input on a frozen render clone.
		health.LegSpeedMult = Mathf.Clamp01(body.legSpeedMult);

		// [Saveable] component state (painkillers, drinkable medicines) that
		// Mapster cannot see and that the medical display must restore.
		CharacterComponentSync.Capture(body, health);
	}

	// ===== Apply: remote render clone =====

	internal static void ApplyRenderClone(Body clone, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		clone.disfigured = health.Disfigured;
		clone.eyeGone = health.EyeGone;
		clone.bothEyesGone = health.BothEyesGone;

		// Face-driving body vitals: Body.Update is skipped on a render clone, so
		// these are written from the owner's 1 Hz snapshot. The game's own
		// FacialExpression.Update remains the sprite authority.
		ApplyFaceVitals(clone, FacePresentationVitals.From(health));

		var face = clone.GetComponentInChildren<FacialExpression>();
		if (face != null) // Unity object — ==
		{
			var count = face.disfiguredHead?.Length ?? 0;
			face.disfiguredIndex = count > 0 ? Mathf.Clamp(health.DisfiguredIndex, 0, count - 1) : 0;
			face.disfiguredTimeFullSkin = health.DisfiguredTimeFullSkin;
			face.eyeTimeHealed = health.EyeTimeHealed;
		}

		var driver = clone.GetComponent<RemoteBodyDriver>();
		if (driver != null) // Unity object — ==
		{
			driver.HeadMouth = health.HeadMouth;
			driver.LegSpeedMult = Mathf.Clamp01(health.LegSpeedMult);
		}
	}

	private static void ApplyFaceVitals(Body clone, FacePresentationVitals vitals)
	{
		clone.consciousness = vitals.Consciousness;
		clone.energy = vitals.Energy;
		clone.badSleepAmount = vitals.BadSleepAmount;
		clone.radiationSickness = vitals.RadiationSickness;
		clone.shock = vitals.Shock;
		clone.adrenaline = vitals.Adrenaline;
		clone.sicknessAmount = vitals.SicknessAmount;
		clone.temperature = vitals.Temperature;
		clone.internalBleeding = vitals.InternalBleeding;
		clone.bloodPressure = vitals.BloodPressure;
		clone.happiness = vitals.Happiness;
	}

	// ===== Apply: remote WoundView display-only body =====

	internal static void ApplyMedicalDisplay(Body body, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		// Keep the display body's component state in sync too: the native
		// MoodleManager reads Painkillers.actualOpiateReception for the
		// overdose/withdrawal row. The per-frame AdvanceMedicalDisplay re-writes
		// the projected actual after this authoritative snapshot reset.
		CharacterComponentSync.Apply(body, health);

		body.heartRate = health.HeartRate;
		body.bloodPressure = health.BloodPressure;
		body.bloodPressureReadout = $"{Mathf.RoundToInt(health.BloodPressure)}/{Mathf.RoundToInt(health.BloodPressure * 0.66f)}";

		// Breathing is recomputed by Body.Update on a live body; the inactive
		// display clone must project it from the authoritative snapshot so a
		// stopped remote breath shows the native "cannot breathe" moodle, not
		// the generic hypoventilation one.
		body.breathing = ProjectBreathing(health);

		// WoundView's respiratory line reads the private-set property that the
		// live body's circulation pass fills; project it for the inactive clone.
		Traverse.Create(body).Property("respiratoryRateReadout")
			.SetValue(ProjectRespiratoryRateReadout(health));

		// Antidepressant happiness is produced by Antidepressants.Update; the
		// component only contributes while its amount is non-zero, and the
		// currentAmount determines the live ramp.
		body.antidepressantHappiness = health.AntidepressantsAmount > 0f
			? -body.happiness * 0.6f * Mathf.Clamp01(health.AntidepressantsCurrentAmount * 0.0166f)
			: 0f;

		// Mindwipe is assigned by Body.Update's half-second pass. The display
		// clone may still carry a component from an earlier snapshot; remove it
		// when the authoritative snapshot says the state is gone.
		if (health.MindwipeScriptPresent)
		{
			body.mindWipe = body.GetComponent<MindwipeScript>();
		}
		else
		{
			body.mindWipe = null;
			var staleMindwipe = body.GetComponent<MindwipeScript>();
			if (staleMindwipe != null) // Unity object — ==
			{
				Object.Destroy(staleMindwipe);
			}
		}
	}

	internal static void AdvanceMedicalDisplay(
		Body display,
		CharacterHealthMsg health,
		float deltaTime,
		float unscaledDeltaTime)
	{
		// The display body is inactive, so the native heart progression never
		// runs; advance it here so the redirected ECG waveform is not frozen at
		// a constant progress.
		display.heartProg = AdvanceHeartProgress(
			display.heartRate,
			display.heartProg,
			unscaledDeltaTime);

		// The display body is also inactive for Painkillers.Update; run the
		// same 5-units/sec opiate-reception ramp here so the remote mood
		// advances between the committed-dose state updates. The raw actual is
		// carried in the display body's opiateHappiness (for positive opiates
		// it is identical; for negative it is invertible) so 1 Hz/state
		// component sync cannot reset the curve.
		var currentActual = ToActualReception(display.opiateHappiness);
		if (currentActual == 0f && health.ActualOpiateReception != 0f)
		{
			currentActual = health.ActualOpiateReception;
		}

		var actual = AdvanceOpiateReception(
			health,
			currentActual,
			deltaTime);
		display.opiateHappiness = OpiateHappinessFromReception(actual);

		// The native MoodleManager reads Painkillers.actualOpiateReception for
		// the overdose/withdrawal row; keep the display component's actual
		// aligned with the projected ramp after the snapshot reset.
		var painkillers = display.GetComponent<Painkillers>();
		if (painkillers != null) // Unity object — ==
		{
			painkillers.actualOpiateReception = actual;
		}
	}

	// ===== Pure medical-display rules =====

	/// <summary>
	/// Native <c>Body.Update</c> recomputes <c>breathing</c> every frame as
	/// <c>alive &amp;&amp; respiratoryRate &gt; 10</c> (Body.cs:2770). The display
	/// body never runs that pass, so without this projection a stopped remote
	/// breathing state would keep the template's default <c>breathing=true</c>
	/// and the moodle manager would show only hypoventilation instead of the
	/// critical "cannot breathe" icon.
	/// </summary>
	internal static bool ProjectBreathing(CharacterHealthMsg health) =>
		health.Alive && health.RespiratoryRate > 10f;

	/// <summary>
	/// The display's opiate actual-reception must advance with the
	/// already-committed opiate dose, not only with the 1 Hz
	/// <c>ActualOpiateReception</c> sample. This mirrors the live
	/// <c>Painkillers.Update</c> ramp (5 units/sec toward
	/// <c>opiateAmount - opiateTolerance</c>); the caller stores the raw actual
	/// on the display body's <c>Painkillers</c> component, exactly like the
	/// native body does.
	/// </summary>
	internal static float AdvanceOpiateReception(
		CharacterHealthMsg health,
		float currentActualReception,
		float deltaTime)
	{
		if (health.OpiateAmount == 0f && health.OpiateTolerance == 0f)
		{
			return 0f;
		}

		return MoveTowards(
			currentActualReception,
			health.OpiateAmount - health.OpiateTolerance,
			deltaTime * 5f);
	}

	/// <summary>
	/// Native Painkillers converts the raw actual reception into the
	/// body-visible opiate happiness (Painkillers.cs:93-104).
	/// </summary>
	internal static float OpiateHappinessFromReception(float reception) =>
		reception > 0f ? reception : Math.Max(-80f, reception * 1.66f);

	/// <summary>
	/// Inverse of <see cref="OpiateHappinessFromReception"/> for the positive
	/// and mild negative ranges. The display body stores the projected raw
	/// actual in <c>Body.opiateHappiness</c> so the per-frame native ramp can
	/// continue; this converts it back for the next step.
	/// </summary>
	internal static float ToActualReception(float opiateHappiness)
	{
		if (opiateHappiness > 0f)
		{
			return opiateHappiness;
		}

		if (opiateHappiness < 0f)
		{
			return opiateHappiness / 1.66f;
		}

		return 0f;
	}

	/// <summary>
	/// The native WoundView respiratory line reads
	/// <c>Body.respiratoryRateReadout</c>, which is generated only by the live
	/// body's circulation pass (Body.cs:931). The display body never runs that
	/// pass, so the readout must be projected here.
	/// </summary>
	internal static string ProjectRespiratoryRateReadout(CharacterHealthMsg health) =>
		Math.Round(health.RespiratoryRate * 0.25f).ToString("0") + "/m";

	/// <summary>
	/// Native ECG animation is driven by <c>Body.heartProg</c>, which only
	/// advances on a live body (Body.cs:888-906). The inactive display body
	/// would otherwise keep a constant <c>heartProg</c> and the redirected ECG
	/// waveform would be frozen/flat. This mirrors the native wrap exactly:
	/// when the progress passes 1 it is capped at 1.2 (when applicable) and
	/// then subtracts 1.
	/// </summary>
	internal static float AdvanceHeartProgress(float heartRate, float currentProgress, float deltaTime)
	{
		if (heartRate <= 0f)
		{
			return 0f;
		}

		var progress = currentProgress + deltaTime * heartRate / 60f;
		if (progress > 1f)
		{
			if (progress > 1.2f)
			{
				progress = 1.2f;
			}

			progress -= 1f;
		}

		return progress;
	}

	private static float MoveTowards(float current, float target, float maxDelta)
	{
		var delta = target - current;
		if (Math.Abs(delta) <= maxDelta)
		{
			return target;
		}

		return current + Math.Sign(delta) * maxDelta;
	}
}
