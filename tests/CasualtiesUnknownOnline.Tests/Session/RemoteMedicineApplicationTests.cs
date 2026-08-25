using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public sealed class RemoteMedicineApplicationTests
{
	[Fact]
	public void Catalog_ExposesKnownMedicineItemsAndLiquids()
	{
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("saline"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("bloodbag"));
		Assert.True(RemoteMedicineCatalog.TryGetInjectionAmount("antiserum", out var amount));
		Assert.True(Math.Abs(amount - 50f) < 0.001f);
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("water"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("saline"));
		Assert.False(RemoteMedicineCatalog.IsSupportedMedicineLiquid("mystery"));
	}

	[Fact]
	public void Catalog_ExposesOpiateItemsAndLiquids()
	{
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("morphine"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("opium"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("heroin"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("fentanyl"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("naloxone"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("morphine"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("heroin"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("fentanyl"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("naloxone"));
	}

	[Fact]
	public void Plan_DrawsOpiateItemAmountOrEntireSmallStack()
	{
		var full = new List<LiquidStackMsg>
		{
			new() { LiquidId = "morphine", Amount = 100f },
		};
		Assert.True(RemoteMedicineCatalog.TryCreatePlan(full, "morphine", out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal("morphine", drain.LiquidId);
		Assert.True(Math.Abs(drain.Amount - 100f) < 0.001f);

		var small = new List<LiquidStackMsg> { new() { LiquidId = "morphine", Amount = 30f } };
		Assert.True(RemoteMedicineCatalog.TryCreatePlan(small, "morphine", out var smallPlan));
		var smallDrain = Assert.Single(smallPlan);
		Assert.True(Math.Abs(smallDrain.Amount - 30f) < 0.001f);
	}

	[Fact]
	public void ApplyMorphine_AddsOpiateAmount()
	{
		var health = new CharacterHealthMsg { OpiateAmount = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "morphine", Amount = 100f } };

		RemoteMedicineApplication.Apply(health, [], plan);

		Assert.True(Math.Abs(health.OpiateAmount - 90f) < 0.001f);
	}

	[Fact]
	public void ApplyHeroin_AddsOpiateAndSickness()
	{
		var health = new CharacterHealthMsg { OpiateAmount = 0f, SicknessAmount = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "heroin", Amount = 100f } };

		RemoteMedicineApplication.Apply(health, [], plan);

		Assert.True(Math.Abs(health.OpiateAmount - 130f) < 0.001f);
		Assert.True(Math.Abs(health.SicknessAmount - 50f) < 0.001f);
	}

	[Fact]
	public void ApplyNaloxone_AddsAntagonistAmount()
	{
		var health = new CharacterHealthMsg { AntagonistAmount = 0f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "naloxone", Amount = 100f } };

		RemoteMedicineApplication.Apply(health, [], plan);

		Assert.True(Math.Abs(health.AntagonistAmount - 50f) < 0.001f);
	}

	[Fact]
	public void Plan_DrawsItemAmountOrEntireSmallStack()
	{
		var full = new List<LiquidStackMsg> { new() { LiquidId = "saline", Amount = 750f } };
		Assert.True(RemoteMedicineCatalog.TryCreatePlan(full, "saline", out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal("saline", drain.LiquidId);
		Assert.True(Math.Abs(drain.Amount - 80f) < 0.001f);

		var small = new List<LiquidStackMsg> { new() { LiquidId = "saline", Amount = 50f } };
		Assert.True(RemoteMedicineCatalog.TryCreatePlan(small, "saline", out var smallPlan));
		var smallDrain = Assert.Single(smallPlan);
		Assert.True(Math.Abs(smallDrain.Amount - 50f) < 0.001f);
	}

	[Fact]
	public void Plan_RefusesUnknownLiquidEvenForKnownItem()
	{
		var bad = new List<LiquidStackMsg> { new() { LiquidId = "mystery", Amount = 750f } };
		Assert.False(RemoteMedicineCatalog.TryCreatePlan(bad, "saline", out _));
	}

	[Fact]
	public void ApplySaline_AppliesBloodVolumeViscosityAndThirst()
	{
		var health = new CharacterHealthMsg
		{
			BloodVolume = 100f,
			BloodViscosity = 10f,
			Thirst = 50f,
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "saline", Amount = 80f } };

		RemoteMedicineApplication.Apply(health, [], plan);

		Assert.True(Math.Abs(health.BloodVolume - 104.2666667f) < 0.001f);
		Assert.True(Math.Abs(health.BloodViscosity - 4.6666667f) < 0.001f);
		Assert.True(Math.Abs(health.Thirst - 57.4666667f) < 0.001f);
	}

	[Fact]
	public void ApplyAntiserum_AppliesBodyAndMostInjuredLimbDisinfection()
	{
		var health = new CharacterHealthMsg
		{
			BloodVolume = 100f,
			SepticShock = 50f,
			AntibioticImmunityTime = 0f,
		};
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 70f, MuscleHealth = 70f },
			new() { Index = 1, SkinHealth = 20f, MuscleHealth = 30f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "antiserum", Amount = 50f } };

		RemoteMedicineApplication.Apply(health, limbs, plan);

		Assert.True(Math.Abs(health.SepticShock - 40f) < 0.001f);
		Assert.True(Math.Abs(health.BloodVolume - 103f) < 0.001f);
		Assert.True(Math.Abs(health.AntibioticImmunityTime - 300f) < 0.001f);
		Assert.True(Math.Abs(limbs[1].DisinfectionTime - 180f) < 0.001f);
	}

	[Fact]
	public void ApplyAntiserum_PreservesHigherExistingDisinfection()
	{
		var health = new CharacterHealthMsg
		{
			BloodVolume = 100f,
			SepticShock = 50f,
			AntibioticImmunityTime = 0f,
		};
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 70f, MuscleHealth = 70f, DisinfectionTime = 500f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "antiserum", Amount = 50f } };

		RemoteMedicineApplication.Apply(health, limbs, plan);

		Assert.True(Math.Abs(limbs[0].DisinfectionTime - 500f) < 0.001f);
	}

	[Fact]
	public void ApplyCeftriaxone_IncreasesImmunityAndLimbPain()
	{
		var health = new CharacterHealthMsg { AntibioticImmunityTime = 0f };
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, Pain = 10f, SkinHealth = 50f, MuscleHealth = 50f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "ceftriaxone", Amount = 100f } };

		RemoteMedicineApplication.Apply(health, limbs, plan);

		Assert.True(Math.Abs(health.AntibioticImmunityTime - 1125f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].Pain - 90f) < 0.001f);
	}

	[Fact]
	public void ApplyRedblood_AppliesHarmfulEffectsToSelectedLimb()
	{
		var health = new CharacterHealthMsg
		{
			BloodVolume = 100f,
			SicknessAmount = 0f,
			SepticShock = 0f,
		};
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, MuscleHealth = 80f, SkinHealth = 80f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "redblood", Amount = 375f } };

		RemoteMedicineApplication.Apply(health, limbs, plan);

		Assert.True(Math.Abs(health.BloodVolume - 115f) < 0.001f);
		Assert.True(Math.Abs(health.SicknessAmount - 25f) < 0.001f);
		Assert.True(Math.Abs(health.SepticShock - 20f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].MuscleHealth - 65f) < 0.001f);
	}

	[Fact]
	public void Catalog_ExposesTimedMedicineItemsAndLiquids()
	{
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("bloodcoagulant"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("combatpen"));
		Assert.True(RemoteMedicineCatalog.IsInjectableItem("syringe"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("procoagulant"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("highgradestimulant"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("midgradestimulant"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("lowgradestimulant"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("epinephrine"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("oxyline"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("chloroform"));
		Assert.True(RemoteMedicineCatalog.IsSupportedMedicineLiquid("amiodarone"));
	}

	[Fact]
	public void BuildTimedEffects_CombatPen_ProducesScaledBodyEffects()
	{
		var plan = new List<LiquidStackMsg>
		{
			new() { LiquidId = "highgradestimulant", Amount = 60f },
			new() { LiquidId = "epinephrine", Amount = 15f },
			new() { LiquidId = "oxyline", Amount = 25f },
		};

		var effects = RemoteMedicineApplication.BuildTimedEffects(plan);

		Assert.Equal(3, effects.Count);
		Assert.Equal("highgradestimulant", effects[0].EffectId);
		Assert.True(Math.Abs(effects[0].DurationSeconds - 144f) < 0.001f);
		Assert.Equal("epinephrine", effects[1].EffectId);
		Assert.True(Math.Abs(effects[1].DurationSeconds - 90f) < 0.001f);
		Assert.Equal("oxyline", effects[2].EffectId);
		Assert.True(Math.Abs(effects[2].DurationSeconds - 50f) < 0.001f);
	}

	[Fact]
	public void BuildTimedEffects_BloodCoagulant_ProducesScaledProcoagulantEffect()
	{
		var plan = new List<LiquidStackMsg>
		{
			new() { LiquidId = "procoagulant", Amount = 33.334f },
		};

		var effects = RemoteMedicineApplication.BuildTimedEffects(plan);

		var effect = Assert.Single(effects);
		Assert.Equal("procoagulant", effect.EffectId);
		Assert.True(Math.Abs(effect.DurationSeconds - 20f) < 0.01f);
	}

	[Fact]
	public void BuildTimedEffects_ImmediateOnlyLiquid_ReturnsEmpty()
	{
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "saline", Amount = 80f } };

		Assert.Empty(RemoteMedicineApplication.BuildTimedEffects(plan));
	}
}
