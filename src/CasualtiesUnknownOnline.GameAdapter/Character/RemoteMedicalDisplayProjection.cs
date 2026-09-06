using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Pure projection rules for the remote WoundView display-only body. The
/// display body is deliberately inactive, so the native Body/component Update
/// methods never run; this helper calculates the few readout fields that the
/// native panel reads directly from the live body but that cannot be recovered
/// from the wire snapshot by Mapster alone.
/// </summary>
internal static class RemoteMedicalDisplayProjection
{
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
		// Native Painkillers self-destroys when both amounts clear; the display
		// must not keep a stale opiate mood/component during that state.
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
