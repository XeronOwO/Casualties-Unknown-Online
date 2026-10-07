using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cross-player SOLID-food slice's L0 half: the curated table answers which
/// items the consume chain carries and what one of them does to a body snapshot.
/// <para>
/// The DRINK cases this file used to hold are gone with the catalog's liquid
/// table (their dispositions are listed in the cycle's self-check): the drain is
/// now <c>LiquidDrainPlan</c>'s, pinned by
/// <c>ConsumeSemanticsTests.TheHostCapsTheDoseAtWhatTheItemReallyCarries</c>, and
/// the per-liquid effect is the game's own <c>onDrink</c> delegate, run on the
/// affected side, which no L0 case can reach (it needs the game's <c>Body</c>).
/// Solid food still has this table because its native shape is different — its
/// <c>useAction</c> writes the eating body and the item directly.
/// </para>
/// </summary>
public sealed class RemoteConsumeApplicationTests
{
	[Fact]
	public void ApplyFood_AppliesBreadEffect()
	{
		var health = new CharacterHealthMsg { Hunger = 50f, Thirst = 80f, WeightOffset = 0f, Happiness = 0f };
		var effect = new RemoteFoodEffect("bread", 0.34f, Hunger: 9f, Thirst: 2f, WeightOffset: 0.5f);

		RemoteConsumeApplication.ApplyFood(health, effect);

		Assert.True(Math.Abs(health.Hunger - 59f) < 0.001f);
		Assert.True(Math.Abs(health.Thirst - 82f) < 0.001f);
		Assert.True(Math.Abs(health.WeightOffset - 0.5f) < 0.001f);
	}

	[Fact]
	public void Catalog_ExposesTheCuratedFoodItems()
	{
		// The catalog answers for SOLID food only now: `waterbottle` was never a
		// food row, and the drink it used to be answered by this type is the game's
		// own ItemInfo.usable flag (ConsumeSemanticsTests).
		Assert.True(RemoteConsumeCatalog.IsFoodItem("bread"));
		Assert.True(RemoteConsumeCatalog.IsFoodItem("nutrientbar"));
		Assert.False(RemoteConsumeCatalog.IsFoodItem("waterbottle"));
	}
}
