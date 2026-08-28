using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase C guest-side projection: applies confirmed kernel batches to the
/// legacy world-item projection/table and raises the item-domain events that
/// the Game Adapter consumes. This is a projection, never an authority: it
/// reads from the already-applied kernel and writes only the rebuildable
/// world-item cache.
/// </summary>
internal sealed class KernelBatchItemProjection(
	ItemKernelAuthority authority,
	WorldItemTable worldTable,
	Action<WorldItem> onItemSpawned,
	Action<ulong> onItemPickedUp,
	Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> onItemDropped,
	Action<ulong> onItemDestroyed)
{
	private readonly ItemKernelAuthority _authority = authority;
	private readonly WorldItemTable _worldTable = worldTable;
	private readonly Action<WorldItem> _onItemSpawned = onItemSpawned;
	private readonly Action<ulong> _onItemPickedUp = onItemPickedUp;
	private readonly Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> _onItemDropped = onItemDropped;
	private readonly Action<ulong> _onItemDestroyed = onItemDestroyed;

	public void Apply(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			ApplyKernelEventToProjection(@event);
		}
	}

	private void ApplyKernelEventToProjection(GameEvent @event)
	{
		switch (@event)
		{
			case ItemSpawnedEvent spawned:
				ApplySpawnedToProjection(spawned);
				break;
			case ItemRelocatedEvent relocated:
				ApplyRelocatedToProjection(relocated);
				break;
			case ItemDestroyedEvent destroyed:
				ApplyDestroyedToProjection(destroyed);
				break;
			case ItemDataUpdatedEvent updated:
				ApplyDataUpdatedToProjection(updated);
				break;
		}
	}

	private void ApplySpawnedToProjection(ItemSpawnedEvent spawned)
	{
		if (spawned.Location.Kind != ItemLocationKind.World)
		{
			return;
		}

		var current = _authority.FindItem(spawned.Identity.InstanceId);
		if (current is null)
		{
			return;
		}

		var world = ToWorldItem(current.Value);
		_worldTable.Set(world.ItemId, world);
		_onItemSpawned(world);
	}

	private void ApplyRelocatedToProjection(ItemRelocatedEvent relocated)
	{
		var current = _authority.FindItem(relocated.Identity.InstanceId);
		if (current is null)
		{
			return;
		}

		var itemId = relocated.Identity.InstanceId;
		if (relocated.NewLocation.Kind == ItemLocationKind.World)
		{
			var wasWorld = relocated.OldLocation.Kind == ItemLocationKind.World;
			var world = ToWorldItem(current.Value);
			_worldTable.Set(itemId, world);
			if (!wasWorld)
			{
				_onItemDropped(itemId, world.Item, world.Pos, world.Vel, world.ParentItemId, world.Rotation, world.AngularVelocity, world.ParentPosition);
			}

			return;
		}

		if (relocated.OldLocation.Kind == ItemLocationKind.World)
		{
			_worldTable.Remove(itemId);
			_onItemPickedUp(itemId);
		}
	}

	private void ApplyDestroyedToProjection(ItemDestroyedEvent destroyed)
	{
		if (_worldTable.ContainsKey(destroyed.Identity.InstanceId))
		{
			_worldTable.Remove(destroyed.Identity.InstanceId);
			_onItemDestroyed(destroyed.Identity.InstanceId);
		}
	}

	private void ApplyDataUpdatedToProjection(ItemDataUpdatedEvent updated)
	{
		if (!_worldTable.TryGetValue(updated.Identity.InstanceId, out var existing))
		{
			return;
		}

		var current = _authority.FindItem(updated.Identity.InstanceId);
		if (current is null)
		{
			return;
		}

		var item = ItemKernelAuthority.ToCharacterItem(current.Value);
		_worldTable.Set(existing.ItemId, existing with { Item = item });
	}

	private static WorldItem ToWorldItem(ItemState state)
	{
		var item = ItemKernelAuthority.ToCharacterItem(state);
		var parentItemId = state.Location.Kind == ItemLocationKind.World ? state.Location.ParentItemId : 0;
		return new WorldItem(
			state.Identity.InstanceId,
			item,
			new NetVector2(state.Location.X, state.Location.Y),
			NetVector2.Zero,
			parentItemId,
			0f,
			false);
	}
}
