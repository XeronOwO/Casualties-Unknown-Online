using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The report/receive message-flow surface of the item domain: local-compute
/// report sends (spawn/drop/use/cook/destroy), wire receive events, block-break
/// drop registration and correction/action receive forwarding. Host-side
/// pickup/spawn/drop arbitration delegates to
/// <see cref="ItemPendingPickupArbiter"/>.
/// </summary>
internal sealed class ItemMessageFlowService(
	ISessionControl session,
	PacketSender sender,
	ILogger<ItemService> log,
	WorldItemTable worldTable,
	ItemArbitration arbitration,
	ItemActionSync itemActionSync,
	ItemSnapshotService snapshots,
	BlockDropSync blockDrops,
	ItemPendingPickupArbiter pendingPickups,
	Action<ItemTrafficKind, string> recordTraffic,
	Func<ulong, string> itemTrafficLabel,
	Action<WorldItem> onItemSpawned,
	Action<ulong> onItemPickedUp,
	Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> onItemDropped,
	Action<ulong> onItemDestroyed,
	Action<ulong, WorldItem> onItemCookedReceived,
	Action<ulong, ItemRejectMsg.Reason> onItemRejected)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<ItemService> _log = log;
	private readonly WorldItemTable _worldTable = worldTable;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly ItemActionSync _itemActionSync = itemActionSync;
	private readonly ItemSnapshotService _snapshots = snapshots;
	private readonly BlockDropSync _blockDrops = blockDrops;
	private readonly ItemPendingPickupArbiter _pendingPickups = pendingPickups;
	private readonly Action<ItemTrafficKind, string> _recordTraffic = recordTraffic;
	private readonly Func<ulong, string> _itemTrafficLabel = itemTrafficLabel;
	private readonly Action<WorldItem> _onItemSpawned = onItemSpawned;
	private readonly Action<ulong> _onItemPickedUp = onItemPickedUp;
	private readonly Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> _onItemDropped = onItemDropped;
	private readonly Action<ulong> _onItemDestroyed = onItemDestroyed;
	private readonly Action<ulong, WorldItem> _onItemCookedReceived = onItemCookedReceived;
	private readonly Action<ulong, ItemRejectMsg.Reason> _onItemRejected = onItemRejected;

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

		_recordTraffic(ItemTrafficKind.Spawn, item.ItemId);

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
			_session.Broadcast(NetMsg.ItemSpawn, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.ItemSpawn, msg);
		}
	}

	public void SendItemCooked(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("ItemCook suppressed on guest (source {Source}, cooked {Cooked}) — the host owns heater conversions.", sourceItemId, cookedItemId);
			return;
		}

		_worldTable.Remove(sourceItemId);
		_worldTable.Set(cookedItemId, new WorldItem(cookedItemId, item, pos, vel, 0, rotation, false, AngularVelocity: angularVelocity));

		if (!_session.SessionActive)
		{
			return;
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
			_worldTable.Remove(itemId);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		_recordTraffic(ItemTrafficKind.Pickup, evidence?.ItemId ?? _itemTrafficLabel(itemId));

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

	public void SendItemUse(ulong itemId, CharacterItemMsg item) => _itemActionSync.SendItemUse(itemId, item);

	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item) => _itemActionSync.SendItemSlot(itemId, slotIndex, item);

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

		_recordTraffic(ItemTrafficKind.Drop, item.ItemId);

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
		var trafficLabel = _itemTrafficLabel(itemId);
		if (_session.Role != SessionRole.Guest)
		{
			_worldTable.Remove(itemId);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		_recordTraffic(ItemTrafficKind.Destroy, trafficLabel);

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
			_pendingPickups.HandleHostSpawnReport(sender, itemId, item, pos, vel, rotation, freshItemDrop, angularVelocity);
			return;
		}

		_onItemSpawned(new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity));
	}

	public void FireItemPickedUpReceived(ulong sender, ulong itemId, CharacterItemMsg? evidence)
	{
		if (_session.Role == SessionRole.Host)
		{
			_pendingPickups.HandleHostPickupReport(sender, itemId, evidence);
			return;
		}

		_onItemPickedUp(itemId);
	}

	public void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos = default)
	{
		if (_session.Role == SessionRole.Host)
		{
			_pendingPickups.HandleHostDropReport(sender, itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
			return;
		}

		_onItemDropped(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
	}

	public void FireItemDestroyedReceived(ulong sender, ulong itemId)
	{
		if (_session.Role == SessionRole.Host)
		{
			var trafficLabel = _itemTrafficLabel(itemId);
			_worldTable.Remove(itemId);
			_session.BroadcastExcept(sender, NetMsg.ItemDestroy, new ItemDestroyMsg { ItemId = itemId });
			_recordTraffic(ItemTrafficKind.Destroy, trafficLabel);
		}

		_onItemDestroyed(itemId);
	}

	public void FireItemCookedReceived(ulong sender, ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_onItemCookedReceived(sourceItemId,
			new WorldItem(cookedItemId, item, pos, vel, 0, rotation, false, AngularVelocity: angularVelocity));
	}

	public void FireItemRejectReceived(ulong sender, ulong itemId, ItemRejectMsg.Reason reason)
	{
		_log.LogWarning("Item {ItemId} rejected by the host ({Reason}) — rolling back.", itemId, reason);
		_onItemRejected(itemId, reason);
	}

	// ===== Block-break drops =====

	public void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops) => _blockDrops.RegisterBlockDrops(drops);

	public void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops) => _blockDrops.FireBlockDropsReceived(sender, drops);

	public void SendItemReject(ulong targetSteamId, ulong itemId, ItemRejectMsg.Reason reason)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.ItemReject, new ItemRejectMsg { ItemId = itemId, Rejection = reason });
	}

	public bool RegisterWorldItemIfAbsent(ulong itemId, WorldItem item) => _worldTable.RegisterIfAbsent(itemId, item);

	public bool IsWorldItemRegistered(ulong itemId) => _worldTable.ContainsKey(itemId);

	public void FireItemSpawned(WorldItem item) => _onItemSpawned(item);

	public void FireItemCorrectionReceived(ulong sender, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_arbitration.FireCorrectionReceived(item);
	}

	public void FireItemUseReceived(ulong sender, ulong itemId, CharacterItemMsg evidence) => _itemActionSync.FireItemUseReceived(sender, itemId, evidence);

	public void FireItemSlotReceived(ulong sender, ulong itemId, int slotIndex, CharacterItemMsg item) => _itemActionSync.FireItemSlotReceived(sender, itemId, slotIndex, item);

	public void FireItemContainerContentReceived(ulong sender, ulong itemId, CharacterItemMsg item) => _itemActionSync.FireItemContainerContentReceived(sender, itemId, item);

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireItemSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);
}
