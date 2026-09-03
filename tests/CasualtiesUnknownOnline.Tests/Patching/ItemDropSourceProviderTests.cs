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
/// The GameAdapter item provider's explicit fixed drop-source contract. The test
/// project never compile-references GameAdapter (it binds game assemblies), so
/// this locks the stable synthetic category names and the "explicit sources
/// replace generic category fallback" rule reflectively. Both peers must see the
/// same synthetic categories and the same frequency weights.
/// </summary>
public class ItemDropSourceProviderTests
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

		var lootPoolType = GameAssemblyHost.ResolveType("ItemLootPool")
			?? throw new InvalidOperationException("ItemLootPool not found in game assembly.");
		var stringListType = typeof(List<>).MakeGenericType(typeof(string));
		var poolDictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), stringListType);
		lootPoolType.GetField("pool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
			.SetValue(null, Activator.CreateInstance(poolDictType)!);
	}

	private static void InvokeUpdate(object provider)
	{
		var update = provider.GetType().GetMethod(
			"Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Update not found.");
		update.Invoke(provider, null);
	}

	private static bool TryGetDropSourceCategory(object provider, ModItemDropSource source, out string category)
	{
		var method = provider.GetType().GetMethod(
			"TryGetDropSourceCategory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryGetDropSourceCategory not found.");
		var args = new object?[] { source, null };
		var found = (bool)method.Invoke(provider, args)!;
		category = (string?)args[1] ?? string.Empty;
		return found;
	}

	private static IDictionary GetLootPool()
	{
		var lootPoolType = GameAssemblyHost.ResolveType("ItemLootPool")
			?? throw new InvalidOperationException("ItemLootPool not found in game assembly.");
		return (IDictionary)lootPoolType.GetField(
			"pool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
	}

	[Fact]
	public void Update_ExplicitDropSourceSuppressesCategoryFallbackAndSeedsSourcePool()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, "custom_med", new ModItemDefinition
		{
			Category = "rare",
			SpawnFrequency = 2,
			DropSources = ModItemDropSource.MedicalCrate
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		var pool = GetLootPool();
		Assert.False(pool.Contains("rare"), "an explicit drop source must suppress the generic category fallback");
		Assert.True(TryGetDropSourceCategory(provider, ModItemDropSource.MedicalCrate, out var category));
		Assert.Equal("cuo_drop_medical_crate", category);
		Assert.Equal(2, ((IList)pool[category]!).Count);
		Assert.False(TryGetDropSourceCategory(provider, ModItemDropSource.Corpse, out _));
	}

	[Fact]
	public void Update_ExplicitSourcesRemoveGenericEntryFromRebuiltPool()
	{
		var provider = CreateProvider();
		PrepareGameTables();
		var pool = GetLootPool();
		pool["rare"] = new List<string> { "custom_med" };

		Assert.True(TryBind(provider, "custom_med", new ModItemDefinition
		{
			Category = "rare",
			SpawnFrequency = 1,
			DropSources = ModItemDropSource.MedicalCrate
		}));

		InvokeUpdate(provider);

		Assert.Equal(0, ((IList)pool["rare"]!).Count);
		Assert.True(TryGetDropSourceCategory(provider, ModItemDropSource.MedicalCrate, out var category));
		Assert.Equal(1, ((IList)pool[category]!).Count);
	}

	[Fact]
	public void Update_AllTradersExpandsToIndividualTraderCategories()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, "custom_trade", new ModItemDefinition
		{
			SpawnFrequency = 1,
			DropSources = ModItemDropSource.AllTraders
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		Assert.True(TryGetDropSourceCategory(provider, ModItemDropSource.Trader1, out var trader1));
		Assert.True(TryGetDropSourceCategory(provider, ModItemDropSource.Trader2, out var trader2));
		Assert.True(TryGetDropSourceCategory(provider, ModItemDropSource.Trader3, out var trader3));
		Assert.Equal("cuo_drop_trader1", trader1);
		Assert.Equal("cuo_drop_trader2", trader2);
		Assert.Equal("cuo_drop_trader3", trader3);

		var pool = GetLootPool();
		Assert.Equal(1, ((IList)pool[trader1]!).Count);
		Assert.Equal(1, ((IList)pool[trader2]!).Count);
		Assert.Equal(1, ((IList)pool[trader3]!).Count);
	}

	[Fact]
	public void Update_ZeroFrequencyRegistersNoExplicitSourcePool()
	{
		var provider = CreateProvider();
		Assert.True(TryBind(provider, "custom_none", new ModItemDefinition
		{
			SpawnFrequency = 0,
			DropSources = ModItemDropSource.DropCapsule
		}));

		PrepareGameTables();
		InvokeUpdate(provider);

		Assert.False(TryGetDropSourceCategory(provider, ModItemDropSource.DropCapsule, out _));
		var pool = GetLootPool();
		Assert.False(pool.Contains("cuo_drop_capsule"));
	}
}
