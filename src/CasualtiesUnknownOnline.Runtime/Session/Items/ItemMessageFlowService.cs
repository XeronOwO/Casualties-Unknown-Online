using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Application.Kernel;

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

	/// <summary>
	/// ONE item came out of a container into this client's own carried inventory. The kernel
	/// fact is the pickup's relocation (the row becomes <c>Carried(sender)</c>), but it
	/// travels under its own kind: a refused pickup is a world item the sender may not have,
	/// whose local copy goes back to the world, while a refused take-out is an item IN this
	/// client's hands — unparenting it would be the opposite of a settlement, so its way back
	/// is the carried-set statement (<see cref="ItemCommandRejectionRouter"/>). A host's own
	/// take-out is its own authority, written locally and never reported.
	/// </summary>
	public void SendItemContainerTakeOut(ulong itemId, CharacterItemMsg? evidence = null)
	{
		_pendingCreations.SettleBeforeOperation();
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
				Kind = WireCommandKind.ItemContainerTakeOut,
				Identity = WireIdentity(itemId, evidence?.ItemId ?? ""),
				NewOwner = _session.LocalSteamId,
				ExpectedRevision = 0,
			}, WirePayloadType.ItemContainerTakeOutCommand);
		}

		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	public void SendItemUse(ulong itemId, CharacterItemMsg item)
	{
		_pendingCreations.SettleBeforeOperation();
		_itemActionSync.SendItemUse(itemId, item);
	}

	/// <summary>An item's state changed without a use claim (the gun chain's transitions): the same pending-creation settlement as the other item operations, then the plain state report.</summary>
	public void SendItemState(ulong itemId, CharacterItemMsg item)
	{
		_pendingCreations.SettleBeforeOperation();
		_itemActionSync.SendItemState(itemId, item);
	}

	/// <summary>
	/// A console command wrote one item's state. Deliberately NOT settled against the
	/// pending creations first, unlike the native operations beside it: the verb
	/// resolves its target from the kernel record, and a row in that record IS this
	/// host's judgement of the item, so a console write can never be the first report
	/// of an id (an unrecorded target is refused by the verb by name).
	/// </summary>
	public void SendItemDataWrite(ulong itemId, CharacterItemMsg item) => _itemActionSync.SendItemDataWrite(itemId, item);

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

	/// <summary>
	/// The applied family's two reports, host and guest alike: the local body's own
	/// limb action turned a carried item into the limb's component, or brought the
	/// component back as an item. Both are RELOCATIONS that keep the instance id, so
	/// neither is the destroy/spawn pair the generic hooks would report — which is
	/// exactly why they are reported from the seam that observed the native call
	/// rather than from <c>Item.OnDestroy</c> / <c>Item.Start</c>.
	/// </summary>
	public void SendItemAppliedToLimb(ulong itemId, int limbIndex)
	{
		_pendingCreations.SettleBeforeOperation();
		if (itemId == 0 || limbIndex < 0)
		{
			return;
		}

		if (_session.Role != SessionRole.Guest)
		{
			_projection.ApplyLimbItem(_session.LocalSteamId, itemId, new ActorId(_session.LocalSteamId), limbIndex, null);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(new WireCommand
			{
				Kind = WireCommandKind.ItemApplyLimb,
				Identity = WireIdentity(itemId, ""),
				Location = AppliedLocation(_session.LocalSteamId, limbIndex),
				ExpectedRevision = 0,
			}, WirePayloadType.ItemApplyLimbCommand);
		}

		// Host broadcasts are unnecessary: the accepted kernel batch is already
		// broadcast by KernelProtocolService.
	}

	public void SendItemRemovedFromLimb(ulong itemId, int limbIndex, CharacterItemMsg? item = null)
	{
		_pendingCreations.SettleBeforeOperation();
		if (itemId == 0 || limbIndex < 0)
		{
			return;
		}

		if (_session.Role != SessionRole.Guest)
		{
			_projection.ApplyLimbItemRemoval(
				_session.LocalSteamId,
				itemId,
				new ActorId(_session.LocalSteamId),
				limbIndex,
				new ActorId(_session.LocalSteamId),
				item);
		}

		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(new WireCommand
			{
				Kind = WireCommandKind.ItemRemoveLimb,
				Identity = WireIdentity(itemId, ""),
				Location = AppliedLocation(_session.LocalSteamId, limbIndex),
				NewOwner = _session.LocalSteamId,
				ExpectedRevision = 0,
				// The revived object's own state travels with the relocation: without it the row
				// keeps the pre-apply slot and the last applied condition (batch 20261018-v's
				// review). Null stays the "the caller could not see the object" case, which the
				// kernel reads as "keep the row's data".
				Data = item is null ? null : ToWireItemData(item),
			}, WirePayloadType.ItemRemoveLimbCommand);
		}
	}

	private static WireItemLocation AppliedLocation(ulong owner, int limbIndex) =>
		new()
		{
			Kind = WireItemLocationKind.Applied,
			Owner = owner,
			LimbIndex = limbIndex,
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
			ItemRejectMsg.Reason.TrapDropUnaccounted => RejectionReason.TrapDropUnaccounted,
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

	// ===== World-item impact presentations (one-shot: the authority's landing) =====

	/// <summary>An authority's world-item impact arrived — the receiver replays the presentation on its own world.</summary>
	public event Action<ulong, ItemImpactMsg>? ItemImpactReceived;

	public void FireItemImpactReceived(ulong sender, ItemImpactMsg msg) =>
		ItemImpactReceived?.Invoke(sender, msg);

	/// <summary>
	/// Report a world-item impact presentation the authority's native collision
	/// call just played: host → every synced member. A guest never sends — its
	/// world-item copies are the non-authoritative half whose collision
	/// presentation is suppressed, so the host's copy is the only source of the
	/// landing. One impact = one message; the presentation is transient and has no
	/// snapshot fallback.
	/// </summary>
	public void SendItemImpact(ItemImpactMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.ItemImpact, msg);
	}
}
