using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of the first-slice cross-player consumable effects to a
/// character snapshot: draw a drink plan from a liquid stack and apply the
/// curated liquid effects, or apply one solid food effect. No game assembly, no
/// state, no I/O — the same code path is used by the host authority and the L0
/// tests.
/// </summary>
public static class RemoteConsumeApplication
{
	/// <summary>
	/// Build the exact liquid draw the host will commit: drink
	/// <see cref="RemoteConsumeCatalog.DrinkAmountMl"/> ml or the whole stack if
	/// it holds less; the draw is proportional across every liquid stack
	/// (<c>WaterContainerItem.CalculateDrain</c>). Refuses unknown liquid ids so
	/// no unsupported effect is silently approximated.
	/// </summary>
	public static bool TryCreateDrinkPlan(
		IReadOnlyList<LiquidStackMsg>? liquids,
		out List<LiquidStackMsg> drained)
	{
		drained = [];
		if (!RemoteConsumeCatalog.IsDrinkable(liquids))
		{
			return false;
		}

		var total = 0f;
		foreach (var liquid in liquids!)
		{
			total += liquid.Amount;
		}

		var amount = Math.Min(RemoteConsumeCatalog.DrinkAmountMl, total);
		if (amount <= 0f)
		{
			return false;
		}

		foreach (var liquid in liquids!)
		{
			drained.Add(new LiquidStackMsg
			{
				LiquidId = liquid.LiquidId,
				Amount = liquid.Amount * (amount / total),
			});
		}

		return true;
	}

	/// <summary>Apply one drink plan (already scaled by the actual ml drawn) to a target body-health snapshot.</summary>
	public static void ApplyDrink(CharacterHealthMsg health, IReadOnlyList<LiquidStackMsg> drained)
	{
		if (health is null)
		{
			return;
		}

		foreach (var drink in drained)
		{
			if (!RemoteConsumeCatalog.TryGetLiquid(drink.LiquidId, out var effect))
			{
				continue;
			}

			var scale = drink.Amount / RemoteConsumeCatalog.DrinkAmountMl;
			health.Thirst += effect.ThirstPer100Ml * scale;
			health.Hunger += effect.HungerPer100Ml * scale;
			health.WeightOffset += effect.WeightPer100Ml * scale;
			health.Stamina += effect.StaminaPer100Ml * scale;
			health.Energy += effect.EnergyPer100Ml * scale;
			health.Happiness += effect.HappinessPer100Ml * scale;
			health.Temperature += effect.TemperaturePer100Ml * scale;
			health.SicknessAmount += effect.SicknessPer100Ml * scale;
			health.Caffeinated += effect.CaffeinatedPer100Ml * scale;
			health.BloodVolume += effect.BloodVolumePer100Ml * scale;
			health.RadiationSickness += effect.RadiationSicknessPer100Ml * scale;
		}
	}

	/// <summary>Apply one solid food effect to a target body-health snapshot.</summary>
	public static void ApplyFood(CharacterHealthMsg health, RemoteFoodEffect effect)
	{
		if (health is null)
		{
			return;
		}

		health.Hunger += effect.Hunger;
		health.Thirst += effect.Thirst;
		health.WeightOffset += effect.WeightOffset;
		health.Stamina += effect.Stamina;
		health.Energy += effect.Energy;
		health.Happiness += effect.Happiness;
		health.Temperature += effect.Temperature;
		health.SicknessAmount += effect.Sickness;
		health.Caffeinated += effect.Caffeinated;
		health.RadiationSickness += effect.RadiationSickness;
		health.BloodVolume += effect.BloodVolume;
		health.SepticShock += effect.SepticShock;
		health.HearingLoss += effect.HearingLoss;
	}
}
