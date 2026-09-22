using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The world-item domain coordinator. It owns the authoritative world-item
/// table, the sub-services and the application events, and delegates the
/// report/receive message flow to <see cref="ItemMessageFlowService"/>. The
/// class is deliberately a facade over real top-level responsibilities rather
/// than a partial-logical god object.
/// </summary>
public sealed class ItemService : IItemControl, IItemActionWorldAccess, IWorldItemLayerReset, IDisposable, ISessionReset
{
	private readonly ISessionControl _session;
	private readonly ILogger<ItemService> _log;
	private readonly WorldItemTable _worldTable = new();
	private readonly ItemArbitration _arbitration;
	private readonly ItemCarriedSyncService _carriedSync;
	private readonly ItemActionSync _itemActionSync;
	private readonly ItemSnapshotService _snapshots;
	private readonly ItemIdCoordinator _idCoordinator;
	private readonly BlockDropSync _blockDrops;
	private readonly ItemTrafficTracker _itemTraffic = new(ItemTrafficTracker.DefaultWindowMs);
	private readonly ItemMessageFlowService _messageFlow;
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly ItemProjection _projection;
	private readonly KernelBatchItemProjection _kernelBatchProjection;
	private readonly IKernelProtocolControl _kernelProtocol;
	private readonly ItemSnapshotStreamReceiver _snapshotStreamReceiver;
	private readonly ProjectionHealthCoordinator _projectionHealth;
	private readonly RestoredWorldItemSet _restoredWorldItems;

	public ItemService(ISessionControl session, PacketSender sender, ItemArbitration arbitration, ITimeSource time, ILogger<ItemService> log, ItemKernelAuthority kernelAuthority, IKernelProtocolControl kernelProtocol, ProjectionHealthCoordinator projectionHealth, RefusedItemCreations refusedCreations, WorldRestoreAudit? audit = null)
	{
		_session = session;
		_log = log;
		_arbitration = arbitration;
		_kernelAuthority = kernelAuthority;
		_kernelProtocol = kernelProtocol;
		_projectionHealth = projectionHealth;
		_restoredWorldItems = new RestoredWorldItemSet(kernelAuthority, audit, log);
		_kernelProtocol.ItemMovesReceived += OnItemMovesReceived;
		_kernelProtocol.ItemStateStreamReceived += OnItemStateStreamReceived;
		_kernelProtocol.CommandRejected += OnCommandRejected;
		_kernelAuthority.ExternalBatchCommitted += OnExternalBatchCommitted;
		_kernelAuthority.BatchApplied += OnBatchApplied;
		_kernelAuthority.CheckpointRestored += OnCheckpointRestored;
		_projection = new ItemProjection(kernelAuthority, _worldTable);
		_kernelBatchProjection = new KernelBatchItemProjection(
			kernelAuthority,
			_worldTable,
			item => ItemSpawned?.Invoke(item),
			itemId => ItemPickedUp?.Invoke(itemId),
			(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos) =>
				ItemDropped?.Invoke(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos),
			itemId => ItemDestroyed?.Invoke(itemId),
			(owner, item, _) => PublishCarriedSyncLocal(owner, item),
			item => FireCorrectionLocal(item),
			(sourceId, cooked) => ItemCookedReceived?.Invoke(sourceId, cooked));
		_projectionHealth.Register(new ProjectionDomain("items", RebuildItemProjectionFromKernel, () => _kernelAuthority.CurrentGlobalRevision));
		_carriedSync = new ItemCarriedSyncService();
		_itemActionSync = new(session, this, _kernelProtocol);
		_snapshots = new(session, () => (IReadOnlyCollection<WorldItem>)_worldTable.Items.Values, _kernelProtocol, log);
		_snapshotStreamReceiver = new ItemSnapshotStreamReceiver(
			session,
			_kernelAuthority,
			log,
			(items, layerModifierIndex, randomState) => _snapshots.FireItemSnapshotReceived(session.HostSteamId, items, layerModifierIndex, randomState),
			(items, layerModifierIndex, randomState) => _snapshots.FireWorldItemsSnapshotReceived(session.HostSteamId, items, layerModifierIndex, randomState));
		_idCoordinator = new ItemIdCoordinator(session, sender, _arbitration, time, log);
		_blockDrops = new BlockDropSync(session, this);

		_messageFlow = new ItemMessageFlowService(
			session,
			_worldTable,
			_itemActionSync,
			_snapshots,
			_blockDrops,
			_projection,
			RecordItemTraffic,
			ItemTrafficLabel,
			item => ItemSpawned?.Invoke(item),
			refusedCreations,
			_kernelProtocol);

		session.SessionEnded += ResetSessionState;
	}

