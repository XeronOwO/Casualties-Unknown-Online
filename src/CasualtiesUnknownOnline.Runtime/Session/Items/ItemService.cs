using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The world-item domain: runtime-generated items in the world (drops, loot,
/// placed items) with the host (and solo play — late-joiner parity) keeping
/// the authoritative table. Local compute → report up / register → relay down,
/// the star-network pattern: the spawner applies locally, the host arbitrates
/// (pickups are first-writer-wins against the table) and relays to the other
/// members. Generation-time items never enter the table — world-gen
/// determinism covers them. ItemService itself has no pump (not an ICuoService,
/// like WorldService); the pending-pickup hold window's time edge lives in
/// PendingPickupPump, and its integration lives in ItemService.PendingPickups.cs.
/// </summary>
public sealed partial class ItemService : IItemControl, IItemActionWorldAccess, IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ILogger<ItemService> _log;

	/// <summary>The authoritative world-item table (instance id → item) — the state lives in WorldItemTable.</summary>
	private readonly WorldItemTable _worldTable = new();

	private readonly ItemArbitration _arbitration;
	private readonly ItemCarriedSyncService _carriedSync;
	private readonly ItemActionSync _itemActionSync;
	private readonly ItemSnapshotService _snapshots;
	private readonly ItemIdCoordinator _idCoordinator;
	private readonly BlockDropSync _blockDrops;

	public ItemService(ISessionControl session, PacketSender sender, ItemArbitration arbitration, ITimeSource time, ILogger<ItemService> log)
	{
		_session = session;
		_sender = sender;
		_log = log;
		_time = time;
		_arbitration = arbitration; // DI-registered — the crafting domain composes the same instance (RemoveTransferred/AdoptEvidence/RegisterCarried)
		_pendingPickups = new(PendingPickupQueue.DefaultHoldMs);
		_carriedSync = new(session, sender, log);
		_itemActionSync = new(session, sender, arbitration, this, log); // the use/slot action flows — this is their narrow world access (abstract extraction)
		_snapshots = new(session, sender, () => (IReadOnlyCollection<WorldItem>)_worldTable.Items.Values, log);
		_idCoordinator = new ItemIdCoordinator(session, sender, _arbitration, log);
		_blockDrops = new BlockDropSync(session, this);

		// The item domain is session-scoped: a lobby switch must never carry a
		// world/transfer table, watermark or modifier projection into the new
		// lobby. The host session survives a guest leaving, so reconnect
		// recovery keeps its state.
		session.SessionEnded += OnSessionEnded;
	}

	// ===== Item-id coordination (watermarks + carried inventory) — the state and the docs live in ItemIdCoordinator =====

	public void SendItemIdWatermark(ulong counter) => _idCoordinator.SendItemIdWatermark(counter);

	public void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items) => _idCoordinator.SendCarriedInventory(items);

	public void GrantItemIdWatermark(ulong targetSteamId, ulong counter) => _idCoordinator.GrantItemIdWatermark(targetSteamId, counter);

	public void FireItemIdWatermarkReceived(ulong sender, ulong counter) => _idCoordinator.FireItemIdWatermarkReceived(sender, counter);

	public void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items) => _idCoordinator.FireCarriedInventoryReceived(sender, items);

	/// <summary>Host side: a guest's self-assigned carried inventory arrived — the adapter merges it into the guest's fact table.</summary>
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

	public event Action<IReadOnlyList<ItemSnapshotEntryMsg>, int, byte[]?>? WorldItemsSnapshotReceived
	{
		add => _snapshots.WorldItemsSnapshotReceived += value;
		remove => _snapshots.WorldItemsSnapshotReceived -= value;
	}

	public event Action<IReadOnlyList<ItemMoveEntryMsg>>? ItemMoveReceived;

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

	// ===== Host-authoritative position stream =====

	/// <summary>Host only: broadcast EVERY world item's authoritative position (unreliable — drops are harmless, the next tick overwrites; the host's physics is the single position authority, the guests' copies are kinematic renders that follow).</summary>
	public void SendItemMove(IReadOnlyList<ItemMoveEntryMsg> items)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || items.Count == 0)
		{
			return;
		}

		foreach (var entry in items)
		{
			RecordItemTraffic(ItemTrafficKind.Move, ItemTrafficLabel(entry.ItemId));
		}

		var msg = new ItemMoveMsg { Items = [.. items] };
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.ItemMove, msg, reliable: false);
	}

	public void FireItemMoveReceived(IReadOnlyList<ItemMoveEntryMsg> items) => ItemMoveReceived?.Invoke(items);

	// ===== Carried-item facts (host → guest events: use/slot move/pickup) — the wire surface lives in ItemCarriedSyncService =====

	public void SendItemCarriedSync(ulong ownerSteamId, CharacterItemMsg item) => _carriedSync.SendItemCarriedSync(ownerSteamId, item);

	public void FireItemCarriedSyncReceived(ulong sender, ulong ownerSteamId, CharacterItemMsg item, bool slotKnown)
		=> _carriedSync.FireItemCarriedSyncReceived(sender, ownerSteamId, item, slotKnown);

	/// <summary>Host only: an arbitration adopted/recorded a carried item's new fact — apply it locally and broadcast it to the peers.</summary>
	private void PublishCarriedSync(ulong ownerSteamId, CharacterItemMsg item) => _carriedSync.Publish(ownerSteamId, item);

	// ===== Host-only surface =====

	/// <summary>The world's current layer modifier projection — rides the snapshots (see ItemSnapshotService).</summary>
	public int LayerModifierIndex
	{
		get => _snapshots.LayerModifierIndex;
		set => _snapshots.LayerModifierIndex = value;
	}

	/// <summary>The modifier decision's random start — rides the snapshots (see ItemSnapshotService).</summary>
	public byte[]? LayerModifierRandomState
	{
		get => _snapshots.LayerModifierRandomState;
		set => _snapshots.LayerModifierRandomState = value;
	}

	public void SendItemCorrection(ulong targetSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_arbitration.SendCorrection(targetSteamId, item);
	}

	/// <summary>Host only: correct every OTHER member's copy of a used world item — the user's own copy IS the fact.</summary>
	public void SendWorldItemCorrection(ulong exceptSteamId, CharacterItemMsg item) => _itemActionSync.SendWorldItemCorrection(exceptSteamId, item);

	public IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId) => _arbitration.GetTransferredItems(steamId);

	public void SendItemSnapshot(ulong targetSteamId) => _snapshots.SendItemSnapshot(targetSteamId);

	/// <summary>Host only: the item's live state — the periodic keyframe must broadcast the CURRENT positions and condition, not the spawn-time ones (the spawn position would pull settled items back into the air every tick; a stale condition would re-align the peers' decay to the wrong value).</summary>
	public void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation, float condition)
	{
		if (_session.Role == SessionRole.Guest || !_worldTable.TryGetValue(itemId, out var w))
		{
			return;
		}

		w.Item.Condition = condition;
		_worldTable.Set(itemId, w with { Pos = pos, Vel = vel, Rotation = rotation });
	}

	/// <summary>Host only: periodically re-send the full table (unreliable) — see ItemSnapshotService.</summary>
	public void SendPeriodicItemSnapshot() => _snapshots.SendPeriodicItemSnapshot();

	public void ResetItems()
	{
		_worldTable.Clear();
		_pendingPickups.Reset(); // a new layer voids every in-flight claim from the old world
	}

	/// <summary>Session ended: every session-scoped item table dies with it.</summary>
	public void ResetSessionState()
	{
		_worldTable.Clear();
		_pendingPickups.Reset();
		_arbitration.ResetForSessionEnd();
		_idCoordinator.ResetForSessionEnd();
		_snapshots.ResetForSessionEnd();
		_itemTraffic.Reset();
	}

	private void OnSessionEnded() => ResetSessionState();

	public void Dispose() => _session.SessionEnded -= OnSessionEnded;

	/// <summary>Host only: the generation finished — the host assigned ids to the generation-time items and hands the full set over. Registered silently (no ItemSpawned event — the local copies exist) and broadcast as ONE reliable snapshot (see GeneratedItemAuthority/Application).</summary>
	public void PublishGeneratedItems(IReadOnlyList<ItemSnapshotEntryMsg> entries)
	{
		if (_session.Role == SessionRole.Guest || entries.Count == 0)
		{
			return;
		}

		var registered = 0;
		foreach (var entry in entries)
		{
			// A carried entry (starting supplies, SlotIndex > 0 — the wire
			// encoding of a backpack slot is slotIndex + 1, see
			// ItemSnapshotEntryMsg.SlotIndex) has NO table entry — it lives in
			// a backpack until a drop brings it into the world (the drop report
			// registers it then, the standard path). SlotIndex 0 IS a world
			// item: protobuf-net omits the 0-valued wire field, so the encoded
			// -1 + 1 arrives as 0 — never treat 0 as a slot.
			if (entry.SlotIndex > 0 || _worldTable.ContainsKey(entry.ItemId))
			{
				continue;
			}

			_worldTable.Set(entry.ItemId, new WorldItem(entry.ItemId, entry.Item,
				entry.Position.ToNetVector2(), entry.Velocity.ToNetVector2(),
				entry.ParentItemId, entry.Rotation, entry.FreshItemDrop));
			registered++;
		}

		_session.Broadcast(NetMsg.WorldItemsSnapshot, new WorldItemsSnapshotMsg
		{
			Items = [.. entries],
			// Wire encoding is modifierIndex + 1 (0 = none) — see SendItemSnapshot.
			LayerModifierIndex = LayerModifierIndex + 1,
			LayerModifierRandomState = LayerModifierRandomState,
		});
		_log.LogInformation("Published generation items ({Count} entries, {Registered} registered): {World} ground, {Carried} carried — modifier {Modifier}.",
			entries.Count, registered,
			entries.Count(e => e.SlotIndex == 0), entries.Count(e => e.SlotIndex > 0), LayerModifierIndex);
	}

	public void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireWorldItemsSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);

	// ===== Crafting-domain seams — the craft apply (CraftSyncService) composes these; it cannot live
	// here: this file sits at the 600-line architecture gate. Role-agnostic where possible (the craft
	// relay applies on the host AND on the guests — their world tables are empty, the event is the point).

	// Local world-table removal, no wire send (the craft relay carries the fact): table remove + the adapter's ItemDestroyed event.
	internal void RemoveWorldItemLocal(ulong itemId)
	{
		_worldTable.Remove(itemId);
		ItemDestroyed?.Invoke(itemId);
	}

	// Host only: adopt a changed world item's state into the table entry (the craft report's world-Changed evidence — the use path's adopt field set).
	internal void UpdateWorldItemState(ulong itemId, CharacterItemMsg state)
	{
		if (_session.Role == SessionRole.Host && _worldTable.TryGetValue(itemId, out var w))
		{
			w.Item.Condition = state.Condition;
			w.Item.Favourited = state.Favourited;
			w.Item.Liquids = state.Liquids;
			w.Item.Components = state.Components;
		}
	}

	// Publish one carried item's adopted fact (local event + host broadcast — the broadcast self-guards host-only, so one method serves both roles).
	internal void PublishCarriedSyncFor(ulong owner, CharacterItemMsg item) => PublishCarriedSync(owner, item);

	// Local-only carried-fact apply (the craft relay already carries the fact — one operation = one message).
	internal void PublishCarriedSyncLocal(ulong owner, CharacterItemMsg item) => _carriedSync.PublishLocal(owner, item);

	// Role-agnostic local correction apply (the wire entry stays guest-only) — the craft domain's world-Changed entries reach the host's own scene copy through this.
	internal void FireCorrectionLocal(CharacterItemMsg item) => _arbitration.FireCorrectionReceived(item);

	// ===== IItemActionWorldAccess (explicit — the narrow surface the action flows compose) =====

	bool IItemActionWorldAccess.IsWorldItem(ulong itemId) => IsWorldItemRegistered(itemId);

	void IItemActionWorldAccess.UpdateWorldItemState(ulong itemId, CharacterItemMsg state) => UpdateWorldItemState(itemId, state);

	void IItemActionWorldAccess.PublishCarriedSyncFor(ulong owner, CharacterItemMsg item) => PublishCarriedSyncFor(owner, item);

	void IItemActionWorldAccess.FireCorrectionLocal(CharacterItemMsg item) => FireCorrectionLocal(item);
}
