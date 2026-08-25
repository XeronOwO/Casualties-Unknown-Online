using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public sealed class RemoteLimbToolApplicationTests
{
	[Fact]
	public void Catalog_ExposesKnownLimbToolsAndRefusesUnknown()
	{
		Assert.True(RemoteLimbToolCatalog.IsToolItem("boneweldingtool"));
		Assert.True(RemoteLimbToolCatalog.IsToolItem("clottingmush"));
		Assert.True(RemoteLimbToolCatalog.IsToolItem("chestdrain"));
		Assert.True(RemoteLimbToolCatalog.IsToolItem("musharm"));
		Assert.False(RemoteLimbToolCatalog.IsToolItem("mysterytool"));
		Assert.True(RemoteLimbToolCatalog.TryGet("boneweldingtool", out _));
	}

	[Fact]
	public void ApplyBoneweldingtool_AppliesLimbAndBodyEffects()
	{
		var health = new CharacterHealthMsg { BloodViscosity = 5f };
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 50f, MuscleHealth = 50f, Pain = 10f, BleedAmount = 5f, BoneHealTimer = 100f },
		};
		Assert.True(RemoteLimbToolCatalog.TryGet("boneweldingtool", out var profile));

		Assert.True(RemoteLimbToolApplication.TryApply(health, limbs, profile, out _));

		Assert.True(Math.Abs(limbs[0].SkinHealth - 25f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].MuscleHealth - 24f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].Pain - 40f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].BleedAmount - 10f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].BoneHealTimer - 25f) < 0.001f);
		Assert.True(Math.Abs(health.BloodViscosity - 7f) < 0.001f);
	}

	[Fact]
	public void ApplyClottingmush_ReducesBleedAndRaisesViscosity()
	{
		var health = new CharacterHealthMsg { BloodViscosity = 5f };
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 50f, MuscleHealth = 50f, BleedAmount = 20f },
		};
		Assert.True(RemoteLimbToolCatalog.TryGet("clottingmush", out var profile));

		Assert.True(RemoteLimbToolApplication.TryApply(health, limbs, profile, out _));

		Assert.True(Math.Abs(limbs[0].BleedAmount - 12f) < 0.001f);
		Assert.True(Math.Abs(health.BloodViscosity - 15f) < 0.001f);
	}

	[Fact]
	public void ApplyChestdrain_ReducesHemothoraxOnChestLimb()
	{
		var health = new CharacterHealthMsg { Hemothorax = 50f };
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, BleedAmount = 0f },
			new() { Index = 1, BleedAmount = 0f },
		};
		Assert.True(RemoteLimbToolCatalog.TryGet("chestdrain", out var profile));

		Assert.True(RemoteLimbToolApplication.TryApply(health, limbs, profile, out var limbIndex));
		Assert.Equal(1, limbIndex);
		Assert.True(Math.Abs(limbs[1].BleedAmount - 2f) < 0.001f);
		Assert.True(Math.Abs(health.Hemothorax - 15f) < 0.001f);
	}

	[Fact]
	public void ApplyChestdrain_MissingChestLimb_ReturnsFalse()
	{
		var health = new CharacterHealthMsg { Hemothorax = 50f };
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, BleedAmount = 0f },
		};
		Assert.True(RemoteLimbToolCatalog.TryGet("chestdrain", out var profile));

		Assert.False(RemoteLimbToolApplication.TryApply(health, limbs, profile, out _));
	}

	[Fact]
	public void ApplyMusharm_AddsSkinHealAndBandageSlow()
	{
		var health = new CharacterHealthMsg();
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
		};
		Assert.True(RemoteLimbToolCatalog.TryGet("musharm", out var profile));

		Assert.True(RemoteLimbToolApplication.TryApply(health, limbs, profile, out _));

		Assert.True(Math.Abs(limbs[0].SkinHealAmount - 8f) < 0.001f);
		Assert.True(Math.Abs(limbs[0].BandageSlowAmount - 10f) < 0.001f);
	}
}
