using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session peer maintenance: warm-up pings, guest handshake retry, member
/// presence reconciliation and the handshake message. Split out of
/// <see cref="SessionService"/> when the session state machine reached the
/// architecture line gate; this class owns the periodic peer-facing timers and
/// the read-only state objects are passed in by the service that still owns the
/// lobby/session lifecycle.
/// </summary>
internal sealed class SessionPeerMaintenance(
	ISteamService steam,
	PacketSender sender,
	ITimeSource time,
	IModListProvider modListProvider,
	SessionIdentity identity,
	SessionState state,
	MemberPresenceTable presence,
	PeerWarmupBackoff warmupBackoff,
	Action<ulong, string> removeMember,
	Action endSession,
	ILogger log)
{
	private const float MemberCheckInterval = 2f;
	private const float HandshakeRetryInterval = 1f;

	private readonly ISteamService _steam = steam;
	private readonly PacketSender _sender = sender;
	private readonly ITimeSource _time = time;
	private readonly IModListProvider _modListProvider = modListProvider;
	private readonly SessionIdentity _identity = identity;
	private readonly SessionState _state = state;
	private readonly MemberPresenceTable _presence = presence;
	private readonly PeerWarmupBackoff _warmupBackoff = warmupBackoff;
	private readonly Action<ulong, string> _removeMember = removeMember;
	private readonly Action _endSession = endSession;
	private readonly ILogger _log = log;
	private long _nextMemberCheckMs;
	private long _nextHandshakeRetryMs;

	internal void KickHandshake()
	{
		// Kick off the handshake: protocol version + our scene state. Retry
		// periodically until acked (Steam P2P sessions establish lazily and
		// swallow the first messages — retransmission also drives the session).
		_nextHandshakeRetryMs = _time.NowMs + (long)(HandshakeRetryInterval * 1000f);
		_sender.Send(_identity.HostSteamId, NetMsg.Handshake, CreateHandshakeMsg());
	}

	internal void RetryHandshakeIfNeeded()
	{
		if (_identity.Role != SessionRole.Guest || _identity.HostSteamId == 0)
		{
			return;
		}

		var nowMs = _time.NowMs;
		if (nowMs < _nextHandshakeRetryMs)
		{
			return;
		}

		_nextHandshakeRetryMs = nowMs + (long)(HandshakeRetryInterval * 1000f);
		_sender.Send(_identity.HostSteamId, NetMsg.Handshake, CreateHandshakeMsg());
		_log.LogInformation("Retrying handshake with {Host}…", _identity.HostSteamId);
	}

	/// <summary>Host-side warm-up for un-handshaken lobby peers (Steam P2P needs traffic both ways).</summary>
	internal void SendPeerWarmup()
	{
		if (_identity.Role != SessionRole.Host)
		{
			return;
		}

		var nowMs = _time.NowMs;
		if (nowMs < _nextHandshakeRetryMs)
		{
			return;
		}

		_nextHandshakeRetryMs = nowMs + (long)(HandshakeRetryInterval * 1000f);
		var ping = PingMsg.At(_time.UtcNowTicks);
		foreach (var peer in _steam.GetLobbyMembers())
		{
			if (peer == _steam.LocalSteamId)
			{
				continue;
			}

			// Established members are kept alive by the periodic ping — warming
			// up only the un-handshaken ones keeps the join window covered.
			if (_presence.TryGetMember(peer, out var member) && member.Handshaken)
			{
				continue;
			}

			if (!_warmupBackoff.ShouldSend(peer, nowMs))
			{
				continue; // a recent failure streak is still backing off — do not hammer the broken session
			}

			if (_sender.TrySend(peer, NetMsg.Ping, ping))
			{
				_warmupBackoff.RecordSuccess(peer);
			}
			else
			{
				_warmupBackoff.RecordFailure(peer, nowMs);
			}
		}
	}

	internal void CheckPeerPresence()
	{
		if (_identity.Role == SessionRole.None || !_state.SessionActive)
		{
			return;
		}

		var nowMs = _time.NowMs;
		if (nowMs < _nextMemberCheckMs)
		{
			return;
		}

		_nextMemberCheckMs = nowMs + (long)(MemberCheckInterval * 1000f);

		var lobbyMembers = _steam.GetLobbyMembers();
		if (_identity.Role == SessionRole.Host)
		{
			// Remove members that vanished from the lobby (each member is
			// tracked individually — a 3-person lobby losing one guest keeps
			// the other). The session itself CONTINUES — the host may be
			// playing alone, and the next guest handshakes into the SAME
			// session (ending it here would be irreversible: SessionActive
			// only re-arms on OnLobbyCreated, so a guest leaving would kill
			// the lobby the host still holds — observed: the guest quit, the
			// host's session ended, and the rejoining guest could never
			// handshake back in). Only the host's own absence ends the
			// session (the guest branch below).
			foreach (var memberId in _presence.Members.Select(m => m.SteamId).ToList())
			{
				if (!lobbyMembers.Contains(memberId))
				{
					_removeMember(memberId, "left the lobby");
				}
			}
		}
		else if (!lobbyMembers.Contains(_identity.HostSteamId))
		{
			// The host is gone — no host migration in the MVP.
			_log.LogWarning("Host left the lobby — ending session (save kept).");
			_endSession();
		}
	}

	internal void ResetHandshakeRetry() => _nextHandshakeRetryMs = 0;

	internal void ResetWarmup() => _warmupBackoff.Reset();

	internal void ResetForSessionEnd()
	{
		ResetHandshakeRetry();
		ResetWarmup();
	}

	private HandshakeMsg CreateHandshakeMsg()
	{
		var color = _steam.LocalPlayerColor;
		return new HandshakeMsg
		{
			Protocol = ProtocolVersion.Current,
			Scene = new SceneStateMsg { State = (byte)(_state.LocalInWorld ? SceneStateType.InWorld : SceneStateType.InMenu) },
			// The declared mod list (Phase 4 Mod API consistency check — the host
			// validates it before admitting the guest). Empty while the first-frame
			// discovery has not run yet: a guest joining during its own Awake sends
			// its first handshake with an empty list, the 1 s retry carries the
			// real one (and a host without requirements accepts the empty list).
			Mods = _modListProvider.CurrentModInfos(),
			// The local display name: Steam persona in Steam mode, the configured
			// custom name in IP-direct mode (the IP adapter answers this query).
			DisplayName = _steam.GetPersonaName(_steam.LocalSteamId),
			HasColor = color.HasValue,
			Color = color.HasValue ? color.Value.ToNetColorRgba().ToNetColorRgbaMsg() : new(),
		};
	}
}
