using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session state machine (control plane): lobby → handshake (protocol/version)
/// → scene-state exchange (architecture.md §3). Owns the handshake lifecycle,
/// the business-level send/receive APIs and the world/diagnostics surface.
/// Its state is private: the lobby identity, the session flags and the member
/// presence table are created here and exposed as a read-only surface —
/// consumers depend on <see cref="ISessionControl"/>, not on the state
/// objects (user rule: state belongs to the object that owns it). The data
/// plane (PacketReceiver/PacketSender) and the dispatch (PacketDispatcher)
/// are independent mechanisms; the entity/data domains hang off the session
/// one-way. Star topology: every message flows guest → host; the host
/// arbitrates and decides the fan-out.
/// </summary>
public sealed class SessionService : ICuoService, ISessionControl
{
	private const float PingInterval = 5f;
	private const float MemberCheckInterval = 2f;
	private const float HandshakeRetryInterval = 3f; // lazy Steam P2P sessions swallow early messages

	private readonly SteamService _steam;
	private readonly PacketSender _sender;
	private readonly ILogger<SessionService> _log;

	// Session-owned state — never registered as services (user rule: state
	// belongs to the object that owns it; consumers get ISessionControl).
	private readonly SessionIdentity _identity = new();
	private readonly SessionState _state = new();
	private readonly MemberPresenceTable _presence = new();

	private long _nextPingMs;
	private long _nextMemberCheckMs;
	private long _nextHandshakeRetryMs;

	public SessionService(SteamService steam, PacketSender sender, ILogger<SessionService> log)
	{
		_steam = steam;
		_sender = sender;
		_log = log;

		steam.LobbyCreated += OnLobbyCreated;
		steam.LobbyEntered += OnLobbyEntered;
	}

	public SessionRole Role => _identity.Role;

	/// <summary>True once the handshake completed (protocol versions agreed). Set by the handshake handlers.</summary>
	public bool SessionActive { get => _state.SessionActive; set => _state.SessionActive = value; }

	public ulong HostSteamId => _identity.HostSteamId;

	public float LastRttMs { get; private set; } = -1f;

	/// <summary>Local scene state — true while the local player is in the world (the SceneState we last reported).</summary>
	public bool LocalInWorld => _state.LocalInWorld;

	/// <summary>Remote member scene state — the Game Adapter's render loop uses it to clone only in-world members.</summary>
	public bool IsRemoteInWorld(ulong steamId) => _presence.TryGetMember(steamId, out var member) && member.InWorld;

	/// <summary>The spawn position a member reported when entering the world — the host's clone anchor.</summary>
	public NetVector2 GetRemoteSpawnPos(ulong steamId) =>
		_presence.TryGetMember(steamId, out var member) ? member.ReportedSpawnPos : default;

	/// <summary>Raised when the handshake completes and scene exchange can start (first member only).</summary>
	public event Action? SessionActivated
	{
		add => _state.SessionActivated += value;
		remove => _state.SessionActivated -= value;
	}

	/// <summary>Raised when the session ends (all members gone, lobby left, …).</summary>
	public event Action? SessionEnded
	{
		add => _state.SessionEnded += value;
		remove => _state.SessionEnded -= value;
	}

	/// <summary>
	/// Raised when a member enters or leaves the world (inWorld=false pauses /
	/// destroys the render clone; a member leaving the session reuses
	/// inWorld=false so the clone teardown path is shared).
	/// </summary>
	public event Action<ulong, bool>? RemoteSceneChanged
	{
		add => _presence.RemoteSceneChanged += value;
		remove => _presence.RemoteSceneChanged -= value;
	}

	// ---- ISessionControl (the packet handlers' + domains' control surface) ----

	ulong ISessionControl.LocalSteamId => _steam.LocalSteamId;

	SceneStateType ISessionControl.LocalSceneState => _state.LocalInWorld ? SceneStateType.InWorld : SceneStateType.InMenu;

	System.Collections.Generic.IEnumerable<MemberPresenceTable.MemberPresence> ISessionControl.Members => _presence.Members;

	bool ISessionControl.TryGetMember(ulong steamId, out MemberPresenceTable.MemberPresence member) =>
		_presence.TryGetMember(steamId, out member);

	MemberPresenceTable.MemberPresence ISessionControl.GetOrCreateMember(ulong steamId) =>
		_presence.GetOrCreateMember(steamId);

	bool ISessionControl.IsLobbyMember(ulong steamId) => _steam.GetLobbyMembers().Contains(steamId);

	void ISessionControl.Broadcast(NetMsg msg, object payload)
	{
		foreach (var member in _presence.Members)
		{
			_sender.Send(member.SteamId, msg, payload);
		}
	}

