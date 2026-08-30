using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
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

	private readonly ISteamService _steam;
	private readonly PacketSender _sender;
	private readonly NetworkTrafficMonitor _traffic;
	private readonly ITimeSource _time;
	private readonly ILogger<SessionService> _log;
	private readonly SessionPeerMaintenance _peerMaintenance;
	private readonly HostKickService _hostKick;

	// Session-owned state — never registered as services (user rule: state
	// belongs to the object that owns it; consumers get ISessionControl).
	private readonly SessionIdentity _identity = new();
	private readonly SessionState _state = new();
	private readonly MemberPresenceTable _presence = new();

	/// <summary>The lobby this client currently hosts or joined (0 = none) — tracked here so a real lobby change is distinguishable from a duplicate Steam callback.</summary>
	private ulong _currentLobbyId;

	private long _nextPingMs;

	public SessionService(ISteamService steam, PacketSender sender, NetworkTrafficMonitor traffic, ITimeSource time,
		IModListProvider modListProvider, ILogger<SessionService> log)
	{
		_steam = steam;
		_sender = sender;
		_traffic = traffic;
		_time = time;
		_log = log;
		_peerMaintenance = new SessionPeerMaintenance(
			steam, sender, time, modListProvider, _identity, _state, _presence,
			new PeerWarmupBackoff(), RemoveMember, EndSession, log);
		_hostKick = new HostKickService(this, _sender, _log);

		steam.LobbyCreated += OnLobbyCreated;
		steam.LobbyEntered += OnLobbyEntered;
		steam.LobbyLeft += OnLobbyLeft;
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

	/// <summary>Our own SteamId (read-only query surface, like Role/HostSteamId).</summary>
	public ulong LocalSteamId => _steam.LocalSteamId;

	/// <summary>The member presence table (read-only; the table itself is owned internally).</summary>
	public System.Collections.Generic.IEnumerable<MemberPresenceTable.MemberPresence> Members => _presence.Members;

	SceneStateType ISessionControl.LocalSceneState => _state.LocalInWorld ? SceneStateType.InWorld : SceneStateType.InMenu;

	bool ISessionControl.TryGetMember(ulong steamId, out MemberPresenceTable.MemberPresence member) =>
		_presence.TryGetMember(steamId, out member);

	MemberPresenceTable.MemberPresence ISessionControl.GetOrCreateMember(ulong steamId) =>
		_presence.GetOrCreateMember(steamId);

	bool ISessionControl.IsLobbyMember(ulong steamId) => _steam.GetLobbyMembers().Contains(steamId);

	void ISessionControl.Broadcast(NetMsg msg, object payload) =>
		_sender.SendToAll(_presence.Members.Select(m => m.SteamId), msg, payload);

	void ISessionControl.BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload) =>
		_sender.SendToAll(_presence.Members.Select(m => m.SteamId), msg, payload, excludeSteamId: excludeSteamId);

	void ISessionControl.RemoveGuestMember(ulong steamId)
	{
		_presence.Remove(steamId);
		_presence.FireMemberRemoved(steamId);
	}

	void ISessionControl.RecordPong(ulong sender, long ticks)
	{
		LastRttMs = (_time.UtcNowTicks - ticks) / 10_000f;
		_traffic.RecordPong(sender, LastRttMs, ticks);
		if (_presence.TryGetMember(sender, out var member))
		{
			member.RttMs = LastRttMs;
		}
	}

	void ISessionControl.FireSessionActivated() => _state.FireSessionActivated();

	void ISessionControl.FireRemoteSceneChanged(ulong steamId, bool inWorld) =>
		_presence.FireRemoteSceneChanged(steamId, inWorld);

	void ISessionControl.FireMemberAdded(ulong steamId) => _presence.FireMemberAdded(steamId);

	event Action<ulong>? ISessionControl.MemberRemoved
	{
		add => _presence.MemberRemoved += value;
		remove => _presence.MemberRemoved -= value;
	}

	event Action<ulong>? ISessionControl.MemberAdded
	{
		add => _presence.MemberAdded += value;
		remove => _presence.MemberAdded -= value;
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
		var msg = PingMsg.At(_time.UtcNowTicks);
		if (Role == SessionRole.Host)
		{
			foreach (var member in _presence.Members)
			{
				_traffic.RecordPingSent(member.SteamId, msg.Ticks, _time.NowMs);
				_sender.Send(member.SteamId, NetMsg.Ping, msg);
			}
		}
		else
		{
			_traffic.RecordPingSent(HostSteamId, msg.Ticks, _time.NowMs);
			_sender.Send(HostSteamId, NetMsg.Ping, msg);
		}
	}

	void ICuoService.Initialize() => _identity.LocalSteamId = _steam.LocalSteamId;

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		// Host: keep warming up lobby peers that have not handshaken yet — a
		// Steam P2P session only establishes with traffic from both directions
		// (Phase-0 finding), and the periodic ping only covers presence members.
		// Without this a late joiner never gets host→guest traffic and its
		// handshake dies (5003 connect timeout).
		_peerMaintenance.SendPeerWarmup();
		if (!SessionActive)
		{
			_peerMaintenance.RetryHandshakeIfNeeded();
			_peerMaintenance.CheckPeerPresence();
			return;
		}

		var nowMs = _time.NowMs;
		if (nowMs >= _nextPingMs)
		{
			_nextPingMs = nowMs + (long)(PingInterval * 1000f);
			RequestPing();
		}

		// The entity-sync decisions and the 20 Hz stream run in
		// EntitySyncService.Update; the receive dispatch in PacketDispatcher
		// (both registered after us).
		_peerMaintenance.CheckPeerPresence();
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
		_steam.LobbyCreated -= OnLobbyCreated;
		_steam.LobbyEntered -= OnLobbyEntered;
		_steam.LobbyLeft -= OnLobbyLeft;
	}

	// ---- Lobby / handshake ----

	private void OnLobbyCreated(ulong lobbyId)
	{
		if (IsCurrentHost(lobbyId))
		{
			return; // duplicate create callback — the first one already armed the session
		}

		if (_currentLobbyId != lobbyId)
		{
			TeardownSession(leaveLobby: true);
		}

		_currentLobbyId = lobbyId;
		_identity.Role = SessionRole.Host;
		_identity.HostSteamId = _steam.LocalSteamId;
		// The host is authoritative from the moment the lobby exists — even
		// before any guest handshakes. SessionActive gates world-gen isolation
		// (IsWorldGenIsolated) and the world/entity domains: without it a host
		// generating alone would run UNisolated generation (public-stream
		// pollution), and a guest joining later would generate from the captured
		// state and get a different world.
		SessionActive = true;
		_log.LogInformation("Session role: Host (lobby {LobbyId})", lobbyId);
	}

	private void OnLobbyLeft(ulong lobbyId)
	{
		_log.LogInformation("Left lobby {LobbyId} — lobby identity ends with it.", lobbyId);
		TeardownSession(leaveLobby: true);
		// Role follows the ACTUAL lobby state: with no lobby there is no
		// identity. EndSession (same-lobby outage/rejoin) still keeps Role.
		_identity.Role = SessionRole.None;
	}

	private void OnLobbyEntered(ulong lobbyId)
	{
		var owner = _steam.GetLobbyOwner();
		if (owner == 0)
		{
			_log.LogWarning("Entered lobby {LobbyId} but Steam reported no owner — session not started.", lobbyId);
			return;
		}

		if (owner == _steam.LocalSteamId)
		{
			// Our own lobby. The normal create flow already ran OnLobbyCreated
			// and LobbyEnter_t follows it — a no-op. If the create callback was
			// missed (or a duplicate arrives after EndSession), re-arm host
			// identity by owner, never by the previous Role guess.
			if (IsCurrentHost(lobbyId))
			{
				return;
			}

			if (_currentLobbyId != lobbyId)
			{
				TeardownSession(leaveLobby: true);
			}

			_currentLobbyId = lobbyId;
			_identity.Role = SessionRole.Host;
			_identity.HostSteamId = owner;
			SessionActive = true;
			_log.LogInformation("Session role: Host (own lobby {LobbyId})", lobbyId);
			return;
		}

		// The host is the lobby owner, not "first member other than me" — with
		// 3+ members that guess picks the wrong peer and the handshake dies.
		var sameSession = _currentLobbyId == lobbyId
			&& Role == SessionRole.Guest
			&& HostSteamId == owner
			&& SessionActive;
		if (!sameSession)
		{
			if (_currentLobbyId != lobbyId)
			{
				TeardownSession(leaveLobby: true);
			}

			_currentLobbyId = lobbyId;
			_identity.Role = SessionRole.Guest;
			_identity.HostSteamId = owner;
			_log.LogInformation("Session role: Guest (lobby {LobbyId}, host {Host})", lobbyId, owner);
		}

		_peerMaintenance.KickHandshake();
	}

	private bool IsCurrentHost(ulong lobbyId) =>
		_currentLobbyId == lobbyId
		&& _identity.Role == SessionRole.Host
		&& _identity.HostSteamId == _steam.LocalSteamId
		&& SessionActive;

	/// <summary>
	/// Host side: kick a guest out of the session. Sends the dedicated
	/// <see cref="NetMsg.Kicked"/> to the target first (so the guest tears down
	/// instead of waiting for the host to disappear), then removes it from the
	/// presence table — the existing member-removal path broadcasts PlayerLeave
	/// to the remaining members and cleans up entity clones.
	/// </summary>
	public bool KickMember(ulong steamId, string reason) => _hostKick.Kick(steamId, reason);

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

		TeardownSession(leaveLobby: false);
	}

	/// <summary>
	/// Tear the session content down (presence, active flag, host id) and fire
	/// the teardown events exactly once when content existed. The Role is NOT
	/// reset here: <see cref="EndSession"/> models a same-lobby outage (a
	/// returning host rebuilds the session), while a real lobby change goes
	/// through <see cref="OnLobbyLeft"/>, which additionally drops the Role to
	/// None before the new lobby assigns the next identity.
	/// </summary>
	private void TeardownSession(bool leaveLobby)
	{
		var hadSession = SessionActive || _presence.Count > 0 || _identity.HostSteamId != 0;

		// Stop every send FIRST — the teardown events below run game code
		// (ToMainMenu / clone destruction) that must not report into a dead
		// session and must not race a fresh session's first frames.
		SessionActive = false;

		// Fire each member's in-world=false edge BEFORE clearing the table:
		// the run coordinator uses HostSteamId to pull a guest out of a world
		// whose host is gone, and the renderer destroys that member's clone.
		foreach (var member in _presence.Members.ToList())
		{
			_presence.FireRemoteSceneChanged(member.SteamId, false);
		}

		_presence.Clear();
		_identity.HostSteamId = 0;
		_peerMaintenance.ResetHandshakeRetry();
		if (leaveLobby)
		{
			_currentLobbyId = 0;
			_peerMaintenance.ResetWarmup(); // the failure history belongs to the old lobby — the next lobby starts clean
		}

		if (hadSession)
		{
			// Role is intentionally still the OLD role here: the entity/enemy
			// domains branch on it to tear their side down correctly.
			_log.LogInformation("Session ended (role {Role} kept).", Role);
			_state.FireSessionEnded(); // the entity domain + the Game Adapter tear down on this
		}

		_traffic.Reset(); // per-session traffic and peer-health diagnostics are not reused across lobbies
	}

	void ISessionControl.EndSession() => EndSession();
}
