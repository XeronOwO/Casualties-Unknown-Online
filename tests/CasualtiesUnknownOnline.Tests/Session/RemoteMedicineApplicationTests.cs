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
}
