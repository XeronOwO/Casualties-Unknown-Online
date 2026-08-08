using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session state machine (control plane): lobby → handshake (protocol/version)
/// → scene-state exchange (architecture.md §3). Owns the member presence table
/// (who is in the session, in which scene) and the business-level send/receive
/// APIs; the data plane (transport binding, direction validation, dispatch to
/// the packet handlers) lives in <see cref="PacketGateway"/>. Entity buffers,
/// ids and the 20 Hz state stream live in <see cref="EntitySyncService"/>; the
/// character save/restore in <see cref="CharacterDataStore"/>. Star topology:
/// every message flows guest → host; the host arbitrates and decides the fan-out.
/// </summary>
public sealed class SessionService : ICuoService
{
	private const float PingInterval = 5f;
	private const float MemberCheckInterval = 2f;
	private const float HandshakeRetryInterval = 3f; // lazy Steam P2P sessions swallow early messages

	/// <summary>
	/// One remote peer's session presence. Host: one entry per guest. Guest: one
	/// for the host plus roster entries for the other guests. Key = SteamId
	/// (stable across reconnects). Scene state (InWorld/ReportedSpawnPos) is
	/// session-scoped; the entity side (buffers, ids, sync state) is tracked by
	/// <see cref="EntitySyncService"/>.
	/// </summary>
	internal sealed class MemberPresence
	{
		public ulong SteamId;
		public bool Handshaken; // protocol handshake completed
		public bool InWorld; // in the world (menu/loading = false)
		public NetVector2 ReportedSpawnPos; // position reported when entering the world — the clone anchor
		public float RttMs = -1f; // per-member ping diagnostics
	}

	private readonly SteamService _steam;
	private readonly SessionIdentity _identity;
	private readonly PacketGateway _gateway;
	private readonly ILogger<SessionService> _log;

	private readonly Dictionary<ulong, MemberPresence> _members = [];
	private WorldStartParams? _worldParams;
	private bool _localInWorld; // local scene state — the SceneState we last reported

	private long _nextPingMs;
	private long _nextMemberCheckMs;
	private long _nextHandshakeRetryMs;

	public SessionService(SteamService steam, SessionIdentity identity, PacketGateway gateway, ILogger<SessionService> log)
	{
		_steam = steam;
		_identity = identity;
		_gateway = gateway;
		_log = log;

		steam.LobbyCreated += OnLobbyCreated;
		steam.LobbyEntered += OnLobbyEntered;
	}

	public SessionRole Role => _identity.Role;

	/// <summary>True once the handshake completed (protocol versions agreed). Set by the handshake handlers.</summary>
	public bool SessionActive { get; internal set; }

	public ulong HostSteamId => _identity.HostSteamId;

	/// <summary>Set by the world-params handler on the guest side.</summary>
	public WorldStartParams? WorldParams { get; internal set; }

	public float LastRttMs { get; private set; } = -1f;

	/// <summary>Local scene state — true while the local player is in the world (the SceneState we last reported).</summary>
	public bool LocalInWorld => _localInWorld;

	/// <summary>Remote member scene state — the Game Adapter's render loop uses it to clone only in-world members.</summary>
	public bool IsRemoteInWorld(ulong steamId) => _members.TryGetValue(steamId, out var member) && member.InWorld;

	/// <summary>The spawn position a member reported when entering the world — the host's clone anchor.</summary>
	public NetVector2 GetRemoteSpawnPos(ulong steamId) =>
		_members.TryGetValue(steamId, out var member) ? member.ReportedSpawnPos : default;

	// ---- Internal surface for the packet handlers (Session/Handlers/) ----

	internal ulong LocalSteamId => _steam.LocalSteamId;

	internal IEnumerable<MemberPresence> Members => _members.Values;

	internal bool TryGetMember(ulong steamId, out MemberPresence member) =>
		_members.TryGetValue(steamId, out member!);

	internal bool IsLobbyMember(ulong steamId) => _steam.GetLobbyMembers().Contains(steamId);

