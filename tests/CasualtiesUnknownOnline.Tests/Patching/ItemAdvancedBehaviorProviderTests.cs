using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter item provider's advanced behavior contract: container,
/// battery, light, tool and gun DTOs must map onto the vanilla
/// <c>ItemInfo</c> surface (tool/gun use action flags, battery decay) and be
/// validated before acceptance. The test project never compile-references
/// GameAdapter, so this locks the stable static-mapping behavior reflectively.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class ItemAdvancedBehaviorProviderTests
{
	private static Type ProviderType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterItemContentProvider",
		throwOnError: true)!;

	private static object CreateProvider()
	{
		var loggerType = typeof(NullLogger<>).MakeGenericType(ProviderType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		return Activator.CreateInstance(ProviderType, [logger])!;
	}

	private static bool TryBind(object provider, IModContentDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration("mod.a", definition);
		return (bool)bind.Invoke(provider, [registration])!;
	}

	private static void PrepareGameTables()
	{
		var itemType = GameAssemblyHost.ResolveType("Item")
			?? throw new InvalidOperationException("Item not found in game assembly.");
		var itemInfoType = GameAssemblyHost.ResolveType("ItemInfo")
			?? throw new InvalidOperationException("ItemInfo not found in game assembly.");
		var itemDictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), itemInfoType);
		itemType.GetField("GlobalItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
			.SetValue(null, Activator.CreateInstance(itemDictType)!);
	}

	private static void InvokeUpdate(object provider)
	{
		var update = provider.GetType().GetMethod(
			"Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Update not found.");
		update.Invoke(provider, null);
	}

	private static object GetItemInfo(string id)
	{
		var itemType = GameAssemblyHost.ResolveType("Item")
			?? throw new InvalidOperationException("Item not found in game assembly.");
		var itemInfoType = GameAssemblyHost.ResolveType("ItemInfo")
			?? throw new InvalidOperationException("ItemInfo not found in game assembly.");
		var itemDictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), itemInfoType);
		var dict = (IDictionary)itemType.GetField(
			"GlobalItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
		return dict[id]!;
	}

	private static bool GetBool(object info, string name) =>
		(bool)info.GetType().GetField(name)!.GetValue(info)!;

	private static byte GetByte(object info, string name) =>
		(byte)info.GetType().GetField(name)!.GetValue(info)!;

	private static string GetString(object info, string name) =>
		(string?)info.GetType().GetField(name)!.GetValue(info) ?? string.Empty;

	private static float GetFloat(object info, string name) =>
		(float)info.GetType().GetField(name)!.GetValue(info)!;

	private static bool HasUseAction(object info) =>
		info.GetType().GetField("useAction")?.GetValue(info) is not null;

	[Fact]
	public void Update_ToolAndGunSetStaticUseDefaultsAndTags()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_tool",
			Tool = new ModItemTool
			{
				Damage = 30f,
				StructuralDamage = 40f,
				Distance = 3f,
				KnockBack = 300f,
				Cooldown = 0.4f
			}
		}));
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_gun",
			Tags = "utility",
			Gun = new ModItemGun
			{
				AmmoType = ModGunAmmoType.Pistol,
				MagCapacity = 12,
				ShotsPerFire = 1
			}
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var tool = GetItemInfo("custom_tool");
		Assert.True(GetBool(tool, "usable"));
		Assert.True(GetBool(tool, "usableWithLMB"));
		Assert.True(GetBool(tool, "autoAttack"));
		Assert.True(HasUseAction(tool));

		var gun = GetItemInfo("custom_gun");
		Assert.True(GetBool(gun, "usable"));
		Assert.True(GetBool(gun, "usableWithLMB"));
		Assert.True(GetBool(gun, "autoAttack"));
		Assert.True(HasUseAction(gun));
		Assert.Contains("gun", GetString(gun, "tags").Split(','), StringComparer.OrdinalIgnoreCase);
	}

	[Fact]
	public void Update_BatteryOverridesDestroyAtZeroAndSetsDecayFlag()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_battery",
			DestroyAtZeroCondition = true,
			DecayMinutes = 60f,
			Battery = new ModItemBattery
			{
				Preset = ModBatteryPreset.Small,
				StartCharge = 0.5f
			}
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo("custom_battery");
		Assert.False(GetBool(info, "destroyAtZeroCondition"));
		Assert.NotEqual(0, GetByte(info, "decayInfo") & 16);
		Assert.True(GetFloat(info, "decayMinutes") > 0f);
		Assert.True(GetFloat(info, "rotSpeed") > 0f);
	}

	[Fact]
	public void TryBind_AcceptsVisualDto()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "custom_visual",
			Visual = new ModItemVisual
			{
				WornSpritePath = "Clothing/TestWorn",
				WornSpriteOffsetX = 1f,
				WornSpriteOffsetY = -1f,
				WornSpriteSortingOrder = 5,
				LiquidMaskPath = "Containers/TestMask",
				MultiWornSprites =
				[
					new ModItemLimbWornSprite
					{
						LimbName = "Head",
						SpritePath = "Clothing/TestHat",
						OffsetX = 0.5f,
						OffsetY = -0.25f
					}
				],
				BaseSpriteAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestBase0", "Fx/TestBase1"],
					FramesPerSecond = 12f,
					Loop = true
				},
				WornSpriteAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestWorn0", "Fx/TestWorn1"],
					FramesPerSecond = 9f,
					Loop = false
				},
				LiquidMaskAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestMask0", "Fx/TestMask1"],
					FramesPerSecond = 7f,
					Loop = true
				}
			}
		}));
	}

	[Fact]
	public void TryBind_RejectsInvalidAdvancedBehaviorValues()
	{
		var provider = CreateProvider();

		Assert.True(TryBind(provider, new ModItemDefinition
		{
			Id = "valid",
			Container = new ModItemContainer { Capacity = 20f },
			Battery = new ModItemBattery { StartCharge = 0.5f },
			Light = new ModItemLight { Intensity = 1f },
			Tool = new ModItemTool { Damage = 10f },
			Gun = new ModItemGun { ShotsPerFire = 1 }
		}));

		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_container",
			Container = new ModItemContainer { Capacity = -1f }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_battery",
			Battery = new ModItemBattery { StartCharge = float.NaN }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_light",
			Light = new ModItemLight { Intensity = -0.1f }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_tool",
			Tool = new ModItemTool { Damage = -1f }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_gun_mag",
			Gun = new ModItemGun { MagCapacity = -1 }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_gun_shots",
			Gun = new ModItemGun { ShotsPerFire = 0 }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_visual",
			Visual = new ModItemVisual { WornSpriteOffsetX = float.NaN }
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_visual_multi",
			Visual = new ModItemVisual
			{
				MultiWornSprites =
				[
					new ModItemLimbWornSprite
					{
						LimbName = "Head",
						SpritePath = "Clothing/TestHat",
						OffsetX = float.PositiveInfinity,
						OffsetY = 0f
					}
				]
			}
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_visual_animation_fps",
			Visual = new ModItemVisual
			{
				BaseSpriteAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestBase0"],
					FramesPerSecond = float.NaN
				}
			}
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_visual_animation_zero_fps",
			Visual = new ModItemVisual
			{
				LiquidMaskAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestMask0", "Fx/TestMask1"],
					FramesPerSecond = 0f
				}
			}
		}));
		Assert.False(TryBind(provider, new ModItemDefinition
		{
			Id = "bad_visual_animation_empty",
			Visual = new ModItemVisual
			{
				WornSpriteAnimation = new ModItemSpriteAnimation
				{
					FramePaths = [],
					FramesPerSecond = 12f
				}
			}
		}));
	}

	/// <summary>
	/// One level down, the same rule the declaration level answers: a mod-authored
	/// implementation may hand back null for a nested collection it does not carry,
	/// and every reader asks <see cref="ModDeclarationCollections"/> rather than
	/// dereferencing — so the definition still binds and still materializes. The
	/// framework's own data classes coalesce a null write, so only a MOD-AUTHORED
	/// nested object can present this shape at all.
	/// </summary>
	[Fact]
	public void Update_ANestedCollectionThatIsNull_ReadsAsNoneAndStillMaterializes()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, new NestedNullsItem(nullFrames: false)));

		PrepareGameTables();
		InvokeUpdate(provider);

		var info = GetItemInfo(NestedNullsItem.ItemId);
		Assert.Equal(NestedNullsItem.ItemId, GetString(info, "fullName"));
		Assert.True(HasUseAction(info));
	}

	/// <summary>
	/// The other half of the same rule: where a null collection IS meaningful to a
	/// provider's own validation — an animation with no frame paths — reading it as
	/// "none" makes the provider refuse the definition with its OWN reason instead of
	/// throwing out of the validator, which is what a raw dereference would do.
	/// </summary>
	[Fact]
	public void TryBind_ANestedAnimationWithoutFramePaths_IsRefusedNotThrown()
	{
		var provider = CreateProvider();

		Assert.False(TryBind(provider, new NestedNullsItem(nullFrames: true)));
	}

	/// <summary>
	/// An item whose nested objects are the mod's own implementations, each
	/// returning null for the collection it does not carry: a tool with no swing
	/// sounds, a container with no tag restriction, a visual with no multi-worn
	/// sprites and — when the case asks for it — an animation with no frame paths.
	/// </summary>
	private sealed class NestedNullsItem(bool nullFrames) : IModItemDefinition
	{
		internal const string ItemId = "nested_nulls";

		public string Id => ItemId;

		public string Kind => ModContentKind.Item;

		public int SchemaVersion => 1;

		public string DisplayName => ItemId;

		public string Description => "";

		public string Category => "nospawn";

		public float Weight => 1f;

		public int Value => 0;

		public bool Usable => false;

		public bool UsableWithLmb => false;

		public bool Wearable => false;

		public bool DestroyAtZeroCondition => false;

		public string Tags => "";

		public int SpawnFrequency => 1;

		public string TemplateId => "";

		public List<string> SpawnComponents => [];

		public Dictionary<string, string> CustomData => [];

		public float? WorldSpawnPerChunk => null;

		public ModItemDropSource? DropSources => null;

		public IModItemContainer? Container { get; } = new NullTagContainer();

		public IModItemBattery? Battery => null;

		public IModItemLight? Light => null;

		public IModItemTool? Tool { get; } = new NullSwingSoundsTool();

		public IModItemGun? Gun => null;

		public float DecayMinutes => 0f;

		public IModItemVisual? Visual { get; } = new NullVisual(nullFrames);

		public List<IModCraftingQuality> Qualities => [];
	}

	private sealed class NullSwingSoundsTool : IModItemTool
	{
		public float Damage => 25f;

		public float StructuralDamage => 25f;

		public float AttackCooldownMultiplier => 0.66f;

		public float Distance => 2.5f;

		public float KnockBack => 270f;

		public float Cooldown => 0.35f;

		public string AttackAnimation => "";

		public float StaminaUse => 0.5f;

		public bool Piercing => false;

		public List<string> SwingSounds => null!;

		public float Volume => 0.5f;

		public float RotateAmount => 15.5f;

		public bool PhysicalSwing => true;

		public bool DoAttackAnimation => true;

		public bool MetalMoreDamage => false;

		public float ConditionLossOnHit => 0.02f;
	}

	private sealed class NullTagContainer : IModItemContainer
	{
		public float Capacity => 10f;

		public float MaxWeightPerItem => 5f;

		public float EncumbranceReduction => 1f;

		public bool ItemsVisible => false;

		public List<string> TagRestriction => null!;
	}

	private sealed class NullVisual(bool nullFrames) : IModItemVisual
	{
		public string WornSpritePath => "";

		public float WornSpriteOffsetX => 0f;

		public float WornSpriteOffsetY => 0f;

		public int? WornSpriteSortingOrder => null;

		public string LiquidMaskPath => "";

		public List<IModItemLimbWornSprite> MultiWornSprites => null!;

		public IModItemSpriteAnimation? BaseSpriteAnimation { get; } = nullFrames ? new NullFramesAnimation() : null;

		public IModItemSpriteAnimation? WornSpriteAnimation => null;

		public IModItemSpriteAnimation? LiquidMaskAnimation => null;
	}

	private sealed class NullFramesAnimation : IModItemSpriteAnimation
	{
		public List<string> FramePaths => null!;

		public float FramesPerSecond => 12f;

		public bool Loop => true;
	}
}
