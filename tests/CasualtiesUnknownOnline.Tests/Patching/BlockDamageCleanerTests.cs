using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter's game-list block-damage cleanup for direct air writes.
/// <c>WorldGeneration.DamageBlock</c> removes its own <c>BlockDamage</c> when it
/// breaks a block, but a remote air write / block-state snapshot / earthquake
/// applies <c>SetBlock(0)</c> directly and leaves the crack sprite in the game's
/// <c>blockDamages</c> list — the "fragmented air" on the host. This locks the
/// cleanup helper that all direct air-write paths must call.
/// </summary>
public class BlockDamageCleanerTests
{
	[Fact]
	public void ClearForAirWrite_RemovesStaleBlockDamageFromTheGameList()
	{
		var cleaner = FindCleaner();
		var (world, list, cell) = CreateWorldWithDamage(3, -7);

		var result = (bool)cleaner.Invoke(null, [world, cell])!;

		Assert.True(result, "an existing BlockDamage entry must be cleared");
		Assert.Equal(0, list.Count);
	}

	[Fact]
	public void ClearForAirWrite_NoEntry_ReturnsFalse()
	{
		var cleaner = FindCleaner();
		var (world, list, cell) = CreateWorld(9, 10);

		var result = (bool)cleaner.Invoke(null, [world, cell])!;

		Assert.False(result, "no entry is not a cleared entry");
		Assert.Equal(0, list.Count);
	}

	private static MethodInfo FindCleaner()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.BlockDamageCleaner")
			?? throw new InvalidOperationException("BlockDamageCleaner type not found in GameAdapter.");
		return type.GetMethod("ClearForAirWrite", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BlockDamageCleaner.ClearForAirWrite not found.");
	}

	private static (object World, IList List, object Cell) CreateWorld(int x, int y)
	{
		var worldType = GameAssemblyHost.ResolveType("WorldGeneration")
			?? throw new InvalidOperationException("WorldGeneration not found in game assembly.");
		var vectorType = GameAssemblyHost.ResolveType("UnityEngine.Vector2Int")
			?? throw new InvalidOperationException("UnityEngine.Vector2Int not found.");

		var world = FormatterServices.GetUninitializedObject(worldType);
		var listField = worldType.GetField("blockDamages", BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException("WorldGeneration.blockDamages not found.");
		var list = (IList)Activator.CreateInstance(listField.FieldType)!;
		listField.SetValue(world, list);

		var cell = Activator.CreateInstance(vectorType, x, y)
			?? throw new InvalidOperationException("Cannot create Vector2Int.");
		return (world, list, cell);
	}

	private static (object World, IList List, object Cell) CreateWorldWithDamage(int x, int y)
	{
		var result = CreateWorld(x, y);
		var blockDamageType = GameAssemblyHost.ResolveType("BlockDamage")
			?? throw new InvalidOperationException("BlockDamage not found in game assembly.");
		var damage = Activator.CreateInstance(blockDamageType)
			?? throw new InvalidOperationException("Cannot create BlockDamage.");
		var posField = blockDamageType.GetField("pos", BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException("BlockDamage.pos not found.");
		posField.SetValue(damage, result.Cell);
		result.List.Add(damage);
		return result;
	}
}
