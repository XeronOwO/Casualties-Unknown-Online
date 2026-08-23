using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Host-side pending-pickup arbitration. A pickup that arrives before its
/// spawn/drop registration waits in a short hold window instead of being
/// refused immediately; a registration that confirms the item settles the first
/// queued claim (first-writer-wins), and the per-frame expiry edge rejects only
/// claims that never resolved.
/// </summary>
internal sealed class ItemPendingPickupArbiter(
	ISessionControl session,
	PacketSender sender,
	ITimeSource time,
	WorldItemTable worldTable,
	ItemArbitration arbitration,
	ItemCarriedSyncService carriedSync,
	Action<ItemTrafficKind, string> recordTraffic,
	Action<WorldItem> onItemSpawned,
	Action<ulong> onItemPickedUp,
	Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> onItemDropped,
	ILogger<ItemService> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ITimeSource _time = time;
	private readonly WorldItemTable _worldTable = worldTable;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly ItemCarriedSyncService _carriedSync = carriedSync;
	private readonly PendingPickupQueue _pendingPickups = new(PendingPickupQueue.DefaultHoldMs);
	private readonly Action<ItemTrafficKind, string> _recordTraffic = recordTraffic;
	private readonly Action<WorldItem> _onItemSpawned = onItemSpawned;
	private readonly Action<ulong> _onItemPickedUp = onItemPickedUp;
	private readonly Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2> _onItemDropped = onItemDropped;
	private readonly ILogger<ItemService> _log = log;

	public void HandleHostPickupReport(ulong sender, ulong itemId, CharacterItemMsg? evidence)
	{
		if (_worldTable.TryGetValue(itemId, out var entry))
		{
			CompleteAcceptedPickup(sender, itemId, entry, evidence);
			return;
		}

		if (_arbitration.IsContainedInEntry(itemId, _worldTable.Items) || _arbitration.IsTransferredToGuest(sender, itemId))
		{
			return;
		}

		if (_arbitration.IsTransferredToAnyGuest(itemId))
		{
			SendUnknownItemReject(sender, itemId, "another guest's pickup already transferred it");
			return;
		}

		if (_pendingPickups.TryEnqueue(sender, itemId, evidence, _time.NowMs))
		{
			_log.LogInformation("Item pickup {ItemId} from {Sender} queued — its registration has not arrived yet (hold {HoldMs} ms).",
				itemId, sender, PendingPickupQueue.DefaultHoldMs);
			return;
		}

		_log.LogWarning("Item pickup {ItemId} from {Sender} already queued — duplicate claim dropped silently.", itemId, sender);
	}

	public void HandleHostSpawnReport(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity)
	{
		var pendingWinner = _pendingPickups.TryTakeFirst(itemId);
		var worldItem = new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity);
		if (!_worldTable.ContainsKey(itemId))
		{
			_worldTable.Set(itemId, worldItem);

			var msg = new ItemSpawnMsg
			{
				ItemId = itemId,
				Item = item,
				Position = pos.ToNetVector2Msg(),
				Velocity = vel.ToNetVector2Msg(),
				Rotation = rotation,
				FreshItemDrop = freshItemDrop,
			};
			if (pendingWinner is null)
			{
				_session.BroadcastExcept(sender, NetMsg.ItemSpawn, msg);
				_log.LogInformation("Item {ItemId} ({Type}) spawned by {Sender} — registered + relayed.", itemId, item.ItemId, sender);
			}
			else
			{
				_sender.SendToAll(MembersExcept(sender, pendingWinner.Sender), NetMsg.ItemSpawn, msg);
				_log.LogInformation("Item {ItemId} ({Type}) spawned by {Sender} — registered; queued pickup from {Picker} is being settled.",
					itemId, item.ItemId, sender, pendingWinner.Sender);
			}

			_recordTraffic(ItemTrafficKind.Spawn, item.ItemId);
		}

		_onItemSpawned(worldItem);
		ResolveContainedPendingPickups();
		SettlePendingWinner(itemId, pendingWinner);
	}

	public void HandleHostDropReport(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos)
	{
		_arbitration.CheckAndUnloadFromGuest(sender, itemId, item);

		var pendingWinner = _pendingPickups.TryTakeFirst(itemId);

		var isDuplicate = _worldTable.TryGetValue(itemId, out var existing)
			&& existing.Pos.X == pos.X && existing.Pos.Y == pos.Y && existing.Rotation == rotation;
		var registered = new WorldItem(itemId, item, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity);
		_worldTable.Set(itemId, registered);
		if (!isDuplicate)
		{
			var msg = new ItemDropMsg
			{
				ItemId = itemId,
				Item = item,
				Position = pos.ToNetVector2Msg(),
				Velocity = vel.ToNetVector2Msg(),
				ParentItemId = parentItemId,
				Rotation = rotation,
				ParentPosition = parentPos.ToNetVector2Msg(),
				AngularVelocity = angularVelocity,
			};
			if (pendingWinner is null)
			{
				_session.BroadcastExcept(sender, NetMsg.ItemDrop, msg);
			}
			else
			{
				_sender.SendToAll(MembersExcept(sender, pendingWinner.Sender), NetMsg.ItemDrop, msg);
			}

			_recordTraffic(ItemTrafficKind.Drop, item.ItemId);
		}

		_onItemDropped(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
		ResolveContainedPendingPickups();
		SettlePendingWinner(itemId, pendingWinner);
	}

	private void CompleteAcceptedPickup(ulong sender, ulong itemId, WorldItem entry, CharacterItemMsg? evidence)
	{
		_worldTable.Remove(itemId);

		var authoritative = _arbitration.CheckAndTransferToGuest(sender, itemId, entry, evidence);
		_carriedSync.Publish(sender, authoritative);

		_session.BroadcastExcept(sender, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId });
		_recordTraffic(ItemTrafficKind.Pickup, entry.Item.ItemId);
		_log.LogInformation("Item {ItemId} picked up by {Sender} — transferred + relayed.", itemId, sender);

		_onItemPickedUp(itemId);
	}

	public void PumpPendingPickups(long nowMs)
	{
		foreach (var pending in _pendingPickups.TakeExpired(nowMs))
		{
			if (_worldTable.TryGetValue(pending.ItemId, out var entry))
			{
				_log.LogInformation("Queued item pickup {ItemId} from {Sender} — item registered after the queue was filled, settling now.", pending.ItemId, pending.Sender);
				CompleteAcceptedPickup(pending.Sender, pending.ItemId, entry, pending.Evidence);
				continue;
			}

			if (_arbitration.IsContainedInEntry(pending.ItemId, _worldTable.Items) || _arbitration.IsTransferredToGuest(pending.Sender, pending.ItemId))
			{
				_log.LogInformation("Queued item pickup {ItemId} from {Sender} resolved silently on expiry (container content / already transferred).", pending.ItemId, pending.Sender);
				continue;
			}

			SendUnknownItemReject(pending.Sender, pending.ItemId,
				$"the registration did not arrive within the {PendingPickupQueue.DefaultHoldMs} ms hold");
		}
	}

	private void ResolveContainedPendingPickups()
	{
		foreach (var pending in _pendingPickups.TakeWhere(p => _arbitration.IsContainedInEntry(p.ItemId, _worldTable.Items)))
		{
			_log.LogInformation("Queued item pickup {ItemId} from {Sender} is a container content after the registration — accepted silently.", pending.ItemId, pending.Sender);
		}
	}

	private void SettlePendingWinner(ulong itemId, PendingPickupQueue.PendingPickup? pendingWinner)
	{
		if (pendingWinner is null)
		{
			return;
		}

		if (_worldTable.TryGetValue(itemId, out var entry))
		{
			CompleteAcceptedPickup(pendingWinner.Sender, itemId, entry, pendingWinner.Evidence);
		}
		else
		{
			_log.LogWarning("Queued item pickup {ItemId} from {Sender} could not settle — the registered entry is gone.", itemId, pendingWinner.Sender);
		}

		foreach (var loser in _pendingPickups.TakeByItem(itemId))
		{
			SendUnknownItemReject(loser.Sender, itemId, "an earlier queued claim settled first");
		}
	}

	private void SendUnknownItemReject(ulong sender, ulong itemId, string reason)
	{
		_sender.Send(sender, NetMsg.ItemReject, new ItemRejectMsg
		{
			ItemId = itemId,
			Rejection = ItemRejectMsg.Reason.UnknownItem,
		});
		_log.LogWarning("Item pickup {ItemId} from {Sender} rejected — {Reason}.", itemId, sender, reason);
	}

	private IEnumerable<ulong> MembersExcept(params ulong[] excluded)
	{
		var excludes = new HashSet<ulong>(excluded);
		return _session.Members.Select(m => m.SteamId).Where(id => !excludes.Contains(id));
	}

	public void Reset() => _pendingPickups.Reset();
}
