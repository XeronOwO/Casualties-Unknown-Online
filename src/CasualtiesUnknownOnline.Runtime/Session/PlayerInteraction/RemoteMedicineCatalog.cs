using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of injectable/IV medicine containers for the
/// second cross-player item-use slice. It maps a known medicine item to the
/// ml the game's <c>WaterContainerItem.Inject</c> drains per use, and maps each
/// supported liquid to a pure per-ml effect. Unknown items/liquids are refused
/// as a whole so an unsupported effect is never silently approximated. Only
/// immediate body/limb surfaces that already ride the character snapshot are
/// included; opiate/components and timed/random effects stay future slices.
/// </summary>
public static class RemoteMedicineCatalog
{
	private static readonly IReadOnlyDictionary<string, float> InjectionAmounts =
		new Dictionary<string, float>(StringComparer.Ordinal)
		{
			["saline"] = 80f,
			["ringersolution"] = 80f,
			["bloodbag"] = 375f,
			["bloodbaghuman"] = 375f,
			["antiserum"] = 50f,
			["ceftriaxone"] = 100f,
			["streptokinase"] = 33.334f,
		};

	private static readonly IReadOnlyDictionary<string, RemoteMedicineLiquidEffect> Liquids =
		new Dictionary<string, RemoteMedicineLiquidEffect>(StringComparer.Ordinal)
		{
			// WaterContainerItem.Drink/Inject formulas from Liquids.cs:1504-1589:
			// saline 750 ml denominator, ringersolution 700, blood 750,
			// redblood 750, antiserum ml*0.02, ceftriaxone ml*0.01,
			// streptokinase 33.334 ml denominator.
			["saline"] = new("saline",
				BloodVolumePerMl: 40f / 750f,
				BloodViscosityPerMl: -50f / 750f,
				ThirstPerMl: 70f / 750f),
			["ringersolution"] = new("ringersolution",
				BloodVolumePerMl: 35f / 700f,
				BloodViscosityPerMl: -40f / 700f,
				ThirstPerMl: 60f / 700f),
			["blood"] = new("blood",
				BloodVolumePerMl: 30f / 750f),
			["redblood"] = new("redblood",
				BloodVolumePerMl: 30f / 750f,
				SicknessPerMl: 50f / 750f,
				SepticShockPerMl: 40f / 750f,
				MuscleHealthPerMl: -30f / 750f),
			["antiserum"] = new("antiserum",
				BloodVolumePerMl: 3f * 0.02f,
				SepticShockPerMl: -10f * 0.02f,
				AntibioticImmunityTimePerMl: 300f * 0.02f,
				DisinfectionTimePerMl: 180f * 0.02f),
			["ceftriaxone"] = new("ceftriaxone",
				AntibioticImmunityTimePerMl: 1125f * 0.01f,
				PainPerMl: 80f * 0.01f),
			["streptokinase"] = new("streptokinase",
				BloodViscosityPerMl: -50f / 33.334f,
				SicknessPerMl: 5f / 33.334f),
		};

	// Liquids that the game may inject without an onHealthUse effect (mostly
	// solvent/water in mixed medicine containers). They are allowed so a
	// refilled container is not refused just because of an inert carrier.
	private static readonly HashSet<string> InertLiquids = ["water"];

	public static bool IsInjectableItem(string itemId) => InjectionAmounts.ContainsKey(itemId);

	public static bool TryGetInjectionAmount(string itemId, out float amount) =>
		InjectionAmounts.TryGetValue(itemId, out amount);

	public static bool IsSupportedMedicineLiquid(string liquidId) =>
		Liquids.ContainsKey(liquidId) || InertLiquids.Contains(liquidId);

	public static bool TryGetLiquid(string liquidId, out RemoteMedicineLiquidEffect effect) =>
		Liquids.TryGetValue(liquidId, out effect!);

	/// <summary>
	/// Build the exact liquid draw the host will commit for a known medicine
	/// container: drain the container's per-item injection amount or the whole
	/// remaining stack if it holds less. The draw is proportional across every
	/// liquid stack. Refuses unknown liquids as a whole.
	/// </summary>
	public static bool TryCreatePlan(
		IReadOnlyList<LiquidStackMsg>? liquids,
		string itemId,
		out List<LiquidStackMsg> drained)
	{
		drained = [];
		if (!TryGetInjectionAmount(itemId, out var amount) || amount <= 0f)
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
			if (!IsSupportedMedicineLiquid(liquid.LiquidId))
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
