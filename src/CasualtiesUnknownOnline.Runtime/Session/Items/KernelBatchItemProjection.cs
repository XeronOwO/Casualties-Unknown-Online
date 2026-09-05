using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase C kernel projection: applies confirmed kernel batches to the legacy
/// world-item projection/table and raises the item-domain events that the Game
/// Adapter consumes. This is a projection, never an authority: it reads from
/// the already-applied kernel and writes only the rebuildable world-item cache.
/// </summary>
internal sealed class KernelBatchItemProjection(
	ItemKernelAuthority authority,
	WorldItemTable worldTable,
	Action<WorldItem> onItemSpawned,
	Action<ulong> onItemPickedUp,
	Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> onItemDropped,
	Action<ulong> onItemDestroyed,
	Action<ulong, CharacterItemMsg, bool>? onCarriedSync = null,
	Action<CharacterItemMsg>? onCorrection = null)
{
	private readonly ItemKernelAuthority _authority = authority;
	private readonly WorldItemTable _worldTable = worldTable;
	private readonly Action<WorldItem> _onItemSpawned = onItemSpawned;
	private readonly Action<ulong> _onItemPickedUp = onItemPickedUp;
	private readonly Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> _onItemDropped = onItemDropped;
	private readonly Action<ulong> _onItemDestroyed = onItemDestroyed;
	private readonly Action<ulong, CharacterItemMsg, bool>? _onCarriedSync = onCarriedSync;
	private readonly Action<CharacterItemMsg>? _onCorrection = onCorrection;

	public void Apply(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			ApplyKernelEventToProjection(@event);
		}

		EmitCarriedFactsForBatch(batch.Events);
	}

	/// <summary>
	/// Apply a batch to the world-item table only, without raising adapter
	/// events. Used for host-originated native transitions where the local
	/// scene is already the fact and only the rebuildable table must converge.
	/// </summary>
	public void ApplyWorldTableOnly(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			switch (@event)
			{
				case ItemSpawnedEvent spawned when spawned.Location.Kind == ItemLocationKind.World:
					SetWorldIfPresent(spawned.Identity.InstanceId);
					break;
				case ItemRelocatedEvent relocated:
					if (relocated.NewLocation.Kind == ItemLocationKind.World)
					{
						SetWorldIfPresent(relocated.Identity.InstanceId);
					}
					else if (relocated.OldLocation.Kind == ItemLocationKind.World)
					{
						_worldTable.Remove(relocated.Identity.InstanceId);
					}

					break;
				case ItemDestroyedEvent destroyed:
					_worldTable.Remove(destroyed.Identity.InstanceId);
					break;
				case ItemDataUpdatedEvent updated when _worldTable.ContainsKey(updated.Identity.InstanceId):
					SetWorldIfPresent(updated.Identity.InstanceId);
					break;
			}
		}
	}

	private void SetWorldIfPresent(ulong itemId)
	{
		var current = _authority.FindItem(itemId);
		if (current is null)
		{
			return;
		}

		var world = ToWorldItem(current.Value);
		_worldTable.Set(world.ItemId, world);
	}

	public void Rebuild(GameCheckpoint checkpoint)
	{
		_worldTable.Clear();
		foreach (var item in checkpoint.Items)
		{
			if (item.Location.Kind != ItemLocationKind.World)
			{
				continue;
			}

			var world = ToWorldItem(item);
			_worldTable.Set(world.ItemId, world);
			_onItemSpawned(world);
		}
	}

	/// <summary>
	/// Rebuild the world-item projection from the current kernel read model.
	/// This is the per-domain recovery path used after a projection failure;
	/// unlike a checkpoint rebuild it uses the live authoritative state without
	/// a full checkpoint round-trip.
	/// </summary>
	public void RebuildFromKernel()
	{
		_worldTable.Clear();
		foreach (var item in _authority.QueryItems().Values)
		{
			if (item.Location.Kind != ItemLocationKind.World)
			{
				continue;
			}

			var world = ToWorldItem(item);
			_worldTable.Set(world.ItemId, world);
			_onItemSpawned(world);
		}

		if (_onCarriedSync is null)
		{
			return;
		}

		foreach (var root in _authority.QueryItems().Values.Where(i => i.Location.Kind == ItemLocationKind.Carried))
		{
			var fact = BuildFullItem(root.Identity.InstanceId);
			if (fact is not null)
			{
				_onCarriedSync(root.Location.Owner.Value, fact, fact.SlotIndex != -1);
			}
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

		var world = ToWorldItem(current.Value, spawned);
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
			var world = ToWorldItem(current.Value);
			_worldTable.Set(itemId, world);
			_onItemDropped(itemId, world.Item, world.Pos, world.Vel, world.ParentItemId, world.Rotation, world.AngularVelocity, world.ParentPosition);
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
		_onCorrection?.Invoke(BuildFullItem(updated.Identity.InstanceId) ?? item);
	}

	private void EmitCarriedFactsForBatch(IReadOnlyList<GameEvent> events)
	{
		if (_onCarriedSync is null)
		{
			return;
		}

		var roots = new HashSet<ulong>();
		foreach (var @event in events)
		{
			var id = @event switch
			{
				ItemSpawnedEvent spawned => spawned.Identity.InstanceId,
				ItemRelocatedEvent relocated => relocated.Identity.InstanceId,
				ItemDataUpdatedEvent updated => updated.Identity.InstanceId,
				_ => 0ul,
			};

			if (id == 0)
			{
				continue;
			}

			var root = FindCarriedRoot(id);
			if (root.HasValue)
			{
				roots.Add(root.Value);
			}
		}

		foreach (var rootId in roots)
		{
			var root = _authority.FindItem(rootId);
			if (root is null || root.Value.Location.Kind != ItemLocationKind.Carried)
			{
				continue;
			}

			var fact = BuildFullItem(rootId);
			if (fact is null)
			{
				continue;
			}

			_onCarriedSync(root.Value.Location.Owner.Value, fact, fact.SlotIndex != -1);
		}
	}

	private ulong? FindCarriedRoot(ulong itemId)
	{
		var current = _authority.FindItem(itemId);
		if (current is null)
		{
			return null;
		}

		if (current.Value.Location.Kind == ItemLocationKind.Carried)
		{
			return itemId;
		}

		if (current.Value.Location.Kind != ItemLocationKind.Contained)
		{
			return null;
		}

		var visited = new HashSet<ulong>();
		var cursor = current.Value.Location.ParentItemId;
		while (cursor != 0 && visited.Add(cursor))
		{
			var parent = _authority.FindItem(cursor);
			if (parent is null)
			{
				return null;
			}

			if (parent.Value.Location.Kind == ItemLocationKind.Carried)
			{
				return cursor;
			}

			if (parent.Value.Location.Kind != ItemLocationKind.Contained)
			{
				return null;
			}

			cursor = parent.Value.Location.ParentItemId;
		}

		return null;
	}

	private CharacterItemMsg? BuildFullItem(ulong rootId)
	{
		var root = _authority.FindItem(rootId);
		if (root is null)
		{
			return null;
		}

		var msg = ItemKernelAuthority.ToCharacterItem(root.Value);
		msg.Contents = BuildContents(rootId);
		return msg;
	}

	private List<CharacterItemMsg> BuildContents(ulong parentId)
	{
		var list = new List<CharacterItemMsg>();
		foreach (var child in _authority.QueryItems().Values
			.Where(i => i.Location.Kind == ItemLocationKind.Contained && i.Location.ParentItemId == parentId)
			.OrderBy(i => i.Identity.InstanceId))
		{
			var childMsg = ItemKernelAuthority.ToCharacterItem(child);
			childMsg.Contents = BuildContents(child.Identity.InstanceId);
			list.Add(childMsg);
		}

		return list;
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

	private static WorldItem ToWorldItem(ItemState state, ItemSpawnedEvent spawned)
	{
		var item = ItemKernelAuthority.ToCharacterItem(state);
		var parentItemId = state.Location.Kind == ItemLocationKind.World ? state.Location.ParentItemId : 0;
		return new WorldItem(
			state.Identity.InstanceId,
			item,
			new NetVector2(state.Location.X, state.Location.Y),
			new NetVector2(spawned.VelocityX, spawned.VelocityY),
			parentItemId,
			spawned.Rotation,
			spawned.FreshItemDrop,
			AngularVelocity: spawned.AngularVelocity);
	}
}
