using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The roster announcement writes the entity domain sends: a member's activation
/// join, the backfill of every other synced member to a newcomer, the announce of
/// a join to the others, and the leave row. Extracted from
/// <see cref="EntitySyncService"/> at the architecture gate (the file sat at 598
/// lines when the sync-coverage R3 row added the roster to the in-session repair
/// group): the service keeps identity and lifecycle — who is synced, with which
/// entity id — this collaborator owns the messages that announce it.
///
/// The same writes serve the first sync AND the repair group's absolute re-send:
/// the receiver absorbs a repeat by the identity the row carries (SteamId +
/// EntityId), which is what lets a swallowed <c>PlayerJoin</c> converge without a
/// leave/re-enter — row R3 of the sync-coverage audit, whose roster half had no
/// re-send at all.
/// </summary>
internal sealed class PlayerRosterAnnouncer(PacketSender sender, ISessionControl session, ILogger log)
{
	private readonly PacketSender _sender = sender;
	private readonly ISessionControl _session = session;
	private readonly ILogger _log = log;

	/// <summary>
	/// Host side: send the member its own activation join (or its absolute repeat)
	/// followed by every OTHER synced member's roster row — a newcomer needs them,
	/// because their joins predate it and state messages only update existing
	/// buffers. Returns the activation message so the caller can announce the same
	/// row to the others.
	/// </summary>
	internal PlayerJoinMsg SendRosterTo(ulong steamId, NetworkEntityId entityId, NetVector2 position, PlayerEntity localPlayer, IEnumerable<EntitySyncService.SyncedEntity> synced)
	{
		var joinMsg = BuildJoinMsg(steamId, entityId, position, localPlayer);
		_sender.Send(steamId, NetMsg.PlayerJoin, joinMsg); // self-activation

		var backfilled = 0;
		foreach (var other in synced)
		{
			if (other.SteamId == steamId)
			{
				continue;
			}

			var presence = _session.GetOrCreateMember(other.SteamId);
			_sender.Send(steamId, NetMsg.PlayerJoin,
				BuildJoinMsg(other.SteamId, other.Entity.EntityId, presence.ReportedSpawnPos, localPlayer));
			backfilled++;
		}

		_log.LogInformation("Roster sent to {Member} (activation + {Backfill} backfilled row(s)).", steamId, backfilled);
		return joinMsg;
	}

	/// <summary>Host side: announce one member's join to every other synced member.</summary>
	internal void AnnounceJoin(PlayerJoinMsg joinMsg, ulong excludeSteamId, IEnumerable<EntitySyncService.SyncedEntity> synced) =>
		BroadcastExcept(excludeSteamId, NetMsg.PlayerJoin, joinMsg, synced);

	/// <summary>Host side: a member left the session — the remaining guests drop its row by id.</summary>
	internal void AnnounceLeave(ulong steamId, NetworkEntityId entityId, IEnumerable<EntitySyncService.SyncedEntity> synced) =>
		BroadcastExcept(steamId, NetMsg.PlayerLeave, new PlayerLeaveMsg
		{
			SteamId = steamId,
			EntityId = entityId.ToNetworkEntityIdMsg(),
		}, synced);

	/// <summary>Broadcast to every synced member except one — relay semantics: the source already applied the change locally.</summary>
	private void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload, IEnumerable<EntitySyncService.SyncedEntity> synced)
	{
		foreach (var member in synced)
		{
			if (member.SteamId != excludeSteamId)
			{
				_sender.Send(member.SteamId, msg, payload);
			}
		}
	}

	private PlayerJoinMsg BuildJoinMsg(ulong guestSteamId, NetworkEntityId guestId, NetVector2 guestPosition, PlayerEntity localPlayer)
	{
		var displayName = _session.TryGetMember(guestSteamId, out var presence)
			? presence.DisplayName
			: "";
		var selectedColor = _session.TryGetMember(guestSteamId, out presence)
			? presence.SelectedColor
			: null;
		return new PlayerJoinMsg
		{
			HostSteamId = localPlayer.SteamId,
			HostEntityId = localPlayer.EntityId.ToNetworkEntityIdMsg(),
			HostPosition = localPlayer.Position.ToNetVector2Msg(),
			GuestSteamId = guestSteamId,
			GuestEntityId = guestId.ToNetworkEntityIdMsg(),
			GuestPosition = guestPosition.ToNetVector2Msg(),
			DisplayName = displayName,
			HasColor = selectedColor.HasValue,
			Color = selectedColor.HasValue ? selectedColor.Value.ToNetColorRgbaMsg() : new(),
		};
	}
}
