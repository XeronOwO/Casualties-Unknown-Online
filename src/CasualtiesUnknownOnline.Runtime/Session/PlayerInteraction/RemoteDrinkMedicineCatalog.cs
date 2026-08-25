using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of drinkable medicine containers for the
/// cross-player item-use slice. It maps the native <c>useAction</c> items that
/// call <c>WaterContainerItem.Drink</c> to the ml drained per use, and maps
/// each supported <c>LiquidType.onDrink</c> liquid to its pure per-ml effect.
/// Unknown items/liquids are refused as a whole so an unsupported effect is
/// never silently approximated. Timed/random/component branches travel as
/// <c>TimedBodyEffectMsg</c> or in the character snapshot's medication
/// component fields and run on the target's local body.
/// </summary>
public static class RemoteDrinkMedicineCatalog
{
	private static readonly IReadOnlyDictionary<string, float> DrinkAmounts =
		new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["naltrexone"] = 20f,
			["sodiumnitroprusside"] = 50f,
			["vasopressin"] = 50f,
			["amiodarone"] = 50f,
			["painkillers"] = 10f,
			["keratinbooster"] = 50f,
			["braingrow"] = 20f,
			["antidepressants"] = 20f,
			["antibiotics"] = 20f,
			["mindwipe"] = 60f,
			["antirad"] = 20f,
			["sleepingpills"] = 5f,
		};

	// Coefficients are the linear part of Liquids.cs onDrink delegates. The
	// injection and drink paths use different formulas (e.g. morphine drink
	// opiate 0.4/ml vs inject 0.9/ml), so this catalog is intentionally separate
	// from RemoteMedicineCatalog.
	private static readonly IReadOnlyDictionary<string, RemoteDrinkMedicineEffect> Liquids =
		new Dictionary<string, RemoteDrinkMedicineEffect>(StringComparer.Ordinal)
		{
			// Liquids.cs:356-373.
			["naltrexone"] = new("naltrexone",
				HappinessPerMl: -0.10f,
				AntagonistAmountPerMl: 0.75f,
				OpiateTolerancePerMl: -0.75f,
				TimedEffectId: "naltrexone",
				TimedDurationPerMl: 1.25f),
			// Liquids.cs:1432-1435.
			["sodiumnitroprusside"] = new("sodiumnitroprusside",
				SicknessPerMl: 0.05f),
			// Liquids.cs:1449-1453.
			["vasopressin"] = new("vasopressin",
				BloodPressureChangeFromMedicinePerMl: -0.5f),
			// Liquids.cs:1476-1480.
			["amiodarone"] = new("amiodarone",
				SicknessPerMl: 0.1f),
			// Liquids.cs:298-302.
			["painkillers"] = new("painkillers",
				OpiateAmountPerMl: 1.4f),
			// Liquids.cs:1256-1266 (overdose branch when clawRegrowTime > 3600).
			["keratinbooster"] = new("keratinbooster",
				ClawRegrowTimePerMl: 24f,
				ClawRegrowOverdoseTimePerMl: 2.4f,
				ClawRegrowOverdoseSicknessPerMl: 0.2f),
			// Liquids.cs:1119-1148.
			["braingrow"] = new("braingrow",
				HappinessPerMl: -0.25f,
				SicknessPerMl: 1f,
				ShockPerMl: 0.5f,
				BrainGrowSicknessPerMl: 60f,
				BrainGrowMindwipeThresholdMl: 40f,
				TimedEffectId: "braingrow",
				TimedDurationSeconds: 100f),
			// Liquids.cs:1170-1176. Component dose is applied through a local
			// one-shot TimedBodyEffect (TakeDose) so the game's 30% sickness
			// roll stays on the simulated body.
			["antidepressants"] = new("antidepressants",
				HappinessPerMl: 0.05f,
				TimedEffectId: "antidepressants"),
			// Liquids.cs:1184-1191.
			["antibiotics"] = new("antibiotics",
				HappinessPerMl: -0.05f,
				SepticShockPerMl: -0.25f,
				AntibioticImmunityTimePerMl: 25f),
			// Liquids.cs:1150-1164.
			["mindwipe"] = new("mindwipe",
				TriggersMindwipe: true),
			// Liquids.cs:1273-1287.
			["antirad"] = new("antirad",
				TimedEffectId: "antirad",
				TimedDurationPerMl: 4.5f),
			// Liquids.cs:1294-1299.
			["sleepingpills"] = new("sleepingpills",
				SleepingPillsAmountPerMl: 60f),
			// Liquids.cs:222-226 (drink path of morphine; used by mixed
			// drinkable medicine containers such as mindwipe).
			["morphine"] = new("morphine",
				OpiateAmountPerMl: 0.4f),
		};

	public static bool IsDrinkableMedicineItem(string itemId) => DrinkAmounts.ContainsKey(itemId);

	public static bool TryGetDrinkAmount(string itemId, out float amount) =>
		DrinkAmounts.TryGetValue(itemId, out amount);

	public static bool IsSupportedDrinkMedicineLiquid(string liquidId) => Liquids.ContainsKey(liquidId);

	public static bool TryGetLiquid(string liquidId, out RemoteDrinkMedicineEffect effect) =>
		Liquids.TryGetValue(liquidId, out effect!);

	/// <summary>
	/// The native mindwipe item refuses to drink while the target is mentally
	/// healthy (<c>Item.cs:1343-1351</c>). The host mirrors that gate so a
	/// cross-player mindwipe use is refused instead of silently wasting the dose.
	/// </summary>
	public static bool IsMindwipeBlocked(string itemId, CharacterHealthMsg health) =>
		itemId == "mindwipe"
		&& health is not null
		&& health.Happiness > -50f
		&& health.BrainHealth > 90f
		&& health.StrokeAmount < 5f;

	/// <summary>
	/// Build the exact liquid draw the host will commit for a known drinkable
	/// medicine container: drain the item's per-use drink amount or the whole
	/// remaining stack if it holds less. The draw is proportional across every
	/// liquid stack (<c>WaterContainerItem.CalculateDrain</c>). Refuses unknown
	/// liquids as a whole.
	/// </summary>
	public static bool TryCreatePlan(
		IReadOnlyList<LiquidStackMsg>? liquids,
		string itemId,
		out List<LiquidStackMsg> drained)
	{
		drained = [];
		if (!TryGetDrinkAmount(itemId, out var amount) || amount <= 0f)
		{
			return false;
		}

		if (liquids is null || liquids.Count == 0)
		{
			return false;
		}

		var total = 0f;
		foreach (var liquid in liquids)
		{
			if (!IsSupportedDrinkMedicineLiquid(liquid.LiquidId))
			{
				return false;
			}

			total += liquid.Amount;
		}

		if (total <= 0f)
		{
			return false;
		}

		var draw = Math.Min(amount, total);
		foreach (var liquid in liquids)
		{
			drained.Add(new LiquidStackMsg
			{
				LiquidId = liquid.LiquidId,
				Amount = liquid.Amount * (draw / total),
			});
		}

		return true;
	}
}
