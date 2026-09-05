using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the pure owner-side mouth-sprite decision that the character snapshot
/// carries to peer render clones. The rule mirrors the game's own
/// <c>FacialExpression.Update</c> head-sprite branch; the remote clone must
/// replay this exact decision instead of deriving it from frozen-proxy inputs.
/// </summary>
public class HeadMouthRuleTests
{
	[Fact]
	public void Evaluate_Closed_WhenNoMouthTrigger()
	{
		Assert.Equal(HeadMouthState.Closed, HeadMouthRule.Evaluate(
			disfigured: false,
			eatTime: 0f,
			holdingMouthItem: false,
			headDislocated: false));
	}

	[Fact]
	public void Evaluate_HalfOpen_WhenShortEatTimeOnly()
	{
		Assert.Equal(HeadMouthState.HalfOpen, HeadMouthRule.Evaluate(
			disfigured: false,
			eatTime: 0.1f,
			holdingMouthItem: false,
			headDislocated: false));
	}

	[Theory]
	[InlineData(0.2f, false, false)]
	[InlineData(0f, true, false)]
	[InlineData(0f, false, true)]
	public void Evaluate_Open_WhenAnyOpenTrigger(float eatTime, bool holdingMouthItem, bool headDislocated)
	{
		Assert.Equal(HeadMouthState.Open, HeadMouthRule.Evaluate(
			disfigured: false,
			eatTime: eatTime,
			holdingMouthItem: holdingMouthItem,
			headDislocated: headDislocated));
	}

	[Fact]
	public void Evaluate_Closed_WhenDisfiguredEvenWithOpenTriggers()
	{
		Assert.Equal(HeadMouthState.Closed, HeadMouthRule.Evaluate(
			disfigured: true,
			eatTime: 1f,
			holdingMouthItem: true,
			headDislocated: true));
	}

	[Fact]
	public void Refresh_SlotTwoItem_RecomputesOpenMouth()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { EatTime = 0f, HeadMouth = HeadMouthState.Closed },
			Items = [new CharacterItemMsg { InstanceId = 1, SlotIndex = 2 }],
		};

		HeadMouthRule.Refresh(data);

		Assert.Equal(HeadMouthState.Open, data.Health!.HeadMouth);
	}

	[Fact]
	public void Refresh_HeadLimbDislocated_RecomputesOpenMouth()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { EatTime = 0f, HeadMouth = HeadMouthState.Closed },
			Limbs = [new CharacterLimbMsg { Index = 0, Dislocated = true }],
		};

		HeadMouthRule.Refresh(data);

		Assert.Equal(HeadMouthState.Open, data.Health!.HeadMouth);
	}

	[Fact]
	public void Refresh_EatTimeHalf_RecomputesHalfOpenMouth()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { EatTime = 0.1f, HeadMouth = HeadMouthState.Closed },
		};

		HeadMouthRule.Refresh(data);

		Assert.Equal(HeadMouthState.HalfOpen, data.Health!.HeadMouth);
	}

	[Fact]
	public void Refresh_RemovedSlotTwoItem_RecomputesClosedMouth()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { EatTime = 0f, HeadMouth = HeadMouthState.Open },
			Items = [],
		};

		HeadMouthRule.Refresh(data);

		Assert.Equal(HeadMouthState.Closed, data.Health!.HeadMouth);
	}
}
