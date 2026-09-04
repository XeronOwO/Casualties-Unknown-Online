using System.Reflection;
using UnityEngine;
using System;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Clears the game's own <c>BlockDamage</c> entry and its crack sprite when a
/// block is air-written through a path that bypasses
/// <c>WorldGeneration.DamageBlock</c>. <c>DamageBlock</c> removes its
/// <c>BlockDamage</c> when it breaks a block, but direct <c>SetBlock(0)</c>
/// paths — remote air writes, block-state snapshots, earthquake/environment
/// breaks — do not, so the crack sprite would otherwise remain over an air
/// cell ("fragmented air"). The Runtime <c>BlockDamageRegistry</c> is separate
/// (snapshot bookkeeping) and is cleared by <c>BlockBreakSync.OnBlockAirWrite</c>;
/// this class owns only the game-side visual/list cleanup.
/// </summary>
internal static class BlockDamageCleaner
{
	private static readonly FieldInfo SpriteField =
		typeof(BlockDamage).GetField("spr", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("BlockDamage.spr not found.");

	/// <summary>
	/// Removes the block's current partial-damage visual from the game's
	/// <c>blockDamages</c> list, if one exists. Returns false when there is
	/// nothing to clear.
	/// </summary>
	internal static bool ClearForAirWrite(WorldGeneration world, Vector2Int cell)
	{
		var damage = world.GetBlockDamage(cell);
		if (damage == null)
		{
			return false;
		}

		DestroyCrackSprite(damage);
		world.blockDamages.Remove(damage);
		return true;
	}

	/// <summary>
	/// Destroys the crack sprite GameObject without calling
	/// <c>BlockDamage.DestroySprite</c>: the game method uses
	/// <c>UnityEngine.Object</c>'s boolean conversion, which is an engine
	/// internal call and cannot run in a unit-test environment. The adapter
	/// owns this cleanup path, so it uses a reference-safe private-field read
	/// and only calls the engine when a sprite actually exists.
	/// </summary>
	private static void DestroyCrackSprite(BlockDamage damage)
	{
		var sprite = SpriteField.GetValue(damage);
		if (ReferenceEquals(sprite, null))
		{
			return;
		}

		var gameObject = sprite.GetType()
			.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
			?.GetValue(sprite);
		if (!ReferenceEquals(gameObject, null))
		{
			Object.Destroy((GameObject)gameObject);
		}
	}
}
