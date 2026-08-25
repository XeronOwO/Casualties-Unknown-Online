using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of topical (non-injectable) limb-treatment
/// containers for the third cross-player item-use slice. It maps known item
/// ids to the exact ml the game's <c>WaterContainerItem.ApplyToLimb</c> drains
/// per use, and maps each supported health-usable liquid to its immediate
/// per-ml effect. Unknown liquids are refused as a whole so an unsupported
/// effect is never silently approximated. Timed/random branches stay outside
/// this slice.
/// </summary>
public static class RemoteTopicalCatalog
{
	private static readonly IReadOnlyDictionary<string, float> TopicalAmounts =
		new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["paincream"] = 10f,
			["woundglue"] = 20f,
			["disinfectant"] = 10f,
			["spraybottle"] = 10f,
		};

	// Coefficients are the immediate, linear part of Liquids.cs onHealthUse.
	// Disinfection is stored as a set-value per ml (SetDisinfect uses max, not
	// addition) and handled by the application; PainMultiplier models woundglue's
	// multiplicative pain reduction.
	private static readonly IReadOnlyDictionary<string, RemoteTopicalLiquidEffect> Liquids =
		new Dictionary<string, RemoteTopicalLiquidEffect>(StringComparer.Ordinal)
		{
			["alcohol"] = new("alcohol", PainPerMl: 0.10f, DisinfectionTimePerMl: 3.5f),
			["bleach"] = new("bleach", PainPerMl: 0.25f, MuscleHealthPerMl: -0.15f, InfectionAmountPerMl: -0.05f, SkinHealAmountPerMl: -0.20f, DisinfectionTimePerMl: 4f),
			["reliefcream"] = new("reliefcream", SkinHealAmountPerMl: 0.30f, DisinfectionTimePerMl: 30f),
			["woundglue"] = new("woundglue", SkinHealAmountPerMl: 0.50f, MuscleHealthPerMl: 0.25f, InfectionAmountPerMl: -0.25f, BandageSlowAmountPerMl: 1.50f, DisinfectionTimePerMl: 15f, BloodViscosityPerMl: 0.75f, SicknessAmountPerMl: 0.125f, PainMultiplier: 0.9f, PainMultiplierDoseMl: 20f),
			["disinfectant"] = new("disinfectant", PainPerMl: 1f, DisinfectionTimePerMl: 24f),
			["soap"] = new("soap", DirtynessPerMl: -0.25f, DisinfectionTimePerMl: 0.30f),
		};

	public static bool IsTopicalItem(string itemId) => TopicalAmounts.ContainsKey(itemId);

	public static bool TryGetTopicalAmount(string itemId, out float amount) =>
		TopicalAmounts.TryGetValue(itemId, out amount);

	public static bool IsSupportedTopicalLiquid(string liquidId) => Liquids.ContainsKey(liquidId);

	public static bool TryGetLiquid(string liquidId, out RemoteTopicalLiquidEffect effect) =>
		Liquids.TryGetValue(liquidId, out effect!);

	/// <summary>
	/// Build the exact liquid draw the host will commit for a known topical
	/// container: drain the item's per-use amount or the whole remaining stack
	/// if it holds less. The draw is proportional across every liquid stack
	/// (<c>WaterContainerItem.CalculateDrain</c>). Refuses unknown liquids as a
	/// whole.
	/// </summary>
	public static bool TryCreatePlan(
		IReadOnlyList<LiquidStackMsg>? liquids,
		string itemId,
		out List<LiquidStackMsg> drained)
	{
		drained = [];
		if (!TryGetTopicalAmount(itemId, out var amount) || amount <= 0f)
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
			if (!IsSupportedTopicalLiquid(liquid.LiquidId))
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
