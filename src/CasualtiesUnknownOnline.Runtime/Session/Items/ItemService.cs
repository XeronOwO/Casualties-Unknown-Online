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
public sealed class ItemService(ISessionControl session, PacketSender sender, ILogger<ItemService> log)
	: IItemControl
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<ItemService> _log = log;

	/// <summary>
	/// The authoritative world-item table: instance id → item. Recorded on the
	/// host and in solo play (Role != Guest — a solo-turned-lobby host keeps
	/// its table so a late joiner sees the same world), broadcast only while the
	/// session is active.
	/// </summary>
	private readonly Dictionary<ulong, WorldItem> _worldItems = [];

	/// <summary>The ownership arbitration domain (transfer table + evidence checks) — its state belongs to it, this service forwards the wire reports.</summary>
	private readonly ItemArbitration _arbitration = new(session, sender, log);

	public event Action<WorldItem>? ItemSpawned;

	public event Action<ulong>? ItemPickedUp;

	public event Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2>? ItemDropped;

	public event Action<ulong>? ItemDestroyed;

	public event Action<ulong>? ItemRejected;

	public event Action<IReadOnlyList<WorldItem>>? ItemSnapshotReceived;

	public event Action<IReadOnlyList<ItemMoveEntryMsg>>? ItemMoveReceived;

	public event Action<CharacterItemMsg>? ItemCorrectionReceived
	{
		add => _arbitration.ItemCorrectionReceived += value;
		remove => _arbitration.ItemCorrectionReceived -= value;
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
		foreach (var member in _session.Members)
		{
			if (member.Handshaken && member.SteamId != _session.LocalSteamId)
			{
				_sender.Send(member.SteamId, NetMsg.ItemMove, msg, reliable: false);
			}
		}
	}

	public void FireItemMoveReceived(IReadOnlyList<ItemMoveEntryMsg> items) => ItemMoveReceived?.Invoke(items);

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
	public void SendItemSlot(ulong itemId, int slotIndex)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.ItemSlot, new ItemSlotMsg { ItemId = itemId, SlotIndex = slotIndex });
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
			if (!_worldItems.TryGetValue(itemId, out var entry) || !_worldItems.Remove(itemId))
			{
				// Not in the table: the spawn report is still in flight (the
				// pickup won the race) or a faster writer already took it —
				// refuse; the requester rolls its local pickup back.
				_sender.Send(sender, NetMsg.ItemReject, new ItemRejectMsg
				{
					ItemId = itemId,
					Rejection = ItemRejectMsg.Reason.UnknownItem,
				});
				_log.LogWarning("Item pickup {ItemId} from {Sender} refused — not in the world-item table.", itemId, sender);
				return;
			}

			// Accept-with-correction: the transfer happens from OUR entry (the
			// picker's claim never replaces it), the picker's evidence is only
			// compared afterwards — divergence syncs, never blocks.
			_arbitration.CheckAndTransferToGuest(sender, itemId, entry, evidence);

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

	public void FireItemRejectReceived(ulong sender, ulong itemId)
	{
		_log.LogWarning("Item pickup {ItemId} rejected by the host ({Reason}) — rolling back.", itemId, sender);
		ItemRejected?.Invoke(itemId);
	}

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

		_arbitration.CheckUseEvidence(sender, itemId, evidence);
	}

	public void FireItemSlotReceived(ulong sender, ulong itemId, int slotIndex)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_arbitration.RecordSlot(sender, itemId, slotIndex);
	}

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items)
	{
		_log.LogInformation("World-item snapshot received ({Count} items).", items.Count);
		ItemSnapshotReceived?.Invoke(items);
	}

	// ===== Host-only surface =====

	public void SendItemCorrection(ulong targetSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_arbitration.SendCorrection(targetSteamId, item);
	}

	public IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId) => _arbitration.GetTransferredItems(steamId);

	public void SendItemSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _worldItems.Count == 0)
		{
			return;
		}

		var msg = new ItemSnapshotMsg
		{
			Entries = [.. _worldItems.Values.Select(w => w.ToSnapshotEntryMsg())],
		};
		_sender.Send(targetSteamId, NetMsg.ItemSnapshot, msg);
		_log.LogInformation("Sent world-item snapshot ({Count} items) to {Peer}.", _worldItems.Count, targetSteamId);
	}

	/// <summary>Host only: the item's live state — the periodic keyframe must broadcast the CURRENT positions, not the spawn-time ones (the spawn position would pull settled items back into the air every tick).</summary>
	public void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation)
	{
		if (_session.Role == SessionRole.Guest || !_worldItems.TryGetValue(itemId, out var w))
		{
			return;
		}

		_worldItems[itemId] = w with { Pos = pos, Vel = vel, Rotation = rotation };
	}

	/// <summary>
	/// Host only: periodically re-send the full table over the unreliable
	/// channel — drops are harmless (the next tick overwrites; the receiver
	/// reconciles), and settled items get their drifted positions re-aligned.
	/// </summary>
	public void SendPeriodicItemSnapshot()
	{
		if (_session.Role != SessionRole.Host || _worldItems.Count == 0 || !_session.SessionActive)
		{
			return;
		}

		var msg = new ItemSnapshotMsg
		{
			Entries = [.. _worldItems.Values.Select(w => w.ToSnapshotEntryMsg())],
		};
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.ItemSnapshot, msg, reliable: false);
			}
		}
	}

	public void ResetItems() => _worldItems.Clear();
}
