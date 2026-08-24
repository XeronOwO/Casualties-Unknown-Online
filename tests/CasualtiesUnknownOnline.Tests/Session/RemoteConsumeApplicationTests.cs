using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public sealed class RemoteConsumeApplicationTests
{
	[Fact]
	public void DrinkPlan_TakesFullAmountFromFullContainer()
	{
		var item = new CharacterItemMsg
		{
			ItemId = "waterbottle",
			Condition = 1f,
			Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 500f }],
		};

		Assert.True(RemoteConsumeApplication.TryCreateDrinkPlan(item.Liquids, out var plan));
		var drink = Assert.Single(plan);
		Assert.Equal("water", drink.LiquidId);
		Assert.True(Math.Abs(drink.Amount - 100f) < 0.001f);
	}

	[Fact]
	public void DrinkPlan_TakesEntireSmallContainer()
	{
		var item = new CharacterItemMsg
		{
			ItemId = "waterbottle",
			Condition = 0.1f,
			Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 50f }],
		};

		Assert.True(RemoteConsumeApplication.TryCreateDrinkPlan(item.Liquids, out var plan));
		var drink = Assert.Single(plan);
		Assert.True(Math.Abs(drink.Amount - 50f) < 0.001f);
	}

	[Fact]
	public void DrinkPlan_RefusesUnknownLiquid()
	{
		var item = new CharacterItemMsg
		{
			Liquids = [new LiquidStackMsg { LiquidId = "mystery", Amount = 500f }],
		};

		Assert.False(RemoteConsumeApplication.TryCreateDrinkPlan(item.Liquids, out _));
	}

	[Fact]
	public void ApplyDrink_AppliesWaterThirstAndTemperature()
	{
		var health = new CharacterHealthMsg { Thirst = 50f, Temperature = 37f };
		var plan = new List<LiquidStackMsg> { new() { LiquidId = "water", Amount = 100f } };

		RemoteConsumeApplication.ApplyDrink(health, plan);

		Assert.True(Math.Abs(health.Thirst - 59f) < 0.001f);
		Assert.True(Math.Abs(health.Temperature - 36.75f) < 0.001f);
	}

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
	public void Catalog_ExposesCuratedFoodAndLiquids()
	{
		Assert.True(RemoteConsumeCatalog.IsFoodItem("bread"));
		Assert.True(RemoteConsumeCatalog.IsFoodItem("nutrientbar"));
		Assert.True(RemoteConsumeCatalog.IsKnownLiquid("water"));
		Assert.True(RemoteConsumeCatalog.IsKnownLiquid("coffee"));
		Assert.False(RemoteConsumeCatalog.IsFoodItem("waterbottle"));
		Assert.False(RemoteConsumeCatalog.IsKnownLiquid("mystery"));
	}
}
