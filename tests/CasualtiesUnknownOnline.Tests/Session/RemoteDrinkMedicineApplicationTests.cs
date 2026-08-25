using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public sealed class RemoteDrinkMedicineApplicationTests
{
	[Fact]
	public void Catalog_ExposesDrinkableMedicineItemsAndLiquids()
	{
		Assert.True(RemoteDrinkMedicineCatalog.IsDrinkableMedicineItem("naltrexone"));
		Assert.True(RemoteDrinkMedicineCatalog.IsDrinkableMedicineItem("antirad"));
		Assert.True(RemoteDrinkMedicineCatalog.IsDrinkableMedicineItem("sleepingpills"));
		Assert.True(RemoteDrinkMedicineCatalog.IsDrinkableMedicineItem("mindwipe"));
		Assert.True(RemoteDrinkMedicineCatalog.TryGetDrinkAmount("braingrow", out var braingrowAmount));
		Assert.True(Math.Abs(braingrowAmount - 20f) < 0.001f);
		Assert.True(RemoteDrinkMedicineCatalog.IsSupportedDrinkMedicineLiquid("morphine"));
		Assert.False(RemoteDrinkMedicineCatalog.IsSupportedDrinkMedicineLiquid("mystery"));
	}

	[Fact]
	public void Plan_DrawsItemDrinkAmountOrEntireSmallStack()
	{
		var full = new List<LiquidStackMsg> { new() { LiquidId = "antirad", Amount = 100f } };
		Assert.True(RemoteDrinkMedicineCatalog.TryCreatePlan(full, "antirad", out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal("antirad", drain.LiquidId);
		Assert.True(Math.Abs(drain.Amount - 20f) < 0.001f);

		var small = new List<LiquidStackMsg> { new() { LiquidId = "painkillers", Amount = 6f } };
		Assert.True(RemoteDrinkMedicineCatalog.TryCreatePlan(small, "painkillers", out var smallPlan));
		var smallDrain = Assert.Single(smallPlan);
		Assert.True(Math.Abs(smallDrain.Amount - 6f) < 0.001f);
	}

	[Fact]
	public void Plan_MindwipeMixedContainer_DrawsProportionalWholeStack()
	{
		var liquids = new List<LiquidStackMsg>
		{
			new() { LiquidId = "mindwipe", Amount = 30f },
			new() { LiquidId = "morphine", Amount = 30f },
		};

		Assert.True(RemoteDrinkMedicineCatalog.TryCreatePlan(liquids, "mindwipe", out var plan));

		Assert.Equal(2, plan.Count);
		Assert.True(Math.Abs(plan[0].Amount - 30f) < 0.001f);
		Assert.True(Math.Abs(plan[1].Amount - 30f) < 0.001f);
	}

	[Fact]
	public void Plan_RefusesUnknownLiquidForKnownDrinkableMedicine()
	{
		var bad = new List<LiquidStackMsg> { new() { LiquidId = "mystery", Amount = 100f } };
		Assert.False(RemoteDrinkMedicineCatalog.TryCreatePlan(bad, "antirad", out _));
	}

	[Fact]
	public void ApplyPainkillers_AddsOpiateAmount()
	{
		var health = new CharacterHealthMsg { OpiateAmount = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "painkillers", Amount = 10f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.OpiateAmount - 14f) < 0.001f);
	}

	[Fact]
	public void ApplyAntibiotics_AddsImmunityAndAdjustsSepticAndHappiness()
	{
		var health = new CharacterHealthMsg
		{
			Happiness = 10f,
			SepticShock = 50f,
			AntibioticImmunityTime = 0f,
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "antibiotics", Amount = 20f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.Happiness - 9f) < 0.001f);
		Assert.True(Math.Abs(health.SepticShock - 45f) < 0.001f);
		Assert.True(Math.Abs(health.AntibioticImmunityTime - 500f) < 0.001f);
	}

	[Fact]
	public void ApplyKeratinBooster_NormalBranchAddsFullClawRegrowTime()
	{
		var health = new CharacterHealthMsg { ClawRegrowTime = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "keratinbooster", Amount = 50f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.ClawRegrowTime - 1200f) < 0.001f);
		Assert.Equal(0f, health.SicknessAmount);
	}

	[Fact]
	public void ApplyKeratinBooster_OverdoseBranchAddsReducedRegrowAndSickness()
	{
		var health = new CharacterHealthMsg { ClawRegrowTime = 4000f, SicknessAmount = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "keratinbooster", Amount = 50f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.ClawRegrowTime - 4120f) < 0.001f);
		Assert.True(Math.Abs(health.SicknessAmount - 10f) < 0.001f);
	}

	[Fact]
	public void ApplyBraingrow_WithExistingBrainGrowSetsMindwipeAndShock()
	{
		var health = new CharacterHealthMsg
		{
			BrainGrowSickness = 10f,
			Happiness = 0f,
			SicknessAmount = 0f,
			Shock = 0f,
			MindwipeScriptPresent = false,
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "braingrow", Amount = 20f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.Happiness + 5f) < 0.001f);
		Assert.True(Math.Abs(health.SicknessAmount - 20f) < 0.001f);
		Assert.True(Math.Abs(health.Shock - 10f) < 0.001f);
		Assert.True(Math.Abs(health.BrainGrowSickness - 1200f) < 0.001f);
		Assert.True(health.MindwipeScriptPresent);
		Assert.False(health.MindwipeScriptActive);
	}

	[Fact]
	public void ApplyMindwipe_SetsMindwipeScriptPresent()
	{
		var health = new CharacterHealthMsg { MindwipeScriptPresent = false };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "mindwipe", Amount = 30f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(health.MindwipeScriptPresent);
		Assert.False(health.MindwipeScriptActive);
	}

	[Fact]
	public void ApplySleepingPills_AddsComponentAmount()
	{
		var health = new CharacterHealthMsg { SleepingPillsAmount = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "sleepingpills", Amount = 5f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.SleepingPillsAmount - 300f) < 0.001f);
	}

	[Fact]
	public void ApplyNaltrexone_AdjustsPainkillerComponentsAndHappiness()
	{
		var health = new CharacterHealthMsg
		{
			Happiness = 10f,
			AntagonistAmount = 0f,
			OpiateTolerance = 100f,
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "naltrexone", Amount = 20f } };

		RemoteDrinkMedicineApplication.Apply(health, plan);

		Assert.True(Math.Abs(health.Happiness - 8f) < 0.001f);
		Assert.True(Math.Abs(health.AntagonistAmount - 15f) < 0.001f);
		Assert.True(Math.Abs(health.OpiateTolerance - 85f) < 0.001f);
	}

	[Fact]
	public void BuildTimedEffects_Antirad_ProducesScaledDuration()
	{
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "antirad", Amount = 20f } };

		var effects = RemoteDrinkMedicineApplication.BuildTimedEffects(plan);

		var effect = Assert.Single(effects);
		Assert.Equal("antirad", effect.EffectId);
		Assert.True(Math.Abs(effect.DurationSeconds - 90f) < 0.001f);
		Assert.True(Math.Abs(effect.DoseMl - 20f) < 0.001f);
	}

	[Fact]
	public void BuildTimedEffects_Braingrow_ProducesConstantDurationAndDose()
	{
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "braingrow", Amount = 20f } };

		var effects = RemoteDrinkMedicineApplication.BuildTimedEffects(plan);

		var effect = Assert.Single(effects);
		Assert.Equal("braingrow", effect.EffectId);
		Assert.True(Math.Abs(effect.DurationSeconds - 100f) < 0.001f);
		Assert.True(Math.Abs(effect.DoseMl - 20f) < 0.001f);
	}

	[Fact]
	public void BuildTimedEffects_Antidepressants_ProducesOneShotWithDose()
	{
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "antidepressants", Amount = 20f } };

		var effects = RemoteDrinkMedicineApplication.BuildTimedEffects(plan);

		var effect = Assert.Single(effects);
		Assert.Equal("antidepressants", effect.EffectId);
		Assert.Equal(0f, effect.DurationSeconds);
		Assert.True(Math.Abs(effect.DoseMl - 20f) < 0.001f);
	}

	[Fact]
	public void MindwipeBlocked_OnlyWhenTargetMentallyHealthy()
	{
		var healthy = new CharacterHealthMsg { Happiness = 0f, BrainHealth = 95f, StrokeAmount = 0f };
		Assert.True(RemoteDrinkMedicineCatalog.IsMindwipeBlocked("mindwipe", healthy));

		var unhappy = new CharacterHealthMsg { Happiness = -60f, BrainHealth = 95f, StrokeAmount = 0f };
		Assert.False(RemoteDrinkMedicineCatalog.IsMindwipeBlocked("mindwipe", unhappy));
	}
}
