using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The world-item domain: runtime-generated items in the world (drops, loot,
/// placed items) with the host (and solo play — late-joiner parity) keeping
/// the authoritative table. Local compute → report up / register → relay down,
/// the star-network pattern: the spawner applies locally, the host arbitrates
/// (pickups are first-writer-wins against the table) and relays to the other
/// members. Generation-time items never enter the table — world-gen
/// determinism covers them. No pump: it only reacts to calls and messages (not
/// an ICuoService, like WorldService).
/// </summary>
public sealed class ItemService : IItemControl
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ILogger<ItemService> _log;

	/// <summary>
	/// The authoritative world-item table: instance id → item. Recorded on the
	/// host and in solo play (Role != Guest — a solo-turned-lobby host keeps
	/// its table so a late joiner sees the same world), broadcast only while the
	/// session is active.
	/// </summary>
	private readonly Dictionary<ulong, WorldItem> _worldItems = [];

	private readonly ItemArbitration _arbitration;
	private readonly ItemCarriedSyncService _carriedSync;
	private readonly ItemSnapshotService _snapshots;

	public ItemService(ISessionControl session, PacketSender sender, ILogger<ItemService> log)
	{
		_session = session;
		_sender = sender;
		_log = log;
		_arbitration = new(session, sender, log);
		_carriedSync = new(session, sender, log);
		_snapshots = new(session, sender, () => _worldItems.Values, log);
	}

	public event Action<WorldItem>? ItemSpawned;

	public event Action<ulong>? ItemPickedUp;

	public event Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2>? ItemDropped;

	public event Action<ulong>? ItemDestroyed;

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

	// ===== Host-authoritative position stream =====

	/// <summary>
	/// Host only: broadcast EVERY world item's authoritative position
	/// (unreliable — drops are harmless, the next tick overwrites). The host's
	/// physics is the single position authority; the guests' copies are
	/// kinematic renders that follow this stream — nothing on their side
	/// simulates, so nothing diverges.
	/// </summary>
	public void SendItemMove(IReadOnlyList<ItemMoveEntryMsg> items)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || items.Count == 0)
		{
			return;
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

	// ===== Report side (local compute) =====

	public void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems[itemId] = new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity);
		}

		if (!_session.SessionActive)
		{
			return;
		}

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

	public void SendItemPickedUp(ulong itemId, CharacterItemMsg? evidence = null)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems.Remove(itemId); // the picker took it — it is inventory data now
		}

		if (!_session.SessionActive)
		{
			return;
		}

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
	public void SendItemUse(ulong itemId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.ItemUse, new ItemUseMsg { ItemId = itemId, Item = item });
	}

	/// <summary>Guest only: an item moved slots locally — report the new slot so the host's record stays in sync. Host-side moves are the host's own authority, never reported.</summary>
	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.ItemSlot, new ItemSlotMsg { ItemId = itemId, SlotIndex = slotIndex, Item = item });
	}

	public void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems[itemId] = new WorldItem(itemId, item, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity);
		}

		if (!_session.SessionActive)
		{
			return;
		}

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
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems.Remove(itemId);
		}

		if (!_session.SessionActive)
		{
			return;
		}

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
			if (!_worldItems.ContainsKey(itemId))
			{
				_worldItems[itemId] = new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity);
				_session.BroadcastExcept(sender, NetMsg.ItemSpawn, new ItemSpawnMsg
				{
					ItemId = itemId,
					Item = item,
					Position = pos.ToNetVector2Msg(),
					Velocity = vel.ToNetVector2Msg(),
					Rotation = rotation,
					FreshItemDrop = freshItemDrop,
				});
				_log.LogInformation("Item {ItemId} ({Type}) spawned by {Sender} — registered + relayed.", itemId, item.ItemId, sender);
			}
			// Duplicate report (reliable retransmit): already registered — drop silently (idempotent).
		}

		// Host materializes the guest's item; guest materializes the host's relay.
		ItemSpawned?.Invoke(new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity));
	}

	public void FireItemPickedUpReceived(ulong sender, ulong itemId, CharacterItemMsg? evidence)
	{
		if (_session.Role == SessionRole.Host)
		{
			if (!_worldItems.TryGetValue(itemId, out var entry))
			{
				// Not in the table: the spawn report is still in flight (the
				// pickup won the race) or a faster writer already took it —
				// refuse; the requester rolls its local pickup back. EXCEPT:
				// an id that travels INSIDE a container entry (a bag's picked-up
				// contents are reported separately, PickupSync, but have no
				// independent world-table entry) is not unknown — accept
				// silently, the container's own transfer carries it (refusing
				// yanked each content back out of the picker's bag — "picked up
				// a bag with contents, it came back empty").
				if (!_arbitration.IsContainedInEntry(itemId, _worldItems))
				{
					_sender.Send(sender, NetMsg.ItemReject, new ItemRejectMsg
					{
						ItemId = itemId,
						Rejection = ItemRejectMsg.Reason.UnknownItem,
					});
					_log.LogWarning("Item pickup {ItemId} from {Sender} refused — not in the world-item table.", itemId, sender);
				}

				return;
			}

			_worldItems.Remove(itemId);

			// Accept-with-correction: the transfer happens from OUR entry (the
			// picker's claim never replaces it), the picker's evidence is only
			// compared afterwards — divergence syncs, never blocks. The adopted
			// entry then broadcasts as the carried-fact event (the peers' clones
			// of the picker show the item the moment it lands in its slot).
			var authoritative = _arbitration.CheckAndTransferToGuest(sender, itemId, entry, evidence);
			PublishCarriedSync(sender, authoritative);

			_session.BroadcastExcept(sender, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId });
			_log.LogInformation("Item {ItemId} picked up by {Sender} — transferred + relayed.", itemId, sender);
		}

		// The winner's local removal; on the losing guests this event rolls
		// their optimistic pickup back (the adapter decides by local state).
		ItemPickedUp?.Invoke(itemId);
	}

	public void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos = default)
	{
		if (_session.Role == SessionRole.Host)
		{
			// The drop leaves the transfer table — the carried item is now a
			// world item. The full item IS the evidence (materialization
			// payload, so the host already has everything to compare) —
			// checked against the entry BEFORE it leaves, the divergence is
			// synced with the drop itself.
			_arbitration.CheckAndUnloadFromGuest(sender, itemId, item);

			// Idempotent: a retransmitted report (Steam reliable resend) must not
			// re-broadcast — the receivers would materialize AND re-place the
			// same item (observed: "not present — materializing" followed by
			// "present — re-placing" for one drop).
			var isDuplicate = _worldItems.TryGetValue(itemId, out var existing)
				&& existing.Pos.X == pos.X && existing.Pos.Y == pos.Y && existing.Rotation == rotation;
			_worldItems[itemId] = new WorldItem(itemId, item, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity);
			if (!isDuplicate)
			{
				_session.BroadcastExcept(sender, NetMsg.ItemDrop, new ItemDropMsg
				{
					ItemId = itemId,
					Item = item,
					Position = pos.ToNetVector2Msg(),
					Velocity = vel.ToNetVector2Msg(),
					ParentItemId = parentItemId,
					Rotation = rotation,
					ParentPosition = parentPos.ToNetVector2Msg(),
					AngularVelocity = angularVelocity,
				});
			}
		}

		ItemDropped?.Invoke(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
	}

	public void FireItemDestroyedReceived(ulong sender, ulong itemId)
	{
		if (_session.Role == SessionRole.Host)
		{
			_worldItems.Remove(itemId);
			_session.BroadcastExcept(sender, NetMsg.ItemDestroy, new ItemDestroyMsg { ItemId = itemId });
		}

		ItemDestroyed?.Invoke(itemId);
	}

	public void FireItemRejectReceived(ulong sender, ulong itemId, ItemRejectMsg.Reason reason)
	{
		_log.LogWarning("Item {ItemId} rejected by the host ({Reason}) — rolling back.", itemId, reason);
		ItemRejected?.Invoke(itemId, reason);
	}

	// ===== Block-break drops (one message, one verdict — the break's drops ride BlockDamagedMsg) =====

	/// <summary>
	/// Host/solo: record the drops of a LOCALLY broken block into the
	/// authoritative table (the wire report itself goes through
	/// WorldService.SendBlockDamaged — the drops travel with the break, not as
	/// standalone spawn reports). The local drop objects already exist — this
	/// only registers, never materializes. Guests have no table and never call.
	/// </summary>
	public void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops)
	{
		if (_session.Role == SessionRole.Guest || drops.Count == 0)
		{
			return;
		}

		foreach (var drop in drops)
		{
			if (!_worldItems.ContainsKey(drop.ItemId))
			{
				_worldItems[drop.ItemId] = ToWorldItem(drop);
			}
		}
	}

	/// <summary>
	/// A break with drops was APPLIED (host: the report's break was accepted —
	/// the sender's BlockPlaced already broke the block on this side; guest: the
	/// host's accepted relay arrived): register (host only) and materialize
	/// every drop. The breaker itself is excluded from the relay and never
	/// arrives here — its local drops are the original, already on the ground.
	/// </summary>
	public void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops)
	{
		if (drops.Count == 0)
		{
			return;
		}

		foreach (var drop in drops)
		{
			if (_session.Role == SessionRole.Host && !_worldItems.ContainsKey(drop.ItemId))
			{
				_worldItems[drop.ItemId] = ToWorldItem(drop);
			}

			ItemSpawned?.Invoke(ToWorldItem(drop));
		}
	}

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

	private static WorldItem ToWorldItem(BlockDropEntryMsg drop) => new(
		drop.ItemId, drop.Item, drop.Position.ToNetVector2(), drop.Velocity.ToNetVector2(),
		0, drop.Rotation, drop.FreshItemDrop, AngularVelocity: drop.AngularVelocity);

	public void FireItemCorrectionReceived(ulong sender, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return; // host-side corrections make no sense — the host is the source
		}

		_arbitration.FireCorrectionReceived(item);
	}

	public void FireItemUseReceived(ulong sender, ulong itemId, CharacterItemMsg evidence)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// The adopted state broadcasts as the carried-fact event (the peers'
		// clones of the user show the flipped state — a flashlight mode — the
		// moment the use lands). A starting-supply item has no transfer-table
		// entry (it never passed a pickup) — the guest's own report IS the fact
		// then (the same unconditional-adoption logic), broadcast as-is.
		var authoritative = _arbitration.CheckUseEvidence(sender, itemId, evidence) ?? evidence;
		PublishCarriedSync(sender, authoritative);
	}

	public void FireItemSlotReceived(ulong sender, ulong itemId, int slotIndex, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// The recorded slot broadcasts as the carried-fact event (the peers'
		// clones of the mover re-home the item the moment the move lands). A
		// starting-supply item has no transfer-table entry (it never passed a
		// pickup) — the report's digest evidence is the fact then, broadcast
		// as-is (its slot is the new one, SlotKnown).
		var authoritative = _arbitration.RecordSlot(sender, itemId, slotIndex) ?? item;
		PublishCarriedSync(sender, authoritative);
	}

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireItemSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);

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

	public IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId) => _arbitration.GetTransferredItems(steamId);

	public void SendItemSnapshot(ulong targetSteamId) => _snapshots.SendItemSnapshot(targetSteamId);

	/// <summary>Host only: the item's live state — the periodic keyframe must broadcast the CURRENT positions and condition, not the spawn-time ones (the spawn position would pull settled items back into the air every tick; a stale condition would re-align the peers' decay to the wrong value).</summary>
	public void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation, float condition)
	{
		if (_session.Role == SessionRole.Guest || !_worldItems.TryGetValue(itemId, out var w))
		{
			return;
		}

		w.Item.Condition = condition;
		_worldItems[itemId] = w with { Pos = pos, Vel = vel, Rotation = rotation };
	}

	/// <summary>Host only: periodically re-send the full table (unreliable) — see ItemSnapshotService.</summary>
	public void SendPeriodicItemSnapshot() => _snapshots.SendPeriodicItemSnapshot();

	public void ResetItems() => _worldItems.Clear();

	/// <summary>
	/// Host only: the generation finished — the host assigned an id to every
	/// generation-time item (ground items + the starting supplies) and hands the
	/// full set over. Registered silently into the table (no ItemSpawned event —
	/// the local copies already exist; only the guests need to bind or
	/// materialize) and broadcast as ONE reliable snapshot — the guests bind
	/// their local copies to the host's ids or materialize the host's version.
	/// After this the items are ordinary table entries: the position stream, the
	/// periodic keyframe, the pickup arbitration and the late-joiner snapshot
	/// all cover them.
	/// </summary>
	public void PublishGeneratedItems(IReadOnlyList<ItemSnapshotEntryMsg> entries)
	{
		if (_session.Role == SessionRole.Guest || entries.Count == 0)
		{
			return;
		}

		var registered = 0;
		foreach (var entry in entries)
		{
			// A carried entry (starting supplies, SlotIndex >= 0) has NO table
			// entry — it lives in a backpack until a drop brings it into the
			// world (the drop report registers it then, the standard path).
			if (entry.SlotIndex >= 0 || _worldItems.ContainsKey(entry.ItemId))
			{
				continue;
			}

			_worldItems[entry.ItemId] = new WorldItem(entry.ItemId, entry.Item,
				entry.Position.ToNetVector2(), entry.Velocity.ToNetVector2(),
				entry.ParentItemId, entry.Rotation, entry.FreshItemDrop);
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
			entries.Count(e => e.SlotIndex < 0), entries.Count(e => e.SlotIndex >= 0), LayerModifierIndex);
	}

	public void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> _snapshots.FireWorldItemsSnapshotReceived(sender, items, layerModifierIndex, layerModifierRandomState);
}
