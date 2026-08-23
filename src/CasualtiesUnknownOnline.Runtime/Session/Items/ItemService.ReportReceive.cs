using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The report/receive message-flow surface of <see cref="ItemService"/>
/// (split off at the 600-line gate): local-compute report sends (spawn/drop/
/// use/cook/destroy), wire receive events, block-break drop registration and
/// the correction/action receive forwarding. The authoritative table, lifecycle
/// seams and host-only snapshot surfaces stay in ItemService.cs.
/// </summary>
public sealed partial class ItemService
{
	// ===== Report side (local compute) =====

	public void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldTable.Set(itemId, new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity));
		}

		if (!_session.SessionActive)
		{
			return;
		}

		RecordItemTraffic(ItemTrafficKind.Spawn, item.ItemId);

		var msg = new ItemSpawnMsg
		{
			ItemId = itemId,
			Item = item,
			Position = pos.ToNetVector2Msg(),
			Velocity = vel.ToNetVector2Msg(),
			Rotation = rotation,
			FreshItemDrop = freshItemDrop,
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.ItemSpawn, msg); // our own spawn: relay to every guest (we already applied)
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.ItemSpawn, msg);
		}
	}

	public void SendItemCooked(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		// The heater conversion is host-authoritative (guest world items are
		// layer-isolated to the Ground layer and can never collide with the
		// cooker) — a guest call would be a stale/wrong role report and is
		// deliberately a no-op.
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("ItemCook suppressed on guest (source {Source}, cooked {Cooked}) — the host owns heater conversions.", sourceItemId, cookedItemId);
			return;
		}

		// ONE atomic table transition: the raw meat is gone, the steak exists.
		_worldTable.Remove(sourceItemId);
		_worldTable.Set(cookedItemId, new WorldItem(cookedItemId, item, pos, vel, 0, rotation, false, AngularVelocity: angularVelocity));

		if (!_session.SessionActive)
		{
			return; // solo play keeps the table for a future late joiner, no wire
		}

		_session.Broadcast(NetMsg.ItemCook, new ItemCookMsg
		{
			SourceItemId = sourceItemId,
			CookedItemId = cookedItemId,
			Item = item,
			Position = pos.ToNetVector2Msg(),
			Velocity = vel.ToNetVector2Msg(),
			Rotation = rotation,
			AngularVelocity = angularVelocity,
		});
	}

	public void SendItemPickedUp(ulong itemId, CharacterItemMsg? evidence = null)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldTable.Remove(itemId); // the picker took it — it is inventory data now
		}

		if (!_session.SessionActive)
		{
			return;
		}

		RecordItemTraffic(ItemTrafficKind.Pickup, evidence?.ItemId ?? ItemTrafficLabel(itemId));

		// The digest evidence rides the guest's report only (host-side pickups
		// are the host's own authority — nothing to check, and the broadcast to
		// the other guests carries no Item).
		var msg = new ItemPickupMsg { ItemId = itemId, Item = _session.Role == SessionRole.Guest ? evidence : null };
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.ItemPickup, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.ItemPickup, msg);
		}
	}

	/// <summary>Guest only: an item was used locally — report the used state (digest evidence) so the host validates and corrects. Host-side uses are the host's own authority, never reported.</summary>
	public void SendItemUse(ulong itemId, CharacterItemMsg item) => _itemActionSync.SendItemUse(itemId, item);

	/// <summary>Guest only: an item moved slots locally — report the new slot so the host's record stays in sync. Host-side moves are the host's own authority, never reported.</summary>
	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item) => _itemActionSync.SendItemSlot(itemId, slotIndex, item);

	/// <summary>Guest only: a carried container's full fact changed internally (nested-content move) — report the parent container so the host records and relays it.</summary>
	public void SendItemContainerContent(ulong itemId, CharacterItemMsg item) => _itemActionSync.SendItemContainerContent(itemId, item);

	public void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldTable.Set(itemId, new WorldItem(itemId, item, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity));
		}

		if (!_session.SessionActive)
		{
			return;
		}

		RecordItemTraffic(ItemTrafficKind.Drop, item.ItemId);

		var msg = new ItemDropMsg
		{
			ItemId = itemId,
			Item = item,
			Position = pos.ToNetVector2Msg(),
			Velocity = vel.ToNetVector2Msg(),
			ParentItemId = parentItemId,
			Rotation = rotation,
			ParentPosition = parentPos.ToNetVector2Msg(),
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.ItemDrop, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.ItemDrop, msg);
		}
	}

	public void SendItemDestroyed(ulong itemId)
	{
		var trafficLabel = ItemTrafficLabel(itemId);
		if (_session.Role != SessionRole.Guest)
		{
			_worldTable.Remove(itemId);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		RecordItemTraffic(ItemTrafficKind.Destroy, trafficLabel);

		var msg = new ItemDestroyMsg { ItemId = itemId };
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.ItemDestroy, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.ItemDestroy, msg);
		}
	}

	// ===== Receive side (wire handlers) =====

	public void FireItemSpawnedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity)
	{
		if (_session.Role == SessionRole.Host)
		{
			HandleHostSpawnReport(sender, itemId, item, pos, vel, rotation, freshItemDrop, angularVelocity);
			return;
		}

		// Guest materializes the host's relay (the host's own materialization
		// is part of HandleHostSpawnReport, where a queued pickup may settle).
		ItemSpawned?.Invoke(new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity));
	}

	public void FireItemPickedUpReceived(ulong sender, ulong itemId, CharacterItemMsg? evidence)
	{
		if (_session.Role == SessionRole.Host)
		{
			HandleHostPickupReport(sender, itemId, evidence);
			return;
		}

		// Guest side: the winner broadcast removes the item from this side's
		// world; a losing optimistic pickup rolls back through ItemReject.
		ItemPickedUp?.Invoke(itemId);
	}

	public void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos = default)
	{
		if (_session.Role == SessionRole.Host)
		{
			HandleHostDropReport(sender, itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
			return;
		}

		ItemDropped?.Invoke(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
	}

	public void FireItemDestroyedReceived(ulong sender, ulong itemId)
	{
		if (_session.Role == SessionRole.Host)
		{
			var trafficLabel = ItemTrafficLabel(itemId);
			_worldTable.Remove(itemId);
			_session.BroadcastExcept(sender, NetMsg.ItemDestroy, new ItemDestroyMsg { ItemId = itemId });
			RecordItemTraffic(ItemTrafficKind.Destroy, trafficLabel);
		}

		ItemDestroyed?.Invoke(itemId);
	}

	public void FireItemCookedReceived(ulong sender, ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		// One-way host → guest; the host never receives its own broadcast and a
		// misrouted/stale frame arriving at the host is dropped here.
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		ItemCookedReceived?.Invoke(sourceItemId,
			new WorldItem(cookedItemId, item, pos, vel, 0, rotation, false, AngularVelocity: angularVelocity));
	}

	public void FireItemRejectReceived(ulong sender, ulong itemId, ItemRejectMsg.Reason reason)
	{
		_log.LogWarning("Item {ItemId} rejected by the host ({Reason}) — rolling back.", itemId, reason);
		ItemRejected?.Invoke(itemId, reason);
	}

	// ===== Block-break drops (one message, one verdict — the break's drops ride BlockDamagedMsg) — the chain lives in BlockDropSync =====

	/// <summary>Host/solo: record the drops of a LOCALLY broken block into the authoritative table.</summary>
	public void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops) => _blockDrops.RegisterBlockDrops(drops);

	/// <summary>A break with drops was APPLIED — register (host only) and materialize every drop.</summary>
	public void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops) => _blockDrops.FireBlockDropsReceived(sender, drops);

	/// <summary>
	/// Host only: refuse a reported break's drops (the break was already applied
	/// by another report — first-writer-wins) — the reporter destroys its local
	/// drops (BlockAlreadyBroken) and the world never sees them.
	/// </summary>
	public void SendItemReject(ulong targetSteamId, ulong itemId, ItemRejectMsg.Reason reason)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.ItemReject, new ItemRejectMsg { ItemId = itemId, Rejection = reason });
	}

	/// <summary>Host/solo: register a world item into the table when absent — the
	/// block-break drop domain asks, the table state belongs to WorldItemTable.</summary>
	internal bool RegisterWorldItemIfAbsent(ulong itemId, WorldItem item) => _worldTable.RegisterIfAbsent(itemId, item);

	/// <summary>Read-only query: is the item in the authoritative world table (tests + diagnostics).</summary>
	internal bool IsWorldItemRegistered(ulong itemId) => _worldTable.ContainsKey(itemId);

	/// <summary>Surface the ItemSpawned event for the block-break drop domain (an event can only be invoked from its declaring class).</summary>
	internal void FireItemSpawned(WorldItem item) => ItemSpawned?.Invoke(item);

	public void FireItemCorrectionReceived(ulong sender, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return; // host-side corrections make no sense — the host is the source
		}

		_arbitration.FireCorrectionReceived(item);
	}

	public void FireItemUseReceived(ulong sender, ulong itemId, CharacterItemMsg evidence) => _itemActionSync.FireItemUseReceived(sender, itemId, evidence);

	public void FireItemSlotReceived(ulong sender, ulong itemId, int slotIndex, CharacterItemMsg item) => _itemActionSync.FireItemSlotReceived(sender, itemId, slotIndex, item);

	public void FireItemContainerContentReceived(ulong sender, ulong itemId, CharacterItemMsg item) => _itemActionSync.FireItemContainerContentReceived(sender, itemId, item);

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireItemSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);
}
