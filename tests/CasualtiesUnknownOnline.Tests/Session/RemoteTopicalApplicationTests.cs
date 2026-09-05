using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public sealed class RemoteTopicalApplicationTests
{
	[Fact]
	public void Catalog_ExposesKnownTopicalItemsAndLiquids()
	{
		Assert.True(RemoteTopicalCatalog.IsTopicalItem("paincream"));
		Assert.True(RemoteTopicalCatalog.IsTopicalItem("spraybottle"));
		Assert.True(RemoteTopicalCatalog.TryGetTopicalAmount("woundglue", out var amount));
		Assert.True(Math.Abs(amount - 20f) < 0.001f);
		Assert.True(RemoteTopicalCatalog.IsSupportedTopicalLiquid("disinfectant"));
		Assert.True(RemoteTopicalCatalog.IsSupportedTopicalLiquid("soap"));
		Assert.False(RemoteTopicalCatalog.IsSupportedTopicalLiquid("mystery"));
	}

	[Fact]
	public void Plan_DrawsItemAmountOrEntireSmallStack()
	{
		var full = new List<LiquidStackMsg> { new() { LiquidId = "reliefcream", Amount = 100f } };
		Assert.True(RemoteTopicalCatalog.TryCreatePlan(full, "paincream", out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal("reliefcream", drain.LiquidId);
		Assert.True(Math.Abs(drain.Amount - 10f) < 0.001f);

		var small = new List<LiquidStackMsg> { new() { LiquidId = "reliefcream", Amount = 5f } };
		Assert.True(RemoteTopicalCatalog.TryCreatePlan(small, "paincream", out var smallPlan));
		var smallDrain = Assert.Single(smallPlan);
		Assert.True(Math.Abs(smallDrain.Amount - 5f) < 0.001f);
	}

	[Fact]
	public void Plan_RefusesUnknownLiquidEvenForKnownItem()
	{
		var bad = new List<LiquidStackMsg> { new() { LiquidId = "mystery", Amount = 100f } };
		Assert.False(RemoteTopicalCatalog.TryCreatePlan(bad, "paincream", out _));
	}

	[Fact]
	public void ApplyWoundglue_AppliesLimbAndBodyEffects()
	{
		var health = new CharacterHealthMsg
		{
			BloodViscosity = 10f,
			SicknessAmount = 0f,
		};
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 20f, MuscleHealth = 30f, Pain = 50f, InfectionAmount = 20f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "woundglue", Amount = 20f } };

		RemoteTopicalApplication.Apply(health, limbs, plan);

		var limb = limbs[0];
		Assert.True(Math.Abs(limb.SkinHealAmount - 10f) < 0.001f);
		Assert.True(Math.Abs(limb.MuscleHealth - 35f) < 0.001f);
		Assert.True(Math.Abs(limb.BandageSlowAmount - 30f) < 0.001f);
		Assert.True(Math.Abs(limb.InfectionAmount - 15f) < 0.001f);
		Assert.True(Math.Abs(limb.DisinfectionTime - 300f) < 0.001f);
		Assert.True(Math.Abs(limb.Pain - 45f) < 0.001f);
		Assert.True(Math.Abs(health.BloodViscosity - 25f) < 0.001f);
		Assert.True(Math.Abs(health.SicknessAmount - 2.5f) < 0.001f);
	}

	[Fact]
	public void ApplyDisinfectant_UsesMaxNotAdditionForDisinfection()
	{
		var health = new CharacterHealthMsg();
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, Pain = 5f, DisinfectionTime = 500f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "disinfectant", Amount = 10f } };

		RemoteTopicalApplication.Apply(health, limbs, plan);

		Assert.True(Math.Abs(limbs[0].Pain - 15f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].DisinfectionTime - 500f) < 0.001f);
	}

	[Fact]
	public void ApplyReliefcream_RequestedLimbWinsOverMostInjuredAutoPick()
	{
		var health = new CharacterHealthMsg();
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 80f, MuscleHealth = 80f },
			new() { Index = 1, SkinHealth = 5f, MuscleHealth = 5f },
		};
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "reliefcream", Amount = 10f } };

		RemoteTopicalApplication.Apply(health, limbs, plan, requestedLimbIndex: 0);

		Assert.True(Math.Abs(limbs[0].SkinHealAmount - 3f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].DisinfectionTime - 300f) < 0.001f);
		Assert.Equal(0f, limbs[1].SkinHealAmount);
	}

	[Fact]
	public void ApplySoap_ReducesDirtynessAndSetsShortDisinfection()
	{
		var health = new CharacterHealthMsg { Dirtyness = 50f };
		var limbs = new List<CharacterLimbMsg> { new() { Index = 0 } };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "soap", Amount = 10f } };

		RemoteTopicalApplication.Apply(health, limbs, plan);

		Assert.True(Math.Abs(health.Dirtyness - 47.5f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].DisinfectionTime - 3f) < 0.001f);
	}
}
