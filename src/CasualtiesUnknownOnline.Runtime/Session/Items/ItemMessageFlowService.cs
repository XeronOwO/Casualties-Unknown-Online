using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
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
	ItemProjection itemProjection,
	Action<ItemTrafficKind, string> recordTraffic,
	Func<ulong, string> itemTrafficLabel,
	Action<WorldItem> onItemSpawned,
	Action<ulong> onItemPickedUp,
	Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> onItemDropped,
	Action<ulong> onItemDestroyed,
	Action<ulong, WorldItem> onItemCookedReceived,
	Action<ulong, ItemRejectMsg.Reason> onItemRejected,
	IKernelProtocolControl kernelProtocol)
{
	private readonly ISessionControl _session = session;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<ItemService> _log = log;
	private readonly WorldItemTable _worldTable = worldTable;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly ItemActionSync _itemActionSync = itemActionSync;
	private readonly ItemSnapshotService _snapshots = snapshots;
	private readonly BlockDropSync _blockDrops = blockDrops;
	private readonly ItemPendingPickupArbiter _pendingPickups = pendingPickups;
	private readonly ItemProjection _projection = itemProjection;
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
			_projection.ApplySpawn(_session.LocalSteamId, itemId, item, pos, vel, rotation, freshItemDrop, angularVelocity);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		_recordTraffic(ItemTrafficKind.Spawn, item.ItemId);

		if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(new WireCommand
			{
				Kind = WireCommandKind.ItemSpawn,
				Identity = WireIdentity(itemId, item.ItemId),
				Location = WorldLocation(pos, 0),
				Data = ToWireItemData(item),
			}, WirePayloadType.ItemSpawnCommand);
		}
		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	public void SendItemCooked(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("ItemCook suppressed on guest (source {Source}, cooked {Cooked}) — the host owns heater conversions.", sourceItemId, cookedItemId);
			return;
		}

		_projection.ApplyCooked(_session.LocalSteamId, sourceItemId, cookedItemId, item, pos, vel, rotation, angularVelocity);

		if (!_session.SessionActive)
		{
			return;
		}

		// Cook is still a legacy presentation projection until Phase D's
		// cross-domain batch; the new kernel batch also carries the item facts,
		// but the dedicated cook event keeps the source→product link.
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
			_projection.ApplyPickup(_session.LocalSteamId, itemId);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		_recordTraffic(ItemTrafficKind.Pickup, evidence?.ItemId ?? _itemTrafficLabel(itemId));

		if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(new WireCommand
			{
				Kind = WireCommandKind.ItemPickup,
				Identity = WireIdentity(itemId, evidence?.ItemId ?? ""),
				NewOwner = _session.LocalSteamId,
				ExpectedRevision = 0,
			}, WirePayloadType.ItemPickupCommand);
			return;
		}

		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	public void SendItemUse(ulong itemId, CharacterItemMsg item) => _itemActionSync.SendItemUse(itemId, item);

	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item) => _itemActionSync.SendItemSlot(itemId, slotIndex, item);

	public void SendItemContainerContent(ulong itemId, CharacterItemMsg item) => _itemActionSync.SendItemContainerContent(itemId, item);

	public void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_projection.ApplyDrop(_session.LocalSteamId, itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		_recordTraffic(ItemTrafficKind.Drop, item.ItemId);

		if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(new WireCommand
			{
				Kind = WireCommandKind.ItemDrop,
				Identity = WireIdentity(itemId, item.ItemId),
				Location = WorldLocation(pos, parentItemId),
				Data = ToWireItemData(item),
				ExpectedRevision = 0,
			}, WirePayloadType.ItemDropCommand);
			return;
		}

		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	public void SendItemDestroyed(ulong itemId)
	{
		var trafficLabel = _itemTrafficLabel(itemId);
		if (_session.Role != SessionRole.Guest)
		{
			_projection.ApplyDestroy(_session.LocalSteamId, itemId);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		_recordTraffic(ItemTrafficKind.Destroy, trafficLabel);

		if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(new WireCommand
			{
				Kind = WireCommandKind.ItemDestroy,
				Identity = WireIdentity(itemId, ""),
				TerminalKind = WireTerminalKind.Destroyed,
				ExpectedRevision = 0,
			}, WirePayloadType.ItemDestroyCommand);
			return;
		}

		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	private static WireItemIdentity WireIdentity(ulong itemId, string definitionId) =>
		new()
		{
			InstanceId = itemId,
			DefinitionId = definitionId,
		};

	private static WireItemLocation WorldLocation(NetVector2 pos, ulong parentItemId) =>
		new()
		{
			Kind = WireItemLocationKind.World,
			X = pos.X,
			Y = pos.Y,
			ParentItemId = parentItemId,
		};

	private static WireItemData ToWireItemData(CharacterItemMsg item) =>
		KernelWireMapper.ToWireData(ItemKernelAuthority.ToKernelData(item));

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
			var isWorldItem = _worldTable.ContainsKey(itemId);
			var isOwnedCarriedItem = _arbitration.IsTransferredToGuest(sender, itemId);
			if (!isWorldItem && !isOwnedCarriedItem)
			{
				// A destroy report is only authoritative for a world item (any
				// peer may witness it) or a carried item the SENDER owns. A
				// remote-clone display proxy's OnDestroy used to report the
				// owner's real carried ids (the proxy children carry those ids);
				// accepting such a report for an item the sender does not own
				// would let a viewer empty the owner's bag through the host.
				_log.LogWarning("Item destroy {ItemId} from {Sender} ignored — not a world item and not owned by the sender.",
					itemId, sender);
				return;
			}

			if (isOwnedCarriedItem)
			{
				_arbitration.RemoveTransferred(sender, itemId);
				_log.LogInformation("Item destroy {ItemId} from owner {Sender} — removed from the transfer table.", itemId, sender);
			}

			_projection.ApplyDestroy(sender, itemId);
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

	public bool RegisterWorldItemIfAbsent(ulong itemId, WorldItem item) =>
		_projection.ApplyRegisterIfAbsent(_session.LocalSteamId, item);

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
