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

	private static bool TryBind(object provider, string id, ModItemDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration(
			"mod.a",
			new ModContentDefinition(id, ModContentKind.Item, definition.ToPayload(), 1));
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
		Assert.True(TryBind(provider, "custom_tool", new ModItemDefinition
		{
			Tool = new ModItemTool
			{
				Damage = 30f,
				StructuralDamage = 40f,
				Distance = 3f,
				KnockBack = 300f,
				Cooldown = 0.4f
			}
		}));
		Assert.True(TryBind(provider, "custom_gun", new ModItemDefinition
		{
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
		Assert.True(TryBind(provider, "custom_battery", new ModItemDefinition
		{
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
		Assert.True(TryBind(provider, "custom_visual", new ModItemDefinition
		{
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

		Assert.True(TryBind(provider, "valid", new ModItemDefinition
		{
			Container = new ModItemContainer { Capacity = 20f },
			Battery = new ModItemBattery { StartCharge = 0.5f },
			Light = new ModItemLight { Intensity = 1f },
			Tool = new ModItemTool { Damage = 10f },
			Gun = new ModItemGun { ShotsPerFire = 1 }
		}));

		Assert.False(TryBind(provider, "bad_container", new ModItemDefinition
		{
			Container = new ModItemContainer { Capacity = -1f }
		}));
		Assert.False(TryBind(provider, "bad_battery", new ModItemDefinition
		{
			Battery = new ModItemBattery { StartCharge = float.NaN }
		}));
		Assert.False(TryBind(provider, "bad_light", new ModItemDefinition
		{
			Light = new ModItemLight { Intensity = -0.1f }
		}));
		Assert.False(TryBind(provider, "bad_tool", new ModItemDefinition
		{
			Tool = new ModItemTool { Damage = -1f }
		}));
		Assert.False(TryBind(provider, "bad_gun_mag", new ModItemDefinition
		{
			Gun = new ModItemGun { MagCapacity = -1 }
		}));
		Assert.False(TryBind(provider, "bad_gun_shots", new ModItemDefinition
		{
			Gun = new ModItemGun { ShotsPerFire = 0 }
		}));
		Assert.False(TryBind(provider, "bad_visual", new ModItemDefinition
		{
			Visual = new ModItemVisual { WornSpriteOffsetX = float.NaN }
		}));
		Assert.False(TryBind(provider, "bad_visual_multi", new ModItemDefinition
		{
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
		Assert.False(TryBind(provider, "bad_visual_animation_fps", new ModItemDefinition
		{
			Visual = new ModItemVisual
			{
				BaseSpriteAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestBase0"],
					FramesPerSecond = float.NaN
				}
			}
		}));
		Assert.False(TryBind(provider, "bad_visual_animation_zero_fps", new ModItemDefinition
		{
			Visual = new ModItemVisual
			{
				LiquidMaskAnimation = new ModItemSpriteAnimation
				{
					FramePaths = ["Fx/TestMask0", "Fx/TestMask1"],
					FramesPerSecond = 0f
				}
			}
		}));
		Assert.False(TryBind(provider, "bad_visual_animation_empty", new ModItemDefinition
		{
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
}
