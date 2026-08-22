using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class RemoteHealApplicationTests
{
	[Fact]
	public void PickMostInjuredLimb_ChoosesLowestCombinedAndSkipsDismembered()
	{
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, SkinHealth = 70f, MuscleHealth = 70f },
			new() { Index = 1, SkinHealth = 10f, MuscleHealth = 40f },
			new() { Index = 2, SkinHealth = 5f, MuscleHealth = 5f, Dismembered = true },
		};

		Assert.Equal(1, RemoteHealApplication.PickMostInjuredLimb(limbs));
	}

	[Fact]
	public void PickMostInjuredLimb_EmptyReturnsMinusOne() =>
		Assert.Equal(-1, RemoteHealApplication.PickMostInjuredLimb([]));

	[Fact]
	public void PickMostInjuredLimb_AllDismemberedReturnsMinusOne()
	{
		var limbs = new List<CharacterLimbMsg>
		{
			new() { Index = 0, Dismembered = true },
			new() { Index = 1, Dismembered = true },
		};

		Assert.Equal(-1, RemoteHealApplication.PickMostInjuredLimb(limbs));
	}

	[Fact]
	public void Apply_AddsHealingFieldsAndClampsNonNegative()
	{
		var limb = new CharacterLimbMsg
		{
			Index = 0,
			SkinHealth = 10f,
			MuscleHealth = 20f,
			Pain = 5f,
			BoneHealTimer = 100f,
			DislocationTimer = 100f,
			BleedAmount = 10f,
			SkinHealAmount = 0f,
		};

		RemoteHealApplication.Apply(limb, new RemoteHealProfile(
			"bandage", 1f,
			SkinHealAmount: 30f,
			BandageSlowAmount: 45f,
			Pain: -60f,
			BoneHealTimer: -20f,
			DislocationTimer: -20f,
			BleedAmount: -5f,
			SkinHealth: 5f,
			MuscleHealth: 10f));

		Assert.Equal(30f, limb.SkinHealAmount);
		Assert.Equal(45f, limb.BandageSlowAmount);
		Assert.Equal(0f, limb.Pain);
		Assert.Equal(80f, limb.BoneHealTimer);
		Assert.Equal(80f, limb.DislocationTimer);
		Assert.Equal(5f, limb.BleedAmount);
		Assert.Equal(15f, limb.SkinHealth);
		Assert.Equal(30f, limb.MuscleHealth);
	}

	[Fact]
	public void Profiles_KnownItemSetExists()
	{
		Assert.True(RemoteHealProfiles.IsHealItem("bandage"));
		Assert.True(RemoteHealProfiles.IsHealItem("sterilizedbandage"));
		Assert.False(RemoteHealProfiles.IsHealItem("medkit"));
	}
}
