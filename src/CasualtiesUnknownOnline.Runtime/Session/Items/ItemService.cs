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

	public event Action<WorldItem>? ItemSpawned;

	public event Action<ulong>? ItemPickedUp;

	public event Action<ulong, CharacterItemMsg, NetVector2, ulong, float>? ItemDropped;

	public event Action<ulong>? ItemDestroyed;

	public event Action<ulong>? ItemRejected;

	public event Action<IReadOnlyList<WorldItem>>? ItemSnapshotReceived;

	// ===== Report side (local compute) =====

	public void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems[itemId] = new WorldItem(itemId, item, pos, vel, 0, rotation);
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

	public void SendItemPickedUp(ulong itemId)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems.Remove(itemId); // the picker took it — it is inventory data now
		}

		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new ItemPickupMsg { ItemId = itemId };
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.ItemPickup, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.ItemPickup, msg);
		}
	}

	public void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, ulong parentItemId, float rotation)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_worldItems[itemId] = new WorldItem(itemId, item, pos, NetVector2.Zero, parentItemId, rotation);
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
			ParentItemId = parentItemId,
			Rotation = rotation,
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

	public void FireItemSpawnedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation)
	{
		if (_session.Role == SessionRole.Host)
		{
			if (!_worldItems.ContainsKey(itemId))
			{
				_worldItems[itemId] = new WorldItem(itemId, item, pos, vel, 0, rotation);
				_session.BroadcastExcept(sender, NetMsg.ItemSpawn, new ItemSpawnMsg
				{
					ItemId = itemId,
					Item = item,
					Position = pos.ToNetVector2Msg(),
					Velocity = vel.ToNetVector2Msg(),
					Rotation = rotation,
				});
				_log.LogInformation("Item {ItemId} ({Type}) spawned by {Sender} — registered + relayed.", itemId, item.ItemId, sender);
			}
			// Duplicate report (reliable retransmit): already registered — drop silently (idempotent).
		}

		// Host materializes the guest's item; guest materializes the host's relay.
		ItemSpawned?.Invoke(new WorldItem(itemId, item, pos, vel, 0, rotation));
	}

	public void FireItemPickedUpReceived(ulong sender, ulong itemId)
	{
		if (_session.Role == SessionRole.Host)
		{
			if (!_worldItems.Remove(itemId))
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

			_session.BroadcastExcept(sender, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId });
			_log.LogInformation("Item {ItemId} picked up by {Sender} — relayed.", itemId, sender);
		}

		// The winner's local removal; on the losing guests this event rolls
		// their optimistic pickup back (the adapter decides by local state).
		ItemPickedUp?.Invoke(itemId);
	}

	public void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, ulong parentItemId, float rotation)
	{
		if (_session.Role == SessionRole.Host)
		{
			_worldItems[itemId] = new WorldItem(itemId, item, pos, NetVector2.Zero, parentItemId, rotation);
			_session.BroadcastExcept(sender, NetMsg.ItemDrop, new ItemDropMsg
			{
				ItemId = itemId,
				Item = item,
				Position = pos.ToNetVector2Msg(),
				ParentItemId = parentItemId,
				Rotation = rotation,
			});
		}

		ItemDropped?.Invoke(itemId, item, pos, parentItemId, rotation);
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

	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items)
	{
		_log.LogInformation("World-item snapshot received ({Count} items).", items.Count);
		ItemSnapshotReceived?.Invoke(items);
	}

	// ===== Host-only surface =====

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

	public void ResetItems() => _worldItems.Clear();
}
