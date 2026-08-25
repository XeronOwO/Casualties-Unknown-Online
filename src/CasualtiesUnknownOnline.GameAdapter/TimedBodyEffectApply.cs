using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Local target-side application of host-authoritative timed/random liquid
/// medicine effects carried by cross-player item-use results. It reuses the
/// game's <c>CoUtils.DoTimedOp</c> 1 Hz tick semantics directly, so a
/// cross-player timed injectable behaves exactly like the native
/// <c>WaterContainerItem.Inject</c> path (Liquids.cs onHealthUse). The effect
/// lives on the target's simulated body; the ordinary character snapshot paths
/// carry the resulting state back to the host. Per-action random rolls are
/// intentionally local because they are not authoritative state.
/// </summary>
internal static class TimedBodyEffectApply
{
	private static readonly MethodInfo? HighGradeStimulantStep =
		typeof(Liquids).GetMethod("HighGradeStimulantStep", BindingFlags.NonPublic | BindingFlags.Static);

	private static readonly MethodInfo? LowGradeStimulantStep =
		typeof(Liquids).GetMethod("LowGradeStimulantStep", BindingFlags.NonPublic | BindingFlags.Static);

	public static void Apply(Body body, IReadOnlyList<TimedBodyEffectMsg> effects, ILogger log)
	{
		if (effects.Count == 0)
		{
			return;
		}

		if (body.limbs.Length == 0)
		{
			log.LogWarning("[ItemUse] timed body effect skipped: local body has no limbs.");
			return;
		}

		var limb = body.limbs[0];
		foreach (var effect in effects)
		{
			switch (effect.EffectId)
			{
				case "chloroform":
					CoUtils.instance.DoTimedOp("chloroform", () =>
					{
						body.consciousness = Mathf.MoveTowards(body.consciousness, 0f, 8f);
					}, effect.DurationSeconds);
					break;

				case "highgradestimulant":
					if (!ScheduleNativeStep(HighGradeStimulantStep, "highgradestimulant", limb, effect.DurationSeconds, log))
					{
						continue;
					}

					break;

				case "midgradestimulant":
					CoUtils.instance.DoTimedOp("midgradestimulant", () =>
					{
						MidGradeStimulantStep(body, limb);
					}, effect.DurationSeconds);
					break;

				case "lowgradestimulant":
					if (!ScheduleNativeStep(LowGradeStimulantStep, "lowgradestimulant", limb, effect.DurationSeconds, log))
					{
						continue;
					}

					break;

				case "procoagulant":
					CoUtils.instance.DoTimedOp("procoagulant", () =>
					{
						body.internalBleeding *= 0.95f;
						body.bloodViscosity += 1.75f;
						body.strokeAmount -= 10f;
						foreach (var targetLimb in body.limbs)
						{
							targetLimb.bleedAmount *= 0.96f;
						}
					}, effect.DurationSeconds);
					break;

				case "epinephrine":
					CoUtils.instance.DoTimedOp("epinephrine", () =>
					{
						body.adrenaline = 100f;
						if (body.alive && body.inCardiacArrest && Random.value < 0.05f)
						{
							body.heartRate = 200f;
							body.fibrillationProgress = 50f;
						}

						if (CoUtils.instance.DurationOf("epinephrine") > 240f)
						{
							body.TryStartFibrillation(true);
						}
					}, effect.DurationSeconds);
					break;

				case "oxyline":
					CoUtils.instance.DoTimedOp("oxyline", () =>
					{
						body.respiratoryRate += 2.5f;
						body.bloodOxygen += 1.666f;
						body.stamina += 2.5f;
						body.fibrillationProgress -= 1.2f;
						body.bloodVolume += 0.1f;
					}, effect.DurationSeconds);
					break;

				case "amiodarone":
					CoUtils.instance.DoTimedOp("amiodarone", () =>
					{
						if (body.fibrillationProgress > 0f)
						{
							body.fibrillationProgress = Mathf.MoveTowards(body.fibrillationProgress, 0f, 2f);
						}

						foreach (var targetLimb in body.limbs)
						{
							targetLimb.muscleHealth -= 0.25f;
						}
					}, effect.DurationSeconds);
					break;

				default:
					log.LogWarning("[ItemUse] timed body effect skipped: unknown effect {Effect}.", effect.EffectId);
					continue;
			}

			log.LogInformation("[ItemUse] scheduled timed body effect: {Effect} for {Duration:F1}s.", effect.EffectId, effect.DurationSeconds);
		}
	}

	private static bool ScheduleNativeStep(
		MethodInfo? method,
		string operationId,
		Limb limb,
		float duration,
		ILogger log)
	{
		if (method is null)
		{
			log.LogError("[ItemUse] timed body effect skipped: native {Operation} step was not found.", operationId);
			return false;
		}

		CoUtils.instance.DoTimedOp(operationId, () =>
		{
			method.Invoke(null, [limb]);
		}, duration);
		return true;
	}

	private static void MidGradeStimulantStep(Body body, Limb limb)
	{
		var duration = CoUtils.instance.DurationOf("midgradestimulant");
		body.stamina += 2f;
		body.consciousness += 2f;
		body.energy += 0.1f;
		body.sicknessAmount += 0.1f;
		body.internalBleeding += 0.06f;
		body.adrenaline += 7f;
		if (Random.value < 0.1f)
		{
			body.miscShakeIntensity += 1.5f;
		}

		if (body.stimulantMultiplier < 0.25f)
		{
			body.stimulantMultiplier += 0.035f;
		}

		if (duration > 220f)
		{
			if (Random.value < 0.18f)
			{
				body.miscShakeIntensity += 1.5f;
			}

			if (Random.value < 0.1f)
			{
				body.stamina -= 35f;
			}

			if (Random.value < 0.06f)
			{
				body.Ragdoll();
			}

			body.internalBleeding += 0.15f;
			body.brainHealth -= 0.05f;
			if (body.limbs.Length > 1 && body.limbs[1].pain < 60f)
			{
				body.limbs[1].pain += 4f;
			}

			body.overdoseIndex = 3;
		}

		if (CoUtils.instance.HighestDurationOf("midgradestimulant") > 59f)
		{
			if (duration < 30f && Random.value < 0.1f)
			{
				body.stamina -= 25f;
			}

			if (duration <= 1f)
			{
				body.energy -= 30f;
				body.vomiter.Vomit();
			}
		}
	}
}
