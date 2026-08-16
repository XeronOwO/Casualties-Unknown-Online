using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Host-side pending-pickup integration for <see cref="ItemService"/>. A pickup
/// that arrives before its spawn/drop registration is no longer refused
/// immediately (the old UnknownItem reject rolled the picker's local pickup
/// back and then left the late spawn in the world for a manual re-pickup);
/// instead the claim waits in <see cref="PendingPickupQueue"/> for a short
/// hold window. A registration that confirms the item settles the first
/// queued claim (first-writer-wins — later claims are rejected), a
/// registration that makes the claim a container content resolves it
/// silently (the container transfer carries it), and the per-frame
/// <see cref="PendingPickupPump"/> rejects only claims that never resolved.
/// </summary>
public sealed partial class ItemService
{
	private readonly PendingPickupQueue _pendingPickups;
	private readonly ITimeSource _time;

	/// <summary>A pickup report on the host: transfer when the item is known, queue when its registration is still in flight, stay silent for the idempotency/container family.</summary>
	private void HandleHostPickupReport(ulong sender, ulong itemId, CharacterItemMsg? evidence)
	{
		if (_worldTable.TryGetValue(itemId, out var entry))
		{
			CompleteAcceptedPickup(sender, itemId, entry, evidence);
			return;
		}

		// An id that travels INSIDE a container entry is not unknown: the
		// container's own transfer carries it (refusing yanked each content back
		// out of the picker's bag). The picker's OWN duplicate report is a
		// retransmission of a completed transfer — a rejection would roll the
		// winner's own successful pickup back.
		if (_arbitration.IsContainedInEntry(itemId, _worldTable.Items) || _arbitration.IsTransferredToGuest(sender, itemId))
		{
			return;
		}

		// A faster writer already owns the item — the first-writer-wins conflict
		// is obvious and the item will not register for this claim; reject now
		// (the pending queue is only for a registration still in flight).
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

	/// <summary>A spawn report on the host: register + relay, then settle any queued pickup the registration confirms.</summary>
	private void HandleHostSpawnReport(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity)
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
				// The queued winner already has the item locally — do not send
				// it a second world copy. Everyone else still needs the spawn
				// fact before the pickup broadcast (same reliable order as the
				// non-raced spawn-then-pickup path).
				_sender.SendToAll(MembersExcept(sender, pendingWinner.Sender), NetMsg.ItemSpawn, msg);
				_log.LogInformation("Item {ItemId} ({Type}) spawned by {Sender} — registered; queued pickup from {Picker} is being settled.",
					itemId, item.ItemId, sender, pendingWinner.Sender);
			}
		}
		// Duplicate report (reliable retransmit): already registered — the
		// registration is idempotent; a queued claim can still settle below.

		// The host's scene applies the spawn first, then the settled pickup
		// removes it — the same event order as the non-raced path.
		ItemSpawned?.Invoke(worldItem);
		ResolveContainedPendingPickups();
		SettlePendingWinner(itemId, pendingWinner);
	}

	/// <summary>A drop report on the host: the carried item becomes a world item again; any queued pickup that this registration confirms settles right away.</summary>
	private void HandleHostDropReport(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos)
	{
		// The drop leaves the transfer table — the carried item is now a world
		// item. The full item IS the evidence (materialization payload, so the
		// host already has everything to compare) — checked against the entry
		// BEFORE it leaves, the divergence is synced with the drop itself.
		_arbitration.CheckAndUnloadFromGuest(sender, itemId, item);

		var pendingWinner = _pendingPickups.TryTakeFirst(itemId);

		// Idempotent: a retransmitted report (Steam reliable resend) must not
		// re-broadcast — the receivers would materialize AND re-place the same
		// item. The queued winner already has its local copy, so a real drop
		// relay also excludes it (spawn/drop fact then pickup fact for everyone
		// else, no second copy on the winner).
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
		}

		ItemDropped?.Invoke(itemId, item, pos, vel, parentItemId, rotation, angularVelocity, parentPos);
		ResolveContainedPendingPickups();
		SettlePendingWinner(itemId, pendingWinner);
	}

	/// <summary>The host-side accepted-pickup completion: remove the authoritative world entry, transfer it to the picker, broadcast the carried fact + the pickup and fire the local removal event.</summary>
	private void CompleteAcceptedPickup(ulong sender, ulong itemId, WorldItem entry, CharacterItemMsg? evidence)
	{
		_worldTable.Remove(itemId);

		// Accept-with-correction: the transfer happens from OUR entry (the
		// picker's claim never replaces it), the picker's evidence is only
		// compared afterwards — divergence syncs, never blocks. The adopted
		// entry then broadcasts as the carried-fact event (the peers' clones
		// of the picker show the item the moment it lands in its slot).
		var authoritative = _arbitration.CheckAndTransferToGuest(sender, itemId, entry, evidence);
		PublishCarriedSync(sender, authoritative);

		_session.BroadcastExcept(sender, NetMsg.ItemPickup, new ItemPickupMsg { ItemId = itemId });
		_log.LogInformation("Item {ItemId} picked up by {Sender} — transferred + relayed.", itemId, sender);

		// The winner's local removal; on the losing guests this event rolls
		// their optimistic pickup back (the adapter decides by local state).
		ItemPickedUp?.Invoke(itemId);
	}

	/// <summary>The per-frame expiry edge: a claim that never resolved gets the late UnknownItem reject — or, if the item registered through a path that does not settle queues, the normal transfer.</summary>
	internal void PumpPendingPickups(long nowMs)
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

	/// <summary>Every pending claim whose item is now a content of a registered container resolves silently — the container's own transfer carries it.</summary>
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

		// Later claims for the same item lose the settled race (first-writer-wins).
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
}
