using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Data-plane sync stream: the 20 Hz state exchange and the join announcements.
/// SessionService (control plane) decides WHEN a member starts syncing and
/// raises <see cref="SessionService.MemberSyncStarted"/>; this class owns the
/// packet assembly, the snapshot sequence numbers and the send throttling.
/// Depends on SessionService + PacketGateway (both direction-safe, acyclic).
/// </summary>
public sealed class EntitySyncStream : ICuoService
{
	private const float StateSendInterval = 0.05f; // 20 Hz authoritative snapshot
	private const float ReportSendInterval = 0.05f; // 20 Hz guest state report

	private readonly SessionService _session;
	private readonly PacketGateway _gateway;
	private readonly SessionIdentity _identity;
	private readonly ILogger<EntitySyncStream> _log;

	private long _nextStateSendMs;
	private long _nextReportSendMs;

	// Snapshot sequence for the unreliable state stream: the sender numbers
	// every broadcast/report, the receiver drops anything at or below the last
	// applied one (the unreliable channel can reorder and duplicate).
	private uint _nextStateSeq; // host: PlayerState broadcasts
	private uint _nextReportSeq; // guest: PlayerStateReport broadcasts

	public EntitySyncStream(
		SessionService session, PacketGateway gateway, SessionIdentity identity, ILogger<EntitySyncStream> log)
	{
		_session = session;
		_gateway = gateway;
		_identity = identity;
		_log = log;
		session.MemberSyncStarted += OnMemberSyncStarted;
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		var nowMs = Environment.TickCount;
		if (_identity.Role == SessionRole.Host && _session.EntitySyncActive && nowMs >= _nextStateSendMs)
		{
			_nextStateSendMs = nowMs + (long)(StateSendInterval * 1000f);
			BroadcastPlayerState();
		}

		if (_identity.Role == SessionRole.Guest && _session.EntitySyncActive && nowMs >= _nextReportSendMs)
		{
			_nextReportSendMs = nowMs + (long)(ReportSendInterval * 1000f);
			SendPlayerStateReport();
		}
	}

	void ICuoService.Stop()
	{
	}

	void ICuoService.Dispose() => _session.MemberSyncStarted -= OnMemberSyncStarted;

	/// <summary>Host side: a member just started syncing — announce it (self-activation
	/// + roster) and push the first snapshot so the clone renders immediately.</summary>
	private void OnMemberSyncStarted(SessionService.MemberState member)
	{
		var joinMsg = new PlayerJoinMsg
		{
			HostSteamId = _session.LocalPlayer.SteamId,
			HostEntityId = NetworkEntityIdMsg.From(_session.LocalPlayer.EntityId),
			GuestSteamId = member.SteamId,
			GuestEntityId = NetworkEntityIdMsg.From(member.Entity.EntityId),
			HostPosition = NetVector2Msg.From(_session.LocalPlayer.Position),
			GuestPosition = NetVector2Msg.From(member.Entity.ReportedSpawnPos),
		};
		_gateway.Send(member.SteamId, NetMsg.PlayerJoin, joinMsg); // self-activation
		BroadcastExcept(member.SteamId, NetMsg.PlayerJoin, joinMsg); // roster: announce to the others
		_log.LogInformation("PlayerJoin sent: local {Local} ({LocalId}), member {Guest} ({GuestId}).",
			_session.LocalPlayer.SteamId, _session.LocalPlayer.EntityId, member.SteamId, member.Entity.EntityId);

		// Immediate full snapshot right after PlayerJoin — the guest's clone
		// renders the very first frame instead of waiting up to one 20 Hz tick
		// for the next broadcast (same mechanism serves respawn/reconnect).
		BroadcastPlayerState();
	}

	/// <summary>Host side: broadcast the authoritative snapshot (local + every synced member) to all synced members.</summary>
	private void BroadcastPlayerState()
	{
		var synced = _session.Members.Where(m => m.EntitySync).ToList();
		if (synced.Count == 0)
		{
			return;
		}

		var payload = new PlayerStateMsg
		{
			Seq = ++_nextStateSeq,
			Entities = BuildEntityList(synced),
		};
		foreach (var member in synced)
		{
			_gateway.Send(member.SteamId, NetMsg.PlayerState, payload, reliable: false);
		}
	}

	private List<EntityStateMsg> BuildEntityList(List<SessionService.MemberState> synced)
	{
		var list = new List<EntityStateMsg>(synced.Count + 1) { EntityStateMsg.From(_session.LocalPlayer) };
		foreach (var member in synced)
		{
			list.Add(EntityStateMsg.From(member.Entity));
		}

		return list;
	}

	/// <summary>Guest side: report the locally simulated state to the host (20 Hz).</summary>
	private void SendPlayerStateReport()
	{
		if (_identity.HostSteamId == 0)
		{
			return;
		}

		_gateway.Send(_identity.HostSteamId, NetMsg.PlayerStateReport,
			new PlayerStateReportMsg
			{
				Seq = ++_nextReportSeq,
				Entity = EntityStateMsg.From(_session.LocalPlayer),
			}, reliable: false);
	}

	private void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload)
	{
		foreach (var member in _session.Members)
		{
			if (member.SteamId != excludeSteamId)
			{
				_gateway.Send(member.SteamId, msg, payload);
			}
		}
	}
}
