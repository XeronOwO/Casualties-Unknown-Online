using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of the curated cross-player medicine injectable effects to
/// a character snapshot: per-ml body deltas and, for limb-addressable effects,
/// the same most-injured-limb choice as the cross-player heal slice. No game
/// assembly, no state, no I/O — the same code path is used by the host
/// authority and the L0 tests.
/// </summary>
public static class RemoteMedicineApplication
{
	/// <summary>
	/// Apply one medicine injection plan (already scaled by the actual ml drawn)
	/// to a target body-health and limb snapshot.
	/// </summary>
	public static void Apply(
		CharacterHealthMsg health,
		IReadOnlyList<CharacterLimbMsg> limbs,
		IReadOnlyList<LiquidStackMsg> plan)
	{
		if (health is null || plan.Count == 0)
		{
			return;
		}

		var limbIndex = RemoteHealApplication.PickMostInjuredLimb(limbs);
		var limb = limbIndex >= 0 && limbIndex < limbs.Count ? limbs[limbIndex] : null;

		foreach (var medicine in plan)
		{
			if (!RemoteMedicineCatalog.TryGetLiquid(medicine.LiquidId, out var effect))
			{
				continue;
			}

			var scale = medicine.Amount;
			health.BloodVolume += effect.BloodVolumePerMl * scale;
			health.BloodViscosity += effect.BloodViscosityPerMl * scale;
			health.Thirst += effect.ThirstPerMl * scale;
			health.SicknessAmount += effect.SicknessPerMl * scale;
			health.SepticShock += effect.SepticShockPerMl * scale;
			health.AntibioticImmunityTime += effect.AntibioticImmunityTimePerMl * scale;
			health.BloodOxygen += effect.BloodOxygenPerMl * scale;
			health.RespiratoryRate += effect.RespiratoryRatePerMl * scale;
			health.Stamina += effect.StaminaPerMl * scale;
			health.FibrillationProgress += effect.FibrillationProgressPerMl * scale;
			health.StrokeAmount += effect.StrokeAmountPerMl * scale;
			health.Adrenaline += effect.AdrenalinePerMl * scale;
			health.OpiateAmount += effect.OpiateAmountPerMl * scale;
			health.AntagonistAmount += effect.AntagonistAmountPerMl * scale;

			if (limb is null)
			{
				continue;
			}

			limb.Pain += effect.PainPerMl * scale;
			limb.MuscleHealth += effect.MuscleHealthPerMl * scale;
			limb.DisinfectionTime = Math.Max(limb.DisinfectionTime, effect.DisinfectionTimePerMl * scale);
			limb.SkinHealAmount += effect.SkinHealAmountPerMl * scale;
			limb.BleedAmount += effect.BleedAmountPerMl * scale;
			limb.SkinHealth += effect.SkinHealthPerMl * scale;
		}
	}

	/// <summary>
	/// Build the timed/random body effects carried by a medicine plan. These are
	/// intentionally not applied to the host snapshot: the target's local body
	/// must run the native <c>CoUtils.DoTimedOp</c> lambda so per-action random
	/// rolls stay on the simulated body. Empty for immediate-only medicines.
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
			if (!RemoteMedicineCatalog.TryGetLiquid(dose.LiquidId, out var effect)
				|| effect.TimedEffectId is null
				|| effect.TimedDurationPerMl <= 0f)
			{
				continue;
			}

			effects.Add(new TimedBodyEffectMsg
			{
				EffectId = effect.TimedEffectId,
				DurationSeconds = effect.TimedDurationPerMl * dose.Amount,
			});
		}

		return effects;
	}
}