	// ===== Item-id coordination =====

	public void SendItemIdWatermark(ulong counter) => _idCoordinator.SendItemIdWatermark(counter);

	public void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items) => _idCoordinator.SendCarriedInventory(items);

	public void ArmCarriedInventoryRegistration(string reason) => _idCoordinator.ArmCarriedInventoryRegistration(reason);

	public bool IsCarriedInventoryRegistrationDue() => _idCoordinator.IsCarriedInventoryRegistrationDue();

	public void GrantItemIdWatermark(ulong targetSteamId, ulong counter) => _idCoordinator.GrantItemIdWatermark(targetSteamId, counter);

	public void FireItemIdWatermarkReceived(ulong sender, ulong counter) => _idCoordinator.FireItemIdWatermarkReceived(sender, counter);

	public void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items) => _idCoordinator.FireCarriedInventoryReceived(sender, items);

	public event Action<ulong, IReadOnlyList<CharacterItemMsg>>? CarriedInventoryReceived
	{
		add => _idCoordinator.CarriedInventoryReceived += value;
		remove => _idCoordinator.CarriedInventoryReceived -= value;
	}

	public event Action<WorldItem>? ItemSpawned;

	public event Action<ulong>? ItemPickedUp;

	public event Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2>? ItemDropped;

	public event Action<ulong>? ItemDestroyed;

	public event Action<ulong, WorldItem>? ItemCookedReceived;

	public event Action<ulong, ItemRejectMsg.Reason>? ItemRejected;

	public event Action<IReadOnlyList<WorldItem>, int, byte[]?>? ItemSnapshotReceived
	{
		add => _snapshots.ItemSnapshotReceived += value;
		remove => _snapshots.ItemSnapshotReceived -= value;
	}

	public event Action<IReadOnlyList<WorldItem>, int, byte[]?>? WorldItemsSnapshotReceived
	{
		add => _snapshots.WorldItemsSnapshotReceived += value;
		remove => _snapshots.WorldItemsSnapshotReceived -= value;
	}

	public event Action<IReadOnlyList<WireItemMoveEntry>>? ItemMoveReceived;

	public event Action<CharacterItemMsg>? ItemCorrectionReceived
	{
		add => _arbitration.ItemCorrectionReceived += value;
		remove => _arbitration.ItemCorrectionReceived -= value;
	}

	public event Action<ulong, CharacterItemMsg, bool>? ItemCarriedSyncReceived
	{
		add => _carriedSync.ItemCarriedSyncReceived += value;
		remove => _carriedSync.ItemCarriedSyncReceived -= value;
	}

	public event Action<ulong>? ItemIdWatermarkReceived
	{
		add => _idCoordinator.ItemIdWatermarkReceived += value;
		remove => _idCoordinator.ItemIdWatermarkReceived -= value;
	}

	// ===== Report/receive flow =====

	public void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity) =>
		_messageFlow.SendItemSpawned(itemId, item, pos, vel, rotation, freshItemDrop, angularVelocity);

	public void SendItemCooked(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("ItemCook suppressed on guest (source {Source}, cooked {Cooked}) — the host owns heater conversions.", sourceItemId, cookedItemId);
			return;
		}

		var cookedIdentity = new ItemIdentity(cookedItemId, item.ItemId);
		if (!_kernelAuthority.TryCook(
				_session.LocalSteamId,
				sourceItemId,
				cookedIdentity,
				ItemLocation.World(pos.X, pos.Y),
				item,
				out var batch,
				out var rejection))
		{
			_log.LogWarning("ItemCook rejected: {Reason} ({Message}).", rejection!.Reason, rejection.Message);
			return;
		}

		_kernelBatchProjection.ApplyWorldTableOnly(batch!);
	}

	public void SendItemPickedUp(ulong itemId, CharacterItemMsg? evidence = null) =>
		_messageFlow.SendItemPickedUp(itemId, evidence);

	public void SendItemUse(ulong itemId, CharacterItemMsg item) => _messageFlow.SendItemUse(itemId, item);

	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item) => _messageFlow.SendItemSlot(itemId, slotIndex, item);

	public void SendItemContainerContent(ulong itemId, CharacterItemMsg item) => _messageFlow.SendItemContainerContent(itemId, item);

	public void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f) =>
		_messageFlow.SendItemDropped(itemId, item, pos, vel, parentItemId, rotation, parentPos, angularVelocity);

	public void SendItemDestroyed(ulong itemId) => _messageFlow.SendItemDestroyed(itemId);

	public void RegisterPendingCreationSource(IPendingItemCreationSource source) => _messageFlow.RegisterPendingCreationSource(source);

	public void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops) => _messageFlow.RegisterBlockDrops(drops);

	public void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops) => _messageFlow.FireBlockDropsReceived(sender, drops);

	public void RegisterBuildingDrops(IReadOnlyList<TrapDropEntryMsg> drops) => _messageFlow.RegisterBuildingDrops(drops);

	public void FireBuildingDropsReceived(ulong sender, IReadOnlyList<TrapDropEntryMsg> drops) => _messageFlow.FireBuildingDropsReceived(sender, drops);

	public void SendItemReject(ulong targetSteamId, ulong itemId, ItemRejectMsg.Reason reason) =>
		_messageFlow.SendItemReject(targetSteamId, itemId, reason);

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState) =>
		_messageFlow.FireItemSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);

	public void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState) =>
		_messageFlow.FireWorldItemsSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);

	// ===== Host-authoritative position stream =====

	public void SendItemMove(IReadOnlyList<WireItemMoveEntry> items)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || items.Count == 0)
		{
			return;
		}

		foreach (var entry in items)
		{
			RecordItemTraffic(ItemTrafficKind.Move, ItemTrafficLabel(entry.ItemId));
		}

		_kernelProtocol.SendStateStream([.. items]);
	}

	public void FireItemMoveReceived(IReadOnlyList<WireItemMoveEntry> items) => ItemMoveReceived?.Invoke(items);

	// ===== Carried-item facts =====

	public void SendItemCarriedSync(ulong ownerSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || item.InstanceId == 0)
		{
			return;
		}

		var current = _kernelAuthority.FindItem(item.InstanceId);
		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(ownerSteamId, item.InstanceId, item.ItemId, item, out _, out _);
		}
		else
		{
			_kernelAuthority.TryUpdateState(ownerSteamId, item.InstanceId, item, out _, out _);
		}

		PublishCarriedSyncLocal(ownerSteamId, item);
	}

	public void FireItemCarriedSyncReceived(ulong sender, ulong ownerSteamId, CharacterItemMsg item, bool slotKnown)
		=> _carriedSync.FireItemCarriedSyncReceived(sender, ownerSteamId, item, slotKnown);

	private void PublishCarriedSync(ulong ownerSteamId, CharacterItemMsg item) => SendItemCarriedSync(ownerSteamId, item);

	// ===== Host-only surface =====

	public int LayerModifierIndex
	{
		get => _snapshots.LayerModifierIndex;
		set => _snapshots.LayerModifierIndex = value;
	}

	public byte[]? LayerModifierRandomState
	{
		get => _snapshots.LayerModifierRandomState;
		set => _snapshots.LayerModifierRandomState = value;
	}

	public void SendWorldItemCorrection(ulong exceptSteamId, CharacterItemMsg item) => _itemActionSync.SendWorldItemCorrection(exceptSteamId, item);

	public IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId) => _arbitration.GetTransferredItems(steamId);

	public void SendItemSnapshot(ulong targetSteamId) => _snapshots.SendItemSnapshot(targetSteamId);

	public void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation, float condition)
	{
		if (_session.Role == SessionRole.Guest)
		{
			return;
		}

		_projection.ApplyRefresh(itemId, pos, vel, rotation, condition);
	}

	public void SendPeriodicItemSnapshot() => _snapshots.SendPeriodicItemSnapshot();

	/// <summary>
	/// Host/solo: a new layer is generating — the world-item table starts empty. The
	/// one exception is the generation a restore drives: there the restored item set
	/// IS this layer's table, so clearing it would leave the host with an empty table
	/// for the whole layer (the suppressed publish does not rebuild it, and an empty
	/// table also skips the periodic keyframe).
	/// </summary>
	public void ResetItems()
	{
		if (RestoredWorldItemsPending)
		{
			_log.LogInformation("[Restore] generation reset skipped: the restored item set IS this layer's world table ({Count} item(s)).", _worldTable.Items.Count);
			return;
		}

		_projection.Clear();
	}

	// ===== Restored world items (mid-run restore) =====
	// The set, its pending expectation and the audit account live in
	// RestoredWorldItemSet; this surface only exposes them to the adapter.

	public bool RestoredWorldItemsPending => _restoredWorldItems.Pending;

	public IReadOnlyList<WorldItem> ReadRestoredWorldItems() => _restoredWorldItems.Read();

	public void CompleteRestoredWorldItems(int applied, IReadOnlyList<string> refused) =>
		_restoredWorldItems.Complete(applied, refused);

	public void CancelRestoredWorldItems(string reason) => _restoredWorldItems.Cancel(reason);

	/// <summary>
	/// Host/solo: a new layer is generating. The previous layer's world items are
	/// gone with its scene, so the authoritative kernel drops every world-rooted
	/// record here and the batch travels to the guests (their replay kernels reset
	/// at the same boundary). Carried items cross the boundary and stay.
	/// </summary>
	void IWorldItemLayerReset.ResetForNewLayer()
	{
		if (_session.Role == SessionRole.Guest)
		{
			return; // the host owns the layer tables; the guest applies the committed reset batch
		}

		if (!_kernelAuthority.TryResetWorldItems(_session.LocalSteamId, out var batch, out var rejection))
		{
			_log.LogWarning("[LayerReset] the item reset for the new layer was rejected: {Reason} ({Message}).",
				rejection!.Reason, rejection.Message);
			return;
		}

		_kernelBatchProjection.ApplyWorldTableOnly(batch!);
		_log.LogInformation("[LayerReset] dropped the previous layer's world-rooted items; {Remaining} item record(s) remain (carried items cross the boundary).",
			_kernelAuthority.QueryItems().Count);
	}

	public void ResetSessionState()
	{
		CancelRestoredWorldItems("the session ended before the generation reconcile ran");
		_projection.Clear();
		_arbitration.ResetSessionState();
		_idCoordinator.ResetSessionState();
		_snapshots.ResetSessionState();
		_itemTraffic.Reset();
		_snapshotStreamReceiver.Reset();
	}


	public void Dispose()
	{
		_kernelProtocol.ItemMovesReceived -= OnItemMovesReceived;
		_kernelProtocol.ItemStateStreamReceived -= OnItemStateStreamReceived;
		_kernelProtocol.CommandRejected -= OnCommandRejected;
		_kernelAuthority.ExternalBatchCommitted -= OnExternalBatchCommitted;
		_kernelAuthority.BatchApplied -= OnBatchApplied;
		_kernelAuthority.CheckpointRestored -= OnCheckpointRestored;
		_session.SessionEnded -= ResetSessionState;
		_idCoordinator.Dispose();
	}

	// ===== Generation-time items =====

	public void PublishGeneratedItems(IReadOnlyList<WorldItem> entries)
	{
		if (_session.Role == SessionRole.Guest || entries.Count == 0)
		{
			return;
		}

		if (RestoredWorldItemsPending)
		{
			// A mid-run restore owns this layer's item set: the objects the game just
			// regenerated are the SAME physical objects the cut described, so the
			// generation reconciles them against the restored ids
			// (GeneratedItemAuthority) instead of publishing them under fresh ones.
			// Publishing here is what left two item families at one spot and put a
			// duplicate beside the restored ground copy.
			_log.LogInformation("[Restore] generation publish suppressed: the restored world-item set is pending reconcile ({Count} generated entries).", entries.Count);
			return;
		}

		var registered = 0;
		foreach (var entry in entries)
		{
			if (_worldTable.ContainsKey(entry.ItemId))
			{
				continue;
			}

			var accepted = _projection.ApplySpawn(_session.LocalSteamId, entry.ItemId, entry.Item,
				entry.Pos, entry.Vel,
				entry.Rotation, entry.FreshItemDrop, entry.AngularVelocity);
			if (accepted)
			{
				registered++;
			}
		}

		_kernelProtocol.BroadcastItemStateStream(
			[.. entries.Select(WireItemStateMapper.ToWire)],
			WirePayloadType.WorldItemsSnapshotStream,
			reliable: true,
			layerModifierIndex: LayerModifierIndex + 1,
			layerModifierRandomState: LayerModifierRandomState);
		_log.LogInformation("Published generation items ({Count} entries, {Registered} registered).",
			entries.Count, registered);
	}

	// ===== Crafting-domain seams =====

	internal void RemoveWorldItemLocal(ulong itemId)
	{
		_projection.ApplyDestroy(_session.LocalSteamId, itemId);
		ItemDestroyed?.Invoke(itemId);
	}

	internal void UpdateWorldItemState(ulong itemId, CharacterItemMsg state) =>
		_projection.ApplyUpdateState(_session.LocalSteamId, itemId, state);

	internal void PublishCarriedSyncFor(ulong owner, CharacterItemMsg item) => PublishCarriedSync(owner, item);

	internal void PublishCarriedSyncLocal(ulong owner, CharacterItemMsg item) => _carriedSync.PublishLocal(owner, item);

	internal void FireCorrectionLocal(CharacterItemMsg item) => _arbitration.FireCorrectionReceived(item);

	// ===== Direct player-interaction forwarding =====

	public void AdoptTransferredItem(ulong guest, ulong itemId, CharacterItemMsg item) =>
		_arbitration.AdoptTransferredItem(guest, itemId, item);

	public void RemoveTransferredItem(ulong guest, ulong itemId) =>
		_arbitration.RemoveTransferredItem(guest, itemId);

	public void UpdateTransferredItem(ulong guest, ulong itemId, CharacterItemMsg item) =>
		_arbitration.UpdateTransferredItem(guest, itemId, item);

	// ===== Traffic =====

	internal void RecordItemTraffic(ItemTrafficKind kind, string itemLabel)
	{
		if (_session.SessionActive)
		{
			_itemTraffic.Record(kind, itemLabel);
		}
	}

	internal void PumpItemTraffic(long nowMs)
	{
		if (_itemTraffic.TryCollectWindow(nowMs, out var window) && window.Total > 0)
		{
			_log.LogInformation("[ItemTraffic] {Window}", ItemTrafficWindowLog.Format(window));
		}
	}

	internal ItemTrafficWindow CurrentItemTraffic => _itemTraffic.Snapshot();

	internal string ItemTrafficLabel(ulong itemId) =>
		_worldTable.TryGetValue(itemId, out var entry) ? entry.Item.ItemId : $"#{itemId}";

	internal void ResetItemTraffic() => _itemTraffic.Reset();

	internal bool RegisterWorldItemIfAbsent(ulong itemId, WorldItem item) => _messageFlow.RegisterWorldItemIfAbsent(itemId, item);

	internal bool IsWorldItemRegistered(ulong itemId) => _messageFlow.IsWorldItemRegistered(itemId);

	internal void FireItemSpawned(WorldItem item) => _messageFlow.FireItemSpawned(item);

	/// <summary>Read-only world-table snapshot for kernel comparison diagnostics (never mutates production state).</summary>
	internal IReadOnlyList<WorldItem> GetWorldItemsForDiagnostics() => [.. _worldTable.Items.Values];


	// ===== Phase C guest batch projection =====

	private void OnExternalBatchCommitted(CommittedBatch batch)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_projectionHealth.Run("items", batch.GlobalRevision, () =>
		{
			_kernelBatchProjection.Apply(batch);
			_arbitration.RebuildCarriedTableFromKernel();
		});
	}

	private void OnBatchApplied(CommittedBatch batch)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_projectionHealth.Run("items", batch.GlobalRevision, () =>
		{
			_kernelBatchProjection.Apply(batch);
			_kernelBatchProjection.FireCookedEventFromBatch(batch);
		});
	}

	private void OnCheckpointRestored(GameCheckpoint checkpoint)
	{
		if (_session.Role != SessionRole.Guest)
		{
			// Host/solo: the archive's world items ARE this world's item set — the layer
			// that follows is regenerated from the restored baseline, so the generation
			// reconciles its objects against these ids (RestoredWorldItemSet). A
			// layer-end cut never gets here: the restore applier cancels the expectation.
			_projectionHealth.Run("items", checkpoint.GlobalRevision, () => _restoredWorldItems.ArmForRestoredCut(checkpoint, _kernelBatchProjection));
			return;
		}

		_projectionHealth.Run("items", checkpoint.GlobalRevision, () => _kernelBatchProjection.Rebuild(checkpoint));
	}

	private void RebuildItemProjectionFromKernel()
	{
		_kernelBatchProjection.RebuildFromKernel();
		if (_session.Role == SessionRole.Host)
		{
			_arbitration.RebuildCarriedTableFromKernel();
		}
	}

	private void OnItemMovesReceived(IReadOnlyList<WireItemMoveEntry> moves)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		FireItemMoveReceived(moves);
	}

	private void OnCommandRejected(ulong itemId, RejectionReason reason)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		var mappedReason = reason switch
		{
			RejectionReason.BlockAlreadyBroken => ItemRejectMsg.Reason.BlockAlreadyBroken,
			_ => ItemRejectMsg.Reason.UnknownItem,
		};
		_log.LogWarning("Kernel command for item {ItemId} rejected ({Reason}) — surfacing item reject.", itemId, reason);
		ItemRejected?.Invoke(itemId, mappedReason);
	}

	private void OnItemStateStreamReceived(WirePayloadType payloadType, WireStateStream stream) =>
		_snapshotStreamReceiver.Handle(_session.HostSteamId, payloadType, stream);

	// ===== IItemActionWorldAccess =====

	bool IItemActionWorldAccess.IsWorldItem(ulong itemId) => IsWorldItemRegistered(itemId);

	void IItemActionWorldAccess.UpdateWorldItemState(ulong itemId, CharacterItemMsg state) => UpdateWorldItemState(itemId, state);

	void IItemActionWorldAccess.PublishCarriedSyncFor(ulong owner, CharacterItemMsg item) => PublishCarriedSyncFor(owner, item);

	void IItemActionWorldAccess.FireCorrectionLocal(CharacterItemMsg item) => FireCorrectionLocal(item);
}