	void ISessionControl.BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload)
	{
		foreach (var member in _presence.Members)
		{
			if (member.SteamId != excludeSteamId)
			{
				_sender.Send(member.SteamId, msg, payload);
			}
		}
	}

	void ISessionControl.RemoveGuestMember(ulong steamId)
	{
		_presence.Remove(steamId);
		_presence.FireMemberRemoved(steamId);
	}

	void ISessionControl.RecordPong(ulong sender, long ticks)
	{
		LastRttMs = (DateTime.UtcNow.Ticks - ticks) / 10_000f;
		if (_presence.TryGetMember(sender, out var member))
		{
			member.RttMs = LastRttMs;
		}
	}

	void ISessionControl.FireSessionActivated() => _state.FireSessionActivated();

	void ISessionControl.FireRemoteSceneChanged(ulong steamId, bool inWorld) =>
		_presence.FireRemoteSceneChanged(steamId, inWorld);

	event Action<ulong>? ISessionControl.MemberRemoved
	{
		add => _presence.MemberRemoved += value;
		remove => _presence.MemberRemoved -= value;
	}

	event Action? ISessionControl.SessionEnded
	{
		add => _state.SessionEnded += value;
		remove => _state.SessionEnded -= value;
	}

	// ---- Scene / diagnostics (Game Adapter → session) ----

	/// <summary>
	/// Either side: report the local scene state (menu / in world). The local
	/// position is attached when entering the world — the host spawns the
	/// guest's clone at the guest's actual spawn point, so both sides simulate
	/// from the same start and validation corrections stay small.
	/// Guest: the report goes to the host (host tracks the member's scene and
	/// relays it to the other guests). Host: broadcast to all synced members.
	/// </summary>
	public void ReportSceneState(SceneStateType state, string sceneName, NetVector2? localPosition = null)
	{
		_state.LocalInWorld = state == SceneStateType.InWorld;
		if (SessionActive)
		{
			var msg = new SceneStateMsg
			{
				State = (byte)state,
				SceneName = sceneName,
				Position = (localPosition ?? default).ToNetVector2Msg(),
				SteamId = _steam.LocalSteamId,
			};
			if (Role == SessionRole.Host)
			{
				((ISessionControl)this).Broadcast(NetMsg.SceneState, msg);
			}
			else
			{
				_sender.Send(HostSteamId, NetMsg.SceneState, msg);
			}
		}

		_log.LogInformation("Scene state: {State} ({SceneName})", state, sceneName);
	}

	/// <summary>
	/// Diagnostics: ping the peer(s) (RTT recorded in <see cref="LastRttMs"/>;
	/// host pings every member, guest pings the host).
	/// </summary>
	public void RequestPing()
	{
		var msg = PingMsg.Now;
		if (Role == SessionRole.Host)
		{
			foreach (var member in _presence.Members)
			{
				_sender.Send(member.SteamId, NetMsg.Ping, msg);
			}
		}
		else
		{
			_sender.Send(HostSteamId, NetMsg.Ping, msg);
		}
	}

	void ICuoService.Initialize() => _identity.LocalSteamId = _steam.LocalSteamId;

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		if (!SessionActive)
		{
			RetryHandshakeIfNeeded();
			SendPreSessionKeepalive();
			CheckPeerPresence();
			return;
		}

		var nowMs = Environment.TickCount;
		if (nowMs >= _nextPingMs)
		{
			_nextPingMs = nowMs + (long)(PingInterval * 1000f);
			RequestPing();
		}

		// The entity-sync decisions and the 20 Hz stream run in
		// EntitySyncService.Update; the receive dispatch in PacketDispatcher
		// (both registered after us).
		CheckPeerPresence();
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
		_steam.LobbyCreated -= OnLobbyCreated;
		_steam.LobbyEntered -= OnLobbyEntered;
	}

	// ---- Lobby / handshake ----

	private void OnLobbyCreated(ulong lobbyId)
	{
		_identity.Role = SessionRole.Host;
		_identity.HostSteamId = _steam.LocalSteamId;
		_log.LogInformation("Session role: Host (lobby {LobbyId})", lobbyId);
	}

	private void OnLobbyEntered(ulong lobbyId)
	{
		if (_identity.Role == SessionRole.Host)
		{
			return; // our own lobby — the create callback already ran
		}

		_identity.Role = SessionRole.Guest;
		// The host is the lobby owner, not "first member other than me" — with
		// 3+ members that guess picks the wrong peer and the handshake dies.
		_identity.HostSteamId = _steam.GetLobbyOwner();
		_log.LogInformation("Session role: Guest (lobby {LobbyId}, host {Host})", lobbyId, _identity.HostSteamId);

		// Kick off the handshake: protocol version + our scene state. Retry
		// periodically until acked (Steam P2P sessions establish lazily and
		// swallow the first messages — retransmission also drives the session).
		_nextHandshakeRetryMs = Environment.TickCount + (long)(HandshakeRetryInterval * 1000f);
		_sender.Send(HostSteamId, NetMsg.Handshake, CreateHandshakeMsg());
	}

	private void RetryHandshakeIfNeeded()
	{
		if (Role != SessionRole.Guest || HostSteamId == 0)
		{
			return;
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextHandshakeRetryMs)
		{
			return;
		}

		_nextHandshakeRetryMs = nowMs + (long)(HandshakeRetryInterval * 1000f);
		_sender.Send(HostSteamId, NetMsg.Handshake, CreateHandshakeMsg());
		_log.LogInformation("Retrying handshake with {Host}…", HostSteamId);
	}

	/// <summary>
	/// Host side, pre-session: keep pinging the lobby peer. The Steam P2P
	/// session only establishes with traffic from both directions (Phase-0
	/// finding — the old auto-ping kept it alive); with the guest retrying the
	/// handshake alone the messages never arrive.
	/// </summary>
	private void SendPreSessionKeepalive()
	{
		if (Role != SessionRole.Host)
		{
			return;
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextHandshakeRetryMs)
		{
			return;
		}

		_nextHandshakeRetryMs = nowMs + (long)(HandshakeRetryInterval * 1000f);
		var ping = PingMsg.Now;
		foreach (var peer in _steam.GetLobbyMembers())
		{
			if (peer != _steam.LocalSteamId)
			{
				_sender.Send(peer, NetMsg.Ping, ping);
			}
		}
	}

	// ---- Message handlers moved to Session/Handlers/ (HandshakeHandlers, SceneStateHandler, …) ----

	// ---- Peer presence ----

	private void CheckPeerPresence()
	{
		if (Role == SessionRole.None || !SessionActive)
		{
			return;
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextMemberCheckMs)
		{
			return;
		}

		_nextMemberCheckMs = nowMs + (long)(MemberCheckInterval * 1000f);

		var lobbyMembers = _steam.GetLobbyMembers();
		if (Role == SessionRole.Host)
		{
			// Remove members that vanished from the lobby (each member is
			// tracked individually — a 3-person lobby losing one guest keeps
			// the other). End the session once the last member is gone; the
			// host stays in the lobby, ready for new joins. Reconnects are
			// cheap: Role stays (lobby identity), the character save is kept
			// per SteamID, and the next handshake rebuilds the member.
			foreach (var memberId in _presence.Members.Select(m => m.SteamId).ToList())
			{
				if (!lobbyMembers.Contains(memberId))
				{
					RemoveMember(memberId, "left the lobby");
				}
			}

			if (_presence.Count == 0)
			{
				_log.LogWarning("All members left the lobby — ending session (save kept).");
				EndSession();
			}
		}
		else if (!lobbyMembers.Contains(HostSteamId))
		{
			// The host is gone — no host migration in the MVP.
			_log.LogWarning("Host left the lobby — ending session (save kept).");
			EndSession();
		}
	}

	/// <summary>Host side: drop a member (presence removal; the entity domain
	/// broadcasts the roster PlayerLeave and tears the clone down).</summary>
	internal void RemoveMember(ulong steamId, string reason)
	{
		if (!_presence.TryGetMember(steamId, out _))
		{
			return;
		}

		_presence.Remove(steamId);
		_log.LogInformation("Member {Member} removed: {Reason}.", steamId, reason);
		_presence.FireMemberRemoved(steamId);
	}

	internal void EndSession()
	{
		if (!SessionActive && _presence.Count == 0)
		{
			return;
		}

		_presence.Clear();
		SessionActive = false;
		_identity.HostSteamId = 0;
		// Role is NOT reset here: it follows the lobby identity (the lobby
		// creator stays Host, a joiner stays Guest) — the session content is
		// gone, but a returning guest's handshake is still accepted and rebuilds
		// everything (new member + character save restore).
		_log.LogInformation("Session ended (role {Role} kept).", Role);
		_state.FireSessionEnded(); // the entity domain + the Game Adapter tear down on this
	}

	private HandshakeMsg CreateHandshakeMsg() => new()
	{
		Protocol = ProtocolVersion.Current,
		Scene = new SceneStateMsg { State = (byte)(_state.LocalInWorld ? SceneStateType.InWorld : SceneStateType.InMenu) },
	};
	void ISessionControl.EndSession() => EndSession();
}