	/// <summary>Raised when the handshake completes and scene exchange can start (first member only).</summary>
	public event Action? SessionActivated;

	/// <summary>Raised when the session ends (all members gone, lobby left, …).</summary>
	public event Action? SessionEnded;

	/// <summary>
	/// Raised when a member enters or leaves the world (inWorld=false pauses /
	/// destroys the render clone; a member leaving the session reuses
	/// inWorld=false so the clone teardown path is shared). The SteamId routes
	/// the event to the right clone.
	/// </summary>
	public event Action<ulong, bool>? RemoteSceneChanged;

	/// <summary>Raised when a member is removed from the presence table (left the
	/// lobby, PlayerLeave, …). The entity domain subscribes to drop the member's
	/// entity and announce the leave.</summary>
	internal event Action<ulong>? MemberRemoved;

	// ---- Event fires for the packet handlers (the events stay public — the
	// Game Adapter subscribes from another assembly; handlers fire through these). ----

	internal void FireSessionActivated() => SessionActivated?.Invoke();

	internal void FireRemoteSceneChanged(ulong steamId, bool inWorld) => RemoteSceneChanged?.Invoke(steamId, inWorld);

	internal void FireBlockDamagedReceived(NetVector2 pos, float damage) => BlockDamagedReceived?.Invoke(pos, damage);

	internal void FireMemberRemoved(ulong steamId) => MemberRemoved?.Invoke(steamId);

