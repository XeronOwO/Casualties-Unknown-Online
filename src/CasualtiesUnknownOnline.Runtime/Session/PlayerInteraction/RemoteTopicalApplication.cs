using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of the curated cross-player topical effects to a character
/// snapshot: per-ml body/limb deltas and the same most-injured-limb choice as
/// the cross-player heal and medicine slices. No game assembly, no state, no
/// I/O — the same code path is used by the host authority and the L0 tests.
/// </summary>
public static class RemoteTopicalApplication
{
	/// <summary>
	/// Apply one topical treatment plan (already scaled by the actual ml drawn)
	/// to a target body-health and limb snapshot.
	/// </summary>
	public static void Apply(
		CharacterHealthMsg health,
		IReadOnlyList<CharacterLimbMsg> limbs,
		IReadOnlyList<LiquidStackMsg> plan,
		int requestedLimbIndex = -1)
	{
		if (health is null || plan.Count == 0)
		{
			return;
		}

		var limbIndex = RemoteHealApplication.ResolveLimbIndex(limbs, requestedLimbIndex);
		var limb = limbIndex >= 0 && limbIndex < limbs.Count ? limbs[limbIndex] : null;

		foreach (var dose in plan)
		{
			if (!RemoteTopicalCatalog.TryGetLiquid(dose.LiquidId, out var effect))
			{
				continue;
			}

			var amount = dose.Amount;
			if (limb is not null)
			{
				limb.Pain += effect.PainPerMl * amount;
				limb.MuscleHealth += effect.MuscleHealthPerMl * amount;
				limb.InfectionAmount += effect.InfectionAmountPerMl * amount;
				limb.BandageSlowAmount += effect.BandageSlowAmountPerMl * amount;
				limb.SkinHealAmount += effect.SkinHealAmountPerMl * amount;

				if (effect.DisinfectionTimePerMl > 0f)
				{
					limb.DisinfectionTime = Math.Max(limb.DisinfectionTime, effect.DisinfectionTimePerMl * amount);
				}

				if (effect.PainMultiplierDoseMl > 0f)
				{
					var factor = 1f + (effect.PainMultiplier - 1f) * Math.Min(1f, amount / effect.PainMultiplierDoseMl);
					limb.Pain *= factor;
				}
			}

			health.BloodViscosity += effect.BloodViscosityPerMl * amount;
			health.SicknessAmount += effect.SicknessAmountPerMl * amount;
			health.Dirtyness += effect.DirtynessPerMl * amount;
		}
	}
}
