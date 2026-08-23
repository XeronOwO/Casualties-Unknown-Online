using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The pure trader-recruit rules (TraderRecruitPolicy): trader gates, dead
/// detection and the post-revive physiological state. The Unity-facing
/// coordinator is a thin shell around this decision surface, so the acceptance
/// logic is L0-locked without a running game.
/// </summary>
public class TraderRecruitPolicyTests
{
	private static TradeStockState NewTrader() => new()
	{
		Reputation = 100f,
		Hostility = 0f,
		BuildHealth = 500f,
		Items = [],
	};

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
	public void CanRecruit_AllowsFriendlyHealthyUnusedTrader() =>
		Assert.True(TraderRecruitPolicy.CanRecruit(NewTrader(), used: false));

	[Fact]
	public void CanRecruit_RejectsUsedTrader() =>
		Assert.False(TraderRecruitPolicy.CanRecruit(NewTrader(), used: true));

	[Fact]
	public void CanRecruit_RejectsLowReputation()
	{
		var trader = NewTrader() with { Reputation = TraderRecruitPolicy.MinReputation - 1f };
		Assert.False(TraderRecruitPolicy.CanRecruit(trader, used: false));
	}

	[Fact]
	public void CanRecruit_RejectsDamagedBuild()
	{
		var trader = NewTrader() with { BuildHealth = TraderRecruitPolicy.MinBuildHealth };
		Assert.False(TraderRecruitPolicy.CanRecruit(trader, used: false));
	}

	[Fact]
	public void CanRecruit_RejectsHostileTrader()
	{
		var trader = NewTrader() with { Hostility = 10f };
		Assert.False(TraderRecruitPolicy.CanRecruit(trader, used: false));
	}

	[Fact]
	public void IsDead_TrueOnlyWhenSnapshotSaysNotAlive()
	{
		Assert.True(TraderRecruitPolicy.IsDead(DeadSnapshot()));

		var alive = DeadSnapshot();
		alive.Health!.Alive = true;
		Assert.False(TraderRecruitPolicy.IsDead(alive));
		Assert.False(TraderRecruitPolicy.IsDead(null));
	}

	[Fact]
	public void PrepareRevive_RestoresLifeAndKeepsInventoryAndLimbs()
	{
		var revived = TraderRecruitPolicy.PrepareRevive(DeadSnapshot());

		Assert.NotNull(revived.Health);
		Assert.True(revived.Health!.Alive);
		Assert.True(revived.Health.Conscious);
		Assert.True(revived.Health.BrainHealth > 0f);
		Assert.True(revived.Health.BloodVolume > 0f);
		Assert.True(revived.Health.BloodOxygen > 0f);
		Assert.Single(revived.Items);
		Assert.Equal(42UL, revived.Items[0].InstanceId);
		Assert.Single(revived.Limbs);
		Assert.Equal(0, revived.Limbs[0].Index);
		Assert.Equal(40f, revived.Limbs[0].SkinHealth);
		Assert.Equal(10f, revived.Position!.X);
		Assert.Equal(20f, revived.Position.Y);
	}
}
