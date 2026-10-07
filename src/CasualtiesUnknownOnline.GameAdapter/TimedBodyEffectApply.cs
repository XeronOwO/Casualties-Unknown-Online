using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Local target-side application of host-authoritative timed/random DRINK
/// medicine effects carried by cross-player item-use results. It reuses the
/// game's <c>CoUtils.DoTimedOp</c> 1 Hz tick semantics directly, so a
/// cross-player drinkable behaves exactly like the native
/// <c>WaterContainerItem.Drink</c> path. The effect lives on the target's
/// simulated body; the ordinary character snapshot paths carry the resulting
/// state back to the host. Per-action random rolls are intentionally local
/// because they are not authoritative state.
/// <para>
/// The injection family no longer travels here: Part A of
/// <c>mod-cross-player-native-semantics</c> moved it onto the game's own
/// <c>LiquidType.onHealthUse</c> delegates on the patient's client
/// (<see cref="NativeInjectionApply"/>), which removed the eight
/// injection-only branches this file used to carry — and with them the hand
/// copies of the native per-tick bodies and the two reflective step lookups.
/// </para>
/// </summary>
internal static class TimedBodyEffectApply
{
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

		foreach (var effect in effects)
		{
			switch (effect.EffectId)
			{
				case "antirad":
					CoUtils.instance.DoTimedOp("antirad", () =>
					{
						body.radiationSickness -= 0.2f;
						if (CoUtils.instance.DurationOf("antirad") > 180f)
						{
							body.sicknessAmount += 0.6f;
							if (body.limbs.Length > 1)
							{
								body.limbs[1].pain += 1.5f;
							}

							body.overdoseIndex = 3;
						}
					}, effect.DurationSeconds);
					break;

				case "naltrexone":
					{
						var dose = effect.DoseMl * 0.05f;
						if (Random.value < 0.15f * dose)
						{
							body.vomiter.Vomit();
						}

						CoUtils.instance.DoTimedOp("naltrexone", () =>
						{
							body.sicknessAmount -= 1f;
						}, effect.DurationSeconds);
						break;
					}

				case "braingrow":
					{
						var twentyMl = effect.DoseMl * 0.05f;
						if (body.brainGrowSickness > 0f || effect.DoseMl > 40f)
						{
							body.shock = twentyMl * 10f;
							body.Ragdoll();
						}

						if (Random.value < twentyMl * 0.5f)
						{
							body.vomiter.Vomit();
						}

						CoUtils.instance.DoTimedOp("braingrow", () =>
						{
							if (body.alive)
							{
								body.brainHealth += 0.1f * twentyMl;
								body.strokeAmount -= 1.5f;
							}
						}, effect.DurationSeconds);
						break;
					}

				case "antidepressants":
					{
						var antidepressants = body.GetComponent<Antidepressants>();
						if (antidepressants == null) // Unity object — ==
						{
							antidepressants = body.gameObject.AddComponent<Antidepressants>();
						}

						antidepressants.TakeDose(effect.DoseMl * 5f);
						break;
					}

				default:
					log.LogWarning("[ItemUse] timed body effect skipped: unknown effect {Effect}.", effect.EffectId);
					continue;
			}

			log.LogInformation("[ItemUse] scheduled timed body effect: {Effect} for {Duration:F1}s.", effect.EffectId, effect.DurationSeconds);
		}
	}
}
