using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of the curated cross-player drinkable-medicine effects to
/// a character snapshot: per-ml body deltas, the keratin/braingrow conditional
/// branches, and medication component flags. No game assembly, no state, no
/// I/O — the same code path is used by the host authority and the L0 tests.
/// Timed/random/one-shot component effects are built as
/// <c>TimedBodyEffectMsg</c> and run on the target's local body; the ordinary
/// character snapshot paths carry the resulting state back.
/// </summary>
public static class RemoteDrinkMedicineApplication
{
	/// <summary>
	/// Apply one drinkable-medicine plan (already scaled by the actual ml drawn)
	/// to a target body-health snapshot.
	/// </summary>
	public static void Apply(CharacterHealthMsg health, IReadOnlyList<LiquidStackMsg> plan)
	{
		if (health is null || plan.Count == 0)
		{
			return;
		}

		foreach (var dose in plan)
		{
			if (!RemoteDrinkMedicineCatalog.TryGetLiquid(dose.LiquidId, out var effect))
			{
				continue;
			}

			var amount = dose.Amount;
			health.SicknessAmount += effect.SicknessPerMl * amount;
			health.Happiness += effect.HappinessPerMl * amount;
			health.SepticShock += effect.SepticShockPerMl * amount;
			health.AntibioticImmunityTime += effect.AntibioticImmunityTimePerMl * amount;
			health.BloodPressureChangeFromMedicine += effect.BloodPressureChangeFromMedicinePerMl * amount;
			health.OpiateAmount += effect.OpiateAmountPerMl * amount;
			health.AntagonistAmount += effect.AntagonistAmountPerMl * amount;
			health.OpiateTolerance += effect.OpiateTolerancePerMl * amount;
			health.SleepingPillsAmount += effect.SleepingPillsAmountPerMl * amount;

			if (effect.ClawRegrowTimePerMl != 0f || effect.ClawRegrowOverdoseTimePerMl != 0f)
			{
				if (health.ClawRegrowTime > 3600f)
				{
					health.SicknessAmount += effect.ClawRegrowOverdoseSicknessPerMl * amount;
					health.ClawRegrowTime += effect.ClawRegrowOverdoseTimePerMl * amount;
				}
				else
				{
					health.ClawRegrowTime += effect.ClawRegrowTimePerMl * amount;
				}
			}

			if (effect.BrainGrowSicknessPerMl != 0f)
			{
				var mindwipeBefore = health.BrainGrowSickness > 0f || amount > effect.BrainGrowMindwipeThresholdMl;
				if (mindwipeBefore)
				{
					health.Shock += effect.ShockPerMl * amount;
					if (!health.MindwipeScriptPresent)
					{
						health.MindwipeScriptPresent = true;
						health.MindwipeScriptActive = false;
					}
				}

				health.BrainGrowSickness = Math.Max(health.BrainGrowSickness, effect.BrainGrowSicknessPerMl * amount);
			}

			if (effect.TriggersMindwipe && !health.MindwipeScriptPresent)
			{
				health.MindwipeScriptPresent = true;
				health.MindwipeScriptActive = false;
			}
		}
	}

	/// <summary>
	/// Build the timed/random/one-shot body effects carried by a drinkable
	/// medicine plan. These are intentionally not applied to the host snapshot:
	/// the target's local body must run the native lambda/component action so
	/// per-action random rolls stay on the simulated body. Empty for
	/// immediate-only medicines.
	/// </summary>
	public static List<TimedBodyEffectMsg> BuildTimedEffects(IReadOnlyList<LiquidStackMsg>? plan)
	{
		var effects = new List<TimedBodyEffectMsg>();
		if (plan is null)
		{
			return effects;
		}

		foreach (var dose in plan)
		{
			if (!RemoteDrinkMedicineCatalog.TryGetLiquid(dose.LiquidId, out var effect)
				|| effect.TimedEffectId is null)
			{
				continue;
			}

			var duration = effect.TimedDurationSeconds > 0f
				? effect.TimedDurationSeconds
				: effect.TimedDurationPerMl * dose.Amount;

			effects.Add(new TimedBodyEffectMsg
			{
				EffectId = effect.TimedEffectId,
				DurationSeconds = duration,
				DoseMl = dose.Amount,
			});
		}

		return effects;
	}
}