	// ---- Scene / world / diagnostics (Game Adapter → session) ----

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
		_localInWorld = state == SceneStateType.InWorld;
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
				Broadcast(NetMsg.SceneState, msg);
			}
			else
			{
				Send(HostSteamId, NetMsg.SceneState, msg);
			}
		}

		_log.LogInformation("Scene state: {State} ({SceneName})", state, sceneName);
	}

	/// <summary>Host side: capture and publish world-start parameters (run start).</summary>
	public void PublishWorldParams(WorldStartParams parameters)
	{
		_worldParams = parameters;
		if (!SessionActive)
		{
			return;
		}

		var msg = parameters.ToWorldStartParamsMsg();
		foreach (var member in _members.Values.Where(m => m.Handshaken))
		{
			Send(member.SteamId, NetMsg.WorldStartParams, msg);
		}

		_log.LogInformation("Published world params ({StateBytes} bytes) to {Members} members.",
			parameters.RandomState.Length, _members.Count);
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
			foreach (var member in _members.Values)
			{
				Send(member.SteamId, NetMsg.Ping, msg);
			}
		}
		else
		{
			Send(HostSteamId, NetMsg.Ping, msg);
		}
	}

	/// <summary>
	/// Report a locally-performed block damage (local compute): guest → host as
	/// a report (the host arbitrates and relays), host → broadcast to all synced
	/// members (the source excluded on relay — it already applied locally).
	/// </summary>
	public void SendBlockDamaged(NetVector2 worldPos, float damage)
	{
		if (!SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
		};
		if (Role == SessionRole.Host)
		{
			Broadcast(NetMsg.BlockDamaged, msg);
		}
		else
		{
			Send(HostSteamId, NetMsg.BlockDamaged, msg);
		}
	}

	/// <summary>Host: a guest reported damage (apply + relay). Guest: the host broadcast it.</summary>
	public event Action<NetVector2, float>? BlockDamagedReceived;

	void ICuoService.Initialize()
	{
	}

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
		// EntitySyncService.Update (registered after us).
		CheckPeerPresence();
	}

	void ICuoService.Stop()
	{
	}

	void ICuoService.Dispose()
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
		_identity.HostSteamId = _steam.GetLobbyMembers().FirstOrDefault(m => m != _steam.LocalSteamId);
		_log.LogInformation("Session role: Guest (lobby {LobbyId}, host {Host})", lobbyId, _identity.HostSteamId);

		// Kick off the handshake: protocol version + our scene state. Retry
		// periodically until acked (Steam P2P sessions establish lazily and
		// swallow the first messages — retransmission also drives the session).
		_nextHandshakeRetryMs = Environment.TickCount + (long)(HandshakeRetryInterval * 1000f);
		Send(HostSteamId, NetMsg.Handshake, CreateHandshakeMsg());
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
		Send(HostSteamId, NetMsg.Handshake, CreateHandshakeMsg());
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
				Send(peer, NetMsg.Ping, ping);
			}
		}
	}

	// ---- Message handlers moved to Session/Handlers/ (HandshakeHandlers, SceneStateHandler, …) ----

	// ---- Ping / pong (diagnostics) ----

	/// <summary>Records the round-trip for the pong sender (per member).</summary>
	internal void RecordPong(ulong sender, long ticks)
	{
		LastRttMs = (DateTime.UtcNow.Ticks - ticks) / 10_000f;
		if (_members.TryGetValue(sender, out var member))
		{
			member.RttMs = LastRttMs;
		}
	}

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
			foreach (var memberId in _members.Keys.ToList())
			{
				if (!lobbyMembers.Contains(memberId))
				{
					RemoveMember(memberId, "left the lobby");
				}
			}

			if (_members.Count == 0)
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
		if (!_members.TryGetValue(steamId, out _))
		{
			return;
		}

		_members.Remove(steamId);
		_log.LogInformation("Member {Member} removed: {Reason}.", steamId, reason);
		FireMemberRemoved(steamId);
	}

	/// <summary>Guest side: drop a roster member (no broadcast — only the host fans out).</summary>
	internal void RemoveGuestMember(ulong steamId)
	{
		_members.Remove(steamId);
		FireMemberRemoved(steamId);
	}

	internal void EndSession()
	{
		if (!SessionActive && _members.Count == 0)
		{
			return;
		}

		_members.Clear();
		SessionActive = false;
		_identity.HostSteamId = 0;
		// Role is NOT reset here: it follows the lobby identity (the lobby
		// creator stays Host, a joiner stays Guest) — the session content is
		// gone, but a returning guest's handshake is still accepted and rebuilds
		// everything (new member + character save restore).
		_log.LogInformation("Session ended (role {Role} kept).", Role);
		SessionEnded?.Invoke(); // the entity domain + the Game Adapter tear down on this
	}

	// ---- Data plane (the PacketGateway owns transport binding + dispatch) ----

	// ---- Broadcast helpers (star fan-out) ----

	/// <summary>Send a message to every member (host side; no-op as guest — the only peer is the host).</summary>
	internal void Broadcast(NetMsg msg, object payload)
	{
		foreach (var member in _members.Values)
		{
			Send(member.SteamId, msg, payload);
		}
	}

	/// <summary>Broadcast to every member except one — relay semantics: the source already applied the change locally.</summary>
	internal void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload)
	{
		foreach (var member in _members.Values)
		{
			if (member.SteamId != excludeSteamId)
			{
				Send(member.SteamId, msg, payload);
			}
		}
	}

	internal MemberPresence GetOrCreateMember(ulong steamId)
	{
		if (!_members.TryGetValue(steamId, out var member))
		{
			member = new MemberPresence { SteamId = steamId };
			_members[steamId] = member;
		}

		return member;
	}

	/// <summary>
	/// Send a message through the gateway. Reliable by default — only the
	/// 20 Hz state stream (PlayerState/PlayerStateReport) goes unreliable, where
	/// overwrite semantics + snapshot sequence make drops harmless and avoid
	/// head-of-line blocking of the newest snapshot behind retransmissions.
	/// </summary>
	internal void Send(ulong steamId, NetMsg msg, object? payload = null, bool reliable = true) =>
		_gateway.Send(steamId, msg, payload, reliable);

	/// <summary>Local scene state as a wire value — the handshake messages carry it
	/// (assembled inline with an object initializer at the call site).</summary>
	internal SceneStateType LocalSceneState => _localInWorld ? SceneStateType.InWorld : SceneStateType.InMenu;

	private HandshakeMsg CreateHandshakeMsg() => new()
	{
		Protocol = ProtocolVersion.Current,
		Scene = new SceneStateMsg { State = (byte)LocalSceneState },
	};
}
