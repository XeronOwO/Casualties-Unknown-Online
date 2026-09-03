using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed item content payload contract: a mod can serialize a
/// <see cref="ModItemDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without a private format.
/// </summary>
public class ModItemDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModItemDefinition
		{
			DisplayName = "Test Shard",
			Description = "A brittle shard.",
			Category = "misc",
			Weight = 0.5f,
			Value = 7,
			Usable = true,
			UsableWithLmb = true,
			Wearable = false,
			DestroyAtZeroCondition = true,
			Tags = "test,shard",
			SpawnFrequency = 3,
			TemplateId = "stone",
			SpawnComponents = ["Example.ShardBehaviour, ExampleMod"],
			WorldSpawnPerChunk = 0.5f,
			DropSources = ModItemDropSource.Corpse | ModItemDropSource.Trader1,
			DecayMinutes = 90f,
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModItemDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(original.Category, restored.Category);
		Assert.Equal(original.Weight, restored.Weight);
		Assert.Equal(original.Value, restored.Value);
		Assert.True(restored.Usable);
		Assert.True(restored.UsableWithLmb);
		Assert.False(restored.Wearable);
		Assert.True(restored.DestroyAtZeroCondition);
		Assert.Equal(original.Tags, restored.Tags);
		Assert.Equal(original.SpawnFrequency, restored.SpawnFrequency);
		Assert.Equal(original.TemplateId, restored.TemplateId);
		Assert.Equal(original.SpawnComponents, restored.SpawnComponents);
		Assert.Equal(original.WorldSpawnPerChunk, restored.WorldSpawnPerChunk);
		Assert.Equal(original.DropSources, restored.DropSources);
		Assert.Equal(original.DecayMinutes, restored.DecayMinutes);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void RoundTrip_PreservesAdvancedBehaviorFields()
	{
		var original = new ModItemDefinition
		{
			Container = new ModItemContainer
			{
				Capacity = 20f,
				MaxWeightPerItem = 7f,
				EncumbranceReduction = 0.5f,
				ItemsVisible = true,
				TagRestriction = ["tool", "medical"]
			},
			Battery = new ModItemBattery
			{
				Preset = ModBatteryPreset.Large,
				StartCharge = 0.75f,
				SpawnWithBattery = false
			},
			Light = new ModItemLight
			{
				Intensity = 1.2f,
				ColorR = 0.1f,
				ColorG = 0.2f,
				ColorB = 0.3f,
				ColorA = 0.4f,
				FalloffIntensity = 0.6f,
				OuterRadius = 9f,
				InnerRadius = 1f,
				OuterAngle = 270f,
				InnerAngle = 45f,
				LightType = ModLightType.Sprite,
				OffsetX = 2f,
				OffsetY = 3f,
				Rotation = 10f,
				AddLightItem = false
			},
			Tool = new ModItemTool
			{
				Damage = 30f,
				StructuralDamage = 40f,
				AttackCooldownMultiplier = 0.8f,
				Distance = 3f,
				KnockBack = 300f,
				Cooldown = 0.4f,
				AttackAnimation = "CustomSwing",
				StaminaUse = 0.7f,
				Piercing = true,
				SwingSounds = ["Swing1", "Swing2"],
				Volume = 0.6f,
				RotateAmount = 20f,
				PhysicalSwing = false,
				DoAttackAnimation = false,
				MetalMoreDamage = true,
				ConditionLossOnHit = 0.03f
			},
			Gun = new ModItemGun
			{
				AmmoType = ModGunAmmoType.Rifle,
				FiringMode = ModGunFiringMode.Auto,
				FeedType = ModGunFeedType.Mag,
				MagCapacity = 30,
				KnockBack = 5f,
				StructureDamage = 100f,
				AnimalDamage = 200f,
				Loudness = 80f,
				DesiredGasTime = 0.5f,
				ShotsPerFire = 2,
				VerticalSpread = 0.1f,
				ConditionLossPerShot = 0.2f
			}
		};

		var restored = ModItemDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.NotNull(restored!.Container);
		Assert.Equal(original.Container.Capacity, restored.Container.Capacity);
		Assert.Equal(original.Container.MaxWeightPerItem, restored.Container.MaxWeightPerItem);
		Assert.Equal(original.Container.EncumbranceReduction, restored.Container.EncumbranceReduction);
		Assert.True(restored.Container.ItemsVisible);
		Assert.Equal(original.Container.TagRestriction, restored.Container.TagRestriction);
		Assert.NotNull(restored.Battery);
		Assert.Equal(ModBatteryPreset.Large, restored.Battery.Preset);
		Assert.Equal(0.75f, restored.Battery.StartCharge);
		Assert.False(restored.Battery.SpawnWithBattery);
		Assert.NotNull(restored.Light);
		Assert.Equal(1.2f, restored.Light.Intensity);
		Assert.Equal(0.1f, restored.Light.ColorR);
		Assert.Equal(0.6f, restored.Light.FalloffIntensity);
		Assert.Equal(ModLightType.Sprite, restored.Light.LightType);
		Assert.False(restored.Light.AddLightItem);
		Assert.NotNull(restored.Tool);
		Assert.Equal(30f, restored.Tool.Damage);
		Assert.True(restored.Tool.Piercing);
		Assert.Equal(["Swing1", "Swing2"], restored.Tool.SwingSounds);
		Assert.NotNull(restored.Gun);
		Assert.Equal(ModGunAmmoType.Rifle, restored.Gun.AmmoType);
		Assert.Equal(ModGunFiringMode.Auto, restored.Gun.FiringMode);
		Assert.Equal(ModGunFeedType.Mag, restored.Gun.FeedType);
		Assert.Equal(30, restored.Gun.MagCapacity);
		Assert.Equal(0.2f, restored.Gun.ConditionLossPerShot);
	}

	[Fact]
	public void RoundTrip_PreservesVisualFields()
	{
		var original = new ModItemDefinition
		{
			Visual = new ModItemVisual
			{
				WornSpritePath = "Clothing/TestWorn",
				WornSpriteOffsetX = 1.5f,
				WornSpriteOffsetY = -2.5f,
				WornSpriteSortingOrder = 12,
				LiquidMaskPath = "Containers/TestMask"
			}
		};

		var restored = ModItemDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.NotNull(restored!.Visual);
		Assert.Equal("Clothing/TestWorn", restored.Visual.WornSpritePath);
		Assert.Equal(1.5f, restored.Visual.WornSpriteOffsetX);
		Assert.Equal(-2.5f, restored.Visual.WornSpriteOffsetY);
		Assert.Equal(12, restored.Visual.WornSpriteSortingOrder);
		Assert.Equal("Containers/TestMask", restored.Visual.LiquidMaskPath);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModItemDefinition.FromPayload([]));
		Assert.Null(ModItemDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModItemDefinition.FromPayload(null!));
	}
}
