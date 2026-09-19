using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The report/receive message-flow surface of the item domain: local-compute
/// report sends (spawn/drop/use/cook/destroy), wire receive events, block-break
/// drop registration and snapshot/action receive forwarding. The production
/// item network path is the Phase C envelope protocol; block-break drop refusal
/// also rides `KernelEnvelope` `CommandRejected`.
/// </summary>
internal sealed class ItemMessageFlowService(
	ISessionControl session,
	WorldItemTable worldTable,
	ItemActionSync itemActionSync,
	ItemSnapshotService snapshots,
	BlockDropSync blockDrops,
	ItemProjection itemProjection,
	Action<ItemTrafficKind, string> recordTraffic,
	Func<ulong, string> itemTrafficLabel,
	Action<WorldItem> onItemSpawned,
	RefusedItemCreations refusedCreations,
	IKernelProtocolControl kernelProtocol)
{
	private readonly ISessionControl _session = session;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly WorldItemTable _worldTable = worldTable;
	private readonly ItemActionSync _itemActionSync = itemActionSync;
	private readonly ItemSnapshotService _snapshots = snapshots;
	private readonly BlockDropSync _blockDrops = blockDrops;
	private readonly ItemProjection _projection = itemProjection;
	private readonly Action<ItemTrafficKind, string> _recordTraffic = recordTraffic;
	private readonly Func<ulong, string> _itemTrafficLabel = itemTrafficLabel;
	private readonly Action<WorldItem> _onItemSpawned = onItemSpawned;
	private readonly RefusedItemCreations _refusedCreations = refusedCreations;

	/// <summary>The creation-before-operation gate: a deferred creation report is settled before any operation report on an item leaves this side.</summary>
	private readonly PendingItemCreations _pendingCreations = new(session);

	// ===== Report side (local compute) =====

	/// <summary>Composition seam: the Game Adapter registers the owners of its deferred creation reports.</summary>
	public void RegisterPendingCreationSource(IPendingItemCreationSource source) => _pendingCreations.Register(source);

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
				VelocityX = vel.X,
				VelocityY = vel.Y,
				Rotation = rotation,
				FreshItemDrop = freshItemDrop,
				AngularVelocity = angularVelocity,
			}, WirePayloadType.ItemSpawnCommand);
		}
		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	public void SendItemPickedUp(ulong itemId, CharacterItemMsg? evidence = null)
	{
		_pendingCreations.SettleBeforeOperation(); // the host judges an operation only after the item's creation — settle the deferred creation reports first
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

	public void SendItemUse(ulong itemId, CharacterItemMsg item)
	{
		_pendingCreations.SettleBeforeOperation();
		_itemActionSync.SendItemUse(itemId, item);
	}

	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item)
	{
		_pendingCreations.SettleBeforeOperation();
		_itemActionSync.SendItemSlot(itemId, slotIndex, item);
	}

	public void SendItemContainerContent(ulong itemId, CharacterItemMsg item)
	{
		_pendingCreations.SettleBeforeOperation();
		_itemActionSync.SendItemContainerContent(itemId, item);
	}

	public void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f)
	{
		_pendingCreations.SettleBeforeOperation(); // a drop reports an operation on the item AND its world fact — the creation must be judged first
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
		_pendingCreations.SettleBeforeOperation();
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

	// ===== Block-break drops =====

	public void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops) => _blockDrops.RegisterBlockDrops(drops);

	public void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops) => _blockDrops.FireBlockDropsReceived(sender, drops);

	public void RegisterBuildingDrops(IReadOnlyList<TrapDropEntryMsg> drops) => _blockDrops.RegisterBuildingDrops(drops);

	public void FireBuildingDropsReceived(ulong sender, IReadOnlyList<TrapDropEntryMsg> drops) => _blockDrops.FireBuildingDropsReceived(sender, drops);

	public void SendItemReject(ulong targetSteamId, ulong itemId, ItemRejectMsg.Reason reason)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var rejection = reason switch
		{
			ItemRejectMsg.Reason.BlockAlreadyBroken => RejectionReason.BlockAlreadyBroken,
			_ => RejectionReason.UnknownAggregate,
		};

		// The creation of this id is dead. Remember it, so a later operation on the
		// same id is answered with THIS reason at once — and is never confused with
		// an operation whose creation was never reported (a protocol violation).
		// Recorded only for a refusal this host actually delivers (the guards above
		// and the target check below), so a guest-side or target-less call cannot
		// plant a tombstone for an id nothing was refused on.
		if (targetSteamId != 0)
		{
			_refusedCreations.Record(itemId, rejection);
		}

		_kernelProtocol.SendCommandRejected(targetSteamId, itemId, rejection);
	}

	public bool RegisterWorldItemIfAbsent(ulong itemId, WorldItem item) =>
		_projection.ApplyRegisterIfAbsent(_session.LocalSteamId, item);

	public bool IsWorldItemRegistered(ulong itemId) => _worldTable.ContainsKey(itemId);

	public void FireItemSpawned(WorldItem item) => _onItemSpawned(item);

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireItemSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);

	public void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireWorldItemsSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);
}
