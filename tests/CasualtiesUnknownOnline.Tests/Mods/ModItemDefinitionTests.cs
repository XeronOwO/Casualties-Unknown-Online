using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed item definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Item"/>), the collection
/// members where null means "none" and the defaults the definition and the
/// member objects it carries declare. Nothing serializes a definition any more,
/// so this suite pins the answers the definition itself owns — a wrong kind
/// constant, a member that stops coalescing null or a changed authoring default
/// fails here.
/// </summary>
public class ModItemDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModItemDefinition();

		Assert.Equal(ModContentKind.Item, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModItemDefinition { Id = "test.shard", SchemaVersion = 3 };

		Assert.Equal("test.shard", authored.Id);
		Assert.Equal(3, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModItemDefinition
		{
			SpawnComponents = null!,
			CustomData = null!,
			Qualities = null!,
			Container = new ModItemContainer { TagRestriction = null! },
			Tool = new ModItemTool { SwingSounds = null! },
			Visual = new ModItemVisual
			{
				MultiWornSprites = null!,
				BaseSpriteAnimation = new ModItemSpriteAnimation { FramePaths = null! },
				WornSpriteAnimation = new ModItemSpriteAnimation { FramePaths = null! },
				LiquidMaskAnimation = new ModItemSpriteAnimation { FramePaths = null! }
			}
		};

		var container = definition.Container!;
		var tool = definition.Tool!;
		var visual = definition.Visual!;

		Assert.Empty(definition.SpawnComponents);
		Assert.Empty(definition.CustomData);
		Assert.Empty(definition.Qualities);
		Assert.Empty(container.TagRestriction);
		Assert.Empty(tool.SwingSounds);
		Assert.Empty(visual.MultiWornSprites);
		Assert.Empty(visual.BaseSpriteAnimation!.FramePaths);
		Assert.Empty(visual.WornSpriteAnimation!.FramePaths);
		Assert.Empty(visual.LiquidMaskAnimation!.FramePaths);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModItemDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Equal("nospawn", definition.Category);
		Assert.Empty(definition.Tags);
		Assert.Equal(1, definition.SpawnFrequency);
		Assert.Empty(definition.TemplateId);
	}

	[Fact]
	public void ContainerDefaults_HoldOnAFreshContainer()
	{
		var container = new ModItemContainer();

		Assert.Equal(10f, container.Capacity);
		Assert.Equal(5f, container.MaxWeightPerItem);
		Assert.Equal(1f, container.EncumbranceReduction);
	}

	[Fact]
	public void BatteryDefaults_HoldOnAFreshBattery()
	{
		var battery = new ModItemBattery();

		Assert.Equal(ModBatteryPreset.Medium, battery.Preset);
		Assert.Equal(-1f, battery.StartCharge);
		Assert.True(battery.SpawnWithBattery);
	}

	[Fact]
	public void LightDefaults_HoldOnAFreshLight()
	{
		var light = new ModItemLight();

		Assert.Equal(0.75f, light.Intensity);
		Assert.Equal(1f, light.ColorR);
		Assert.Equal(1f, light.ColorG);
		Assert.Equal(1f, light.ColorB);
		Assert.Equal(1f, light.ColorA);
		Assert.Equal(0.5f, light.FalloffIntensity);
		Assert.Equal(7.5f, light.OuterRadius);
		Assert.Equal(360f, light.OuterAngle);
		Assert.Equal(360f, light.InnerAngle);
		Assert.Equal(ModLightType.Point, light.LightType);
		Assert.True(light.AddLightItem);
	}

	[Fact]
	public void ToolDefaults_HoldOnAFreshTool()
	{
		var tool = new ModItemTool();

		Assert.Equal(25f, tool.Damage);
		Assert.Equal(25f, tool.StructuralDamage);
		Assert.Equal(0.66f, tool.AttackCooldownMultiplier);
		Assert.Equal(2.5f, tool.Distance);
		Assert.Equal(270f, tool.KnockBack);
		Assert.Equal(0.35f, tool.Cooldown);
		Assert.Equal("SwingAnim", tool.AttackAnimation);
		Assert.Equal(0.5f, tool.StaminaUse);
		Assert.Equal(["BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4"], tool.SwingSounds);
		Assert.Equal(0.5f, tool.Volume);
		Assert.Equal(15.5f, tool.RotateAmount);
		Assert.True(tool.PhysicalSwing);
		Assert.True(tool.DoAttackAnimation);
		Assert.Equal(0.02f, tool.ConditionLossOnHit);
	}

	[Fact]
	public void VisualDefaults_HoldOnAFreshVisual()
	{
		var visual = new ModItemVisual();

		Assert.Empty(visual.WornSpritePath);
		Assert.Empty(visual.LiquidMaskPath);

		var animation = new ModItemSpriteAnimation();
		Assert.Equal(12f, animation.FramesPerSecond);
		Assert.True(animation.Loop);

		var wornSprite = new ModItemLimbWornSprite();
		Assert.Empty(wornSprite.LimbName);
		Assert.Empty(wornSprite.SpritePath);
	}

	[Fact]
	public void CraftingQualityDefaults_HoldOnAFreshQuality()
	{
		// The declared 1 is what a quality-based recipe asks for when the author
		// leaves the amount out; the Game Adapter normalises a non-positive
		// amount, not this DTO.
		var quality = new ModCraftingQuality { Id = "rippable" };

		Assert.Equal(1f, quality.Amount);
	}
}
