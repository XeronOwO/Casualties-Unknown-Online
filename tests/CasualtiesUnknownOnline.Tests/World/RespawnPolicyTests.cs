using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The pure revive/respawn rules (RespawnPolicy): Permadeath / trader / next-level
/// gates and the respawn shaping flags (keep inventory/skills). The Unity-facing
/// RespawnCoordinator is a thin shell around this decision surface, so the
/// lifecycle rules are L0-locked without a running game.
/// </summary>
public class RespawnPolicyTests
{
	private static RespawnOptions DefaultRules() => new();

	private static CharacterDataMsg DeadSnapshot() => new()
	{
		OwnerSteamId = 2001,
		Health = new CharacterHealthMsg
		{
			Alive = false,
			Conscious = false,
			BrainHealth = 0f,
			BloodVolume = 20f,
			BloodOxygen = 30f,
		},
		Skills = new CharacterSkillsMsg { Strength = 5, Resistance = 4, Intelligence = 3, ExpStrength = 10f },
		Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 40f, MuscleHealth = 60f },
		],
		Items =
		[
			new CharacterItemMsg { InstanceId = 42, ItemId = "medkit", SlotIndex = 0, Condition = 0.5f },
		],
		Position = new NetVector2Msg(10f, 20f),
	};

	[Fact]
	public void CanUseTraderRecruit_DisabledByPermadeath()
	{
		var rules = DefaultRules();
		rules.Permadeath = true;
		rules.ReviveFromTrader = true;

		Assert.False(RespawnPolicy.CanUseTraderRecruit(rules));
	}

	[Fact]
	public void CanUseTraderRecruit_DisabledByReviveFromTraderFlag()
	{
		var rules = DefaultRules();
		rules.ReviveFromTrader = false;

		Assert.False(RespawnPolicy.CanUseTraderRecruit(rules));
	}

	[Fact]
	public void CanUseTraderRecruit_AllowedByDefaults() =>
		Assert.True(RespawnPolicy.CanUseTraderRecruit(DefaultRules()));

	[Fact]
	public void CanAutoReviveOnNextLevel_DisabledByPermadeath()
	{
		var rules = DefaultRules();
		rules.Permadeath = true;
		rules.ReviveOnNextLevel = true;

		Assert.False(RespawnPolicy.CanAutoReviveOnNextLevel(rules));
	}

	[Fact]
	public void CanAutoReviveOnNextLevel_DisabledByFlag()
	{
		var rules = DefaultRules();
		rules.ReviveOnNextLevel = false;

		Assert.False(RespawnPolicy.CanAutoReviveOnNextLevel(rules));
	}

	[Fact]
	public void CanAutoReviveOnNextLevel_AllowedByDefaults() =>
		Assert.True(RespawnPolicy.CanAutoReviveOnNextLevel(DefaultRules()));

	[Fact]
	public void IsDead_TrueOnlyForNonAliveSnapshot()
	{
		Assert.True(RespawnPolicy.IsDead(DeadSnapshot()));

		var alive = DeadSnapshot();
		alive.Health!.Alive = true;
		Assert.False(RespawnPolicy.IsDead(alive));
		Assert.False(RespawnPolicy.IsDead(null));
	}

	[Fact]
	public void PrepareRespawn_KeepsInventoryAndSkillsByDefault()
	{
		var respawn = RespawnPolicy.PrepareRespawn(DeadSnapshot(), keepInventory: true, keepSkills: true);

		Assert.NotNull(respawn.Health);
		Assert.True(respawn.Health!.Alive);
		Assert.True(respawn.Health.Conscious);
		Assert.Single(respawn.Items);
		Assert.Equal("medkit", respawn.Items[0].ItemId);
		Assert.NotNull(respawn.Skills);
		Assert.Equal(5, respawn.Skills!.Strength);
		Assert.Null(respawn.Position); // respawn lands at the current world's spawn point, not the old layer
	}

	[Fact]
	public void PrepareRespawn_DropsInventoryWhenDisabled()
	{
		var respawn = RespawnPolicy.PrepareRespawn(DeadSnapshot(), keepInventory: false, keepSkills: true);

		Assert.Empty(respawn.Items);
		Assert.Equal(0, respawn.HandSlot);
		Assert.NotNull(respawn.Skills);
	}

	[Fact]
	public void PrepareRespawn_ResetsSkillsWhenDisabled()
	{
		var respawn = RespawnPolicy.PrepareRespawn(DeadSnapshot(), keepInventory: true, keepSkills: false);

		Assert.Single(respawn.Items);
		Assert.NotNull(respawn.Skills);
		Assert.Equal(0, respawn.Skills!.Strength);
		Assert.Equal(0, respawn.Skills.Resistance);
		Assert.Equal(0, respawn.Skills.Intelligence);
		Assert.Equal(0f, respawn.Skills.ExpStrength);
	}
}
