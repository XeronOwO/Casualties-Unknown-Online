using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

public enum SessionRole
{
	None,
	Host,
	Guest,
}

public enum SceneStateType : byte
{
	InMenu = 0,
	InWorld = 1,
}

/// <summary>
/// World-start parameters captured by the host at run start and applied by
/// guests before their own world generation. The game's world gen is fully
/// non-deterministic (no seed, Unity global Random everywhere) — restoring the
/// host's Random.state plus run settings is the only way to produce the same
/// world on both sides (see docs/game-internals.md).
/// </summary>
public sealed class WorldStartParams
{
	// NOTE: plain set, not init — net48 lacks IsExternalInit.
	public byte[] RandomState { get; set; } = [];

	public byte BiomeOverride { get; set; }

	public byte BiomeDepth { get; set; }

	public int TotalTraveled { get; set; }

	public bool LoadedRun { get; set; }

	public Dictionary<string, object>? RunSettings { get; set; }
}

/// <summary>
/// Session state machine: lobby → handshake (protocol/version) → scene-state
/// exchange → entity sync (local compute, remote verify/sync, architecture.md
/// §3). Owns the wire protocol dispatch on top of SteamTransport and the
/// member table. Star topology: every message flows guest → host; the host
/// arbitrates and decides the fan-out (pure star, no envelope).
/// </summary>
public sealed class SessionService : ICuoService
{
	private const float StateSendInterval = 0.05f; // 20 Hz authoritative snapshot
	private const float ReportSendInterval = 0.05f; // 20 Hz guest state report
	private const float PingInterval = 5f;
	private const float MemberCheckInterval = 2f;
	private const float HandshakeRetryInterval = 3f; // lazy Steam P2P sessions swallow early messages

	/// <summary>
	/// One remote peer's session state. Host: one entry per guest. Guest: one
	/// for the host plus roster entries for the other guests — the host
	/// broadcasts the full entity list, so every side renders every member.
	/// Key = SteamId (stable across reconnects); EntityId is re-allocated per join.
	/// </summary>
	private sealed class MemberState
	{
		public ulong SteamId;
		public PlayerEntity Entity = null!; // remote render buffer
		public bool Handshaken; // protocol handshake completed
		public bool EntitySync; // entity sync active for this member (host side)
		public uint LastReportSeq; // host side: last applied PlayerStateReport seq
		public float RttMs = -1f; // per-member ping diagnostics
	}

	private readonly SteamService _steam;
	private readonly SteamTransport _transport;
	private readonly ILogger<SessionService> _log;

	private readonly PlayerEntity _localPlayer;
	private readonly Dictionary<ulong, MemberState> _members = [];
	private WorldStartParams? _worldParams;
	private readonly Dictionary<ulong, CharacterDataMsg> _savedCharacters = []; // host: last report per SteamID
	private ulong _epoch;
	private uint _nextEntityCounter;

	private long _nextStateSendMs;
	private long _nextReportSendMs;
	private long _nextPingMs;
	private long _nextMemberCheckMs;
	private long _nextHandshakeRetryMs;

	// Snapshot sequence for the unreliable state stream: the sender numbers
	// every broadcast/report, the receiver drops anything at or below the last
	// applied one (the unreliable channel can reorder and duplicate).
	private uint _nextStateSeq; // host: PlayerState broadcasts
	private uint _nextReportSeq; // guest: PlayerStateReport broadcasts
	private uint _lastStateSeq; // guest: last applied host snapshot seq
	private bool _entitySyncActive; // guest: self sync state (host derives per member)

	public SessionService(SteamService steam, SteamTransport transport, ILogger<SessionService> log)
	{
		_steam = steam;
		_transport = transport;
		_log = log;
		_localPlayer = new PlayerEntity(steam.LocalSteamId, default, isLocal: true);

		transport.MessageReceived += OnMessage;
		steam.LobbyCreated += OnLobbyCreated;
		steam.LobbyEntered += OnLobbyEntered;
	}

	public SessionRole Role { get; private set; }

	/// <summary>True once the handshake completed (protocol versions agreed).</summary>
	public bool SessionActive { get; private set; }

	/// <summary>
	/// True while entity sync is active. Host: any member in sync (each member
	/// syncs independently); guest: the self-sync flag (set by the self
	/// PlayerJoin, cleared on leave). The Game Adapter renders per member and
	/// does not gate on this — it is informational (HUD).
	/// </summary>
	public bool EntitySyncActive => Role == SessionRole.Host
		? _members.Values.Any(m => m.EntitySync)
		: _entitySyncActive;

	public ulong HostSteamId { get; private set; }

	public PlayerEntity LocalPlayer => _localPlayer;

	/// <summary>All remote members (host: one per guest; guest: the host plus roster guests).</summary>
	public IEnumerable<PlayerEntity> RemotePlayers => _members.Values.Select(m => m.Entity);

	/// <summary>Remote member by SteamId, or null.</summary>
	public PlayerEntity? GetRemotePlayer(ulong steamId) =>
		_members.TryGetValue(steamId, out var member) ? member.Entity : null;

	public WorldStartParams? WorldParams => _worldParams;

	public float LastRttMs { get; private set; } = -1f;

	/// <summary>Raised when the handshake completes and scene exchange can start (first member only).</summary>
	public event Action? SessionActivated;

	/// <summary>Raised when the session ends (all members gone, lobby left, …).</summary>
	public event Action? SessionEnded;

	/// <summary>Raised when a member's entity sync starts (host: that guest; guest: host or a roster member).</summary>
	public event Action<PlayerEntity>? RemoteJoined;

	/// <summary>
	/// Raised when a member enters or leaves the world (inWorld=false pauses /
	/// destroys the render clone; a member leaving the session reuses
	/// inWorld=false so the clone teardown path is shared). The SteamId routes
	/// the event to the right clone.
	/// </summary>
	public event Action<ulong, bool>? RemoteSceneChanged;

	/// <summary>Both sides: a state message refreshed the entity buffer (PlayerState on guest, PlayerStateReport on host).</summary>
	public event Action<PlayerEntity>? StateReceived;

	// ---- Local state submission (Game Adapter → session) ----

	/// <summary>Host side: current authoritative state of the local body.</summary>
	public void PublishLocalState(NetVector2 position, NetVector2 lookPos, NetVector2 velocity,
		bool isRight, bool standing, bool alive, bool conscious, bool crouching,
		bool sitting = false, bool sleeping = false, bool climbing = false)
	{
		_localPlayer.Position = position;
		_localPlayer.LookPos = lookPos;
		_localPlayer.Velocity = velocity;
		_localPlayer.IsRight = isRight;
		_localPlayer.Standing = standing;
		_localPlayer.Alive = alive;
		_localPlayer.Conscious = conscious;
		_localPlayer.Crouching = crouching;
		_localPlayer.Sitting = sitting;
		_localPlayer.Sleeping = sleeping;
		_localPlayer.Climbing = climbing;
	}

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
		_localPlayer.InWorld = state == SceneStateType.InWorld;
		if (SessionActive)
		{
			var msg = new SceneStateMsg
			{
				State = (byte)state,
				SceneName = sceneName,
				Position = NetVector2Msg.From(localPosition ?? default),
				SteamId = _localPlayer.SteamId,
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

		var msg = WorldStartParamsMsg.From(parameters);
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
		var msg = new PingMsg { Ticks = DateTime.UtcNow.Ticks };
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
	/// Guest side: report the local character snapshot to the host (1-2 Hz,
	/// driven by the Game Adapter). The host keeps the latest per SteamID and
	/// hands it back when the same player reconnects.
	/// </summary>
	public void ReportCharacterData(CharacterDataMsg msg)
	{
		if (Role != SessionRole.Guest || !SessionActive)
		{
			return;
		}

		Send(HostSteamId, NetMsg.CharacterData, msg);
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
			Position = NetVector2Msg.From(worldPos),
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

	/// <summary>
	/// Guest side: the host sent a saved character snapshot back (reconnect
	/// restore) — apply it in the Game Adapter once the local body exists.
	/// </summary>
	public event Action<CharacterDataMsg>? CharacterDataReceived;

	void ICuoService.Initialize()
	{
		_epoch = (ulong)DateTime.UtcNow.Ticks;
		// Steam API init (SteamService.Initialize) runs before us in registration
		// order — refresh the local SteamID captured at construction time (it was
		// still 0 then, and guest input messages are sent to the host's SteamID).
		_localPlayer.SteamId = _steam.LocalSteamId;
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

		// Either side leaving the world ends the sync: the peer renders our clone
		// from state we no longer publish. The SceneState message we reported
		// already told the peer — its own self-check (this same branch) ends its
		// side, so both stop the stream without waiting for a round-trip.
		if (EntitySyncActive && !_localPlayer.InWorld)
		{
			EndEntitySync();
		}

		if (Role == SessionRole.Host && EntitySyncActive && nowMs >= _nextStateSendMs)
		{
			_nextStateSendMs = nowMs + (long)(StateSendInterval * 1000f);
			BroadcastPlayerState();
		}

		// Opportunistic entity-sync start: retried every frame (cheap, idempotent)
		// instead of only on message arrival — the local InWorld flag is set by
		// the Game Adapter's Update, which may run after a peer's InWorld message
		// was already processed, and the sync would otherwise never start. Runs
		// unconditionally (per member, each starts on its own conditions).
		if (Role == SessionRole.Host)
		{
			MaybeStartEntitySync();
		}

		if (Role == SessionRole.Guest && EntitySyncActive && nowMs >= _nextReportSendMs)
		{
			_nextReportSendMs = nowMs + (long)(ReportSendInterval * 1000f);
			SendPlayerStateReport();
		}

		CheckPeerPresence();
	}

	void ICuoService.Stop()
	{
	}

	void ICuoService.Dispose()
	{
		_transport.MessageReceived -= OnMessage;
		_steam.LobbyCreated -= OnLobbyCreated;
		_steam.LobbyEntered -= OnLobbyEntered;
	}

	// ---- Lobby / handshake ----

	private void OnLobbyCreated(ulong lobbyId)
	{
		Role = SessionRole.Host;
		HostSteamId = _steam.LocalSteamId;
		_log.LogInformation("Session role: Host (lobby {LobbyId})", lobbyId);
	}

	private void OnLobbyEntered(ulong lobbyId)
	{
		if (Role == SessionRole.Host)
		{
			return; // our own lobby — the create callback already ran
		}

		Role = SessionRole.Guest;
		HostSteamId = _steam.GetLobbyMembers().FirstOrDefault(m => m != _steam.LocalSteamId);
		_log.LogInformation("Session role: Guest (lobby {LobbyId}, host {Host})", lobbyId, HostSteamId);

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
		var peer = _steam.GetLobbyMembers().FirstOrDefault(m => m != _steam.LocalSteamId);
		if (peer != 0)
		{
			Send(peer, NetMsg.Ping, new PingMsg { Ticks = DateTime.UtcNow.Ticks });
		}
	}

	private void OnHandshake(ulong sender, HandshakeMsg msg)
	{
		if (Role != SessionRole.Host)
		{
			return;
		}

		var protocol = msg.Protocol;
		var peerState = (SceneStateType)msg.Scene.State;
		if (protocol != ProtocolVersion.Current)
		{
			_log.LogWarning("Peer {Peer} speaks protocol {PeerProtocol}; we speak {Current}. Rejecting.",
				sender, protocol, ProtocolVersion.Current);
			return;
		}

		// Star network: only lobby members may join — the lobby is the roster.
		// A third player is no longer rejected, they become a new member.
		if (!_steam.GetLobbyMembers().Contains(sender))
		{
			_log.LogWarning("Handshake from {Peer} ignored: not a lobby member.", sender);
			return;
		}

		var wasActive = SessionActive;
		if (!_members.TryGetValue(sender, out var member))
		{
			member = new MemberState
			{
				SteamId = sender,
				Entity = new PlayerEntity(sender, default, isLocal: false)
				{
					InWorld = peerState == SceneStateType.InWorld,
				},
			};
			_members[sender] = member;
			// Cross-session restore: the in-memory character save outlives the
			// session (kept per SteamID for the process lifetime) — a returning
			// player gets it back even in a brand-new session.
			SendSavedCharacter(sender);
		}
		else
		{
			// Reconnect from the same player while the entity is still held
			// (within the presence-check window, or a quick lobby round trip):
			// identity is the SteamID — reuse the entity. The normal flow
			// (session re-activation → scene re-report → entity sync) then
			// re-establishes everything, character data included.
			member.Entity.InWorld = peerState == SceneStateType.InWorld;
			_log.LogInformation("Peer {Peer} reconnected — entity reused.", sender);
			SendSavedCharacter(sender);
		}

		member.Handshaken = true;
		if (!wasActive)
		{
			// Fire the session-level event once, on the first member — later
			// members only take the member-level path below.
			SessionActive = true;
			_log.LogInformation("Handshake complete with {Peer}.", sender);
			SessionActivated?.Invoke();
		}

		MaybeStartEntitySync();

		// Ack on every handshake, even repeats: the guest retransmits its
		// handshake until it receives one (Steam P2P sessions establish lazily,
		// first messages can be swallowed — Phase-0 finding). Same for world
		// params, which are only sent once the session exists.
		Send(sender, NetMsg.HandshakeAck, new HandshakeAckMsg
		{
			Protocol = ProtocolVersion.Current,
			Scene = CreateSceneStateMsg(),
			HasWorldParams = _worldParams is not null,
		});
		if (_worldParams is not null)
		{
			Send(sender, NetMsg.WorldStartParams, WorldStartParamsMsg.From(_worldParams));
		}
	}

	private void OnHandshakeAck(ulong sender, HandshakeAckMsg msg)
	{
		var protocol = msg.Protocol;
		var hostState = (SceneStateType)msg.Scene.State;
		if (protocol != ProtocolVersion.Current)
		{
			_log.LogWarning("Host {Host} speaks protocol {HostProtocol}; we speak {Current}. Ending session.",
				sender, protocol, ProtocolVersion.Current);
			EndSession();
			return;
		}

		// Upsert: a repeated ack (retransmission / reconnect) must not rebuild
		// the entity — that would reset the interpolation buffer and teleport
		// the clone back to its spawn anchor.
		var member = GetOrCreateMember(sender);
		member.Entity.InWorld = hostState == SceneStateType.InWorld;
		member.Handshaken = true;

		var wasActive = SessionActive;
		SessionActive = true;
		if (!wasActive)
		{
			_log.LogInformation("Handshake complete with host {Host}.", sender);
			SessionActivated?.Invoke();
		}

		// The ack carries the host's scene state — surface it like a regular
		// scene change so a reconnecting guest follows the host into a world
		// that is already running (Game Adapter auto-starts the run).
		RemoteSceneChanged?.Invoke(sender, hostState == SceneStateType.InWorld);
	}

	// ---- Scene state ----

	private void OnSceneState(ulong sender, SceneStateMsg msg)
	{
		// The reporter is msg.SteamId when the host relays another member's
		// change; the sender itself otherwise (msg.SteamId is stamped by the
		// reporter in ReportSceneState).
		var reporter = msg.SteamId != 0 ? msg.SteamId : sender;
		if (!_members.TryGetValue(reporter, out var member))
		{
			return;
		}

		var wasInWorld = member.Entity.InWorld;
		member.Entity.InWorld = msg.State == (byte)SceneStateType.InWorld;
		member.Entity.ReportedSpawnPos = msg.Position.ToNetVector2();

		_log.LogInformation("Peer {Peer} scene state: {State} ({SceneName})", reporter, (SceneStateType)msg.State, msg.SceneName);
		if (wasInWorld != member.Entity.InWorld)
		{
			// Either side pauses when a member leaves the world: the member's
			// state stream stops and the render clone is torn down; re-entering
			// re-activates the same entity.
			if (member.Entity.InWorld)
			{
				RemoteSceneChanged?.Invoke(reporter, true);
				if (Role == SessionRole.Host)
				{
					MaybeStartEntitySync();
				}
			}
			else
			{
				if (Role == SessionRole.Host)
				{
					EndMemberSync(member);
				}
				else if (reporter == HostSteamId)
				{
					EndEntitySync(); // the host left the world — our sync ends
				}

				RemoteSceneChanged?.Invoke(reporter, false);
			}
		}
	}

	// ---- World params ----

	private void OnWorldStartParams(ulong sender, WorldStartParamsMsg msg)
	{
		_worldParams = msg.ToWorldStartParams();
		_log.LogInformation("Received world params ({StateBytes} bytes, loaded run: {LoadedRun}).",
			_worldParams.RandomState.Length, _worldParams.LoadedRun);
	}

	// ---- Entities ----

	/// <summary>
	/// Host side: start entity sync for every handshaken member that is in
	/// world (idempotent, retried every frame). Members sync independently — a
	/// mid-session joiner starts on its own.
	/// </summary>
	private void MaybeStartEntitySync()
	{
		if (Role != SessionRole.Host || !SessionActive)
		{
			return;
		}

		foreach (var member in _members.Values)
		{
			if (member.Handshaken && !member.EntitySync && _localPlayer.InWorld && member.Entity.InWorld)
			{
				StartMemberSync(member);
			}
		}
	}

	/// <summary>Host side: allocate ids, announce the member (self-activation +
	/// roster), and push the first snapshot so the clone renders immediately.</summary>
	private void StartMemberSync(MemberState member)
	{
		member.Entity.EntityId = AllocateEntityId();
		member.EntitySync = true;
		member.LastReportSeq = 0; // the member re-joins with a fresh sequence space

		var joinMsg = new PlayerJoinMsg
		{
			HostSteamId = _localPlayer.SteamId,
			HostEntityId = NetworkEntityIdMsg.From(_localPlayer.EntityId),
			GuestSteamId = member.SteamId,
			GuestEntityId = NetworkEntityIdMsg.From(member.Entity.EntityId),
			HostPosition = NetVector2Msg.From(_localPlayer.Position),
			GuestPosition = NetVector2Msg.From(member.Entity.ReportedSpawnPos),
		};
		Send(member.SteamId, NetMsg.PlayerJoin, joinMsg); // self-activation
		BroadcastExcept(member.SteamId, NetMsg.PlayerJoin, joinMsg); // roster: announce to the others
		_log.LogInformation("PlayerJoin sent: local {Local} ({LocalId}), member {Guest} ({GuestId}).",
			_localPlayer.SteamId, _localPlayer.EntityId, member.SteamId, member.Entity.EntityId);
		RemoteJoined?.Invoke(member.Entity);

		// Immediate full snapshot right after PlayerJoin — the guest's clone
		// renders the very first frame instead of waiting up to one 20 Hz tick
		// for the next broadcast (same mechanism serves respawn/reconnect).
		BroadcastPlayerState();
	}

	/// <summary>Guest side: self-activation (the host assigned our id) or a roster
	/// announcement (another member joined — upsert with its spawn anchor).</summary>
	private void OnPlayerJoin(ulong sender, PlayerJoinMsg msg)
	{
		if (Role != SessionRole.Guest)
		{
			return;
		}

		if (msg.GuestSteamId == _localPlayer.SteamId)
		{
			_localPlayer.EntityId = msg.GuestEntityId.ToNetworkEntityId();
			var host = GetOrCreateMember(msg.HostSteamId);
			host.Entity.SteamId = msg.HostSteamId; // backfill (session already knows it)
			host.Entity.EntityId = msg.HostEntityId.ToNetworkEntityId();
			host.Entity.Position = msg.HostPosition.ToNetVector2();
			_entitySyncActive = true;
			_lastStateSeq = 0; // host's snapshot sequence restarts with this join
			_log.LogInformation("PlayerJoin received: local {Local}, host {Host} at {Position}.",
				_localPlayer.EntityId, host.Entity.EntityId, host.Entity.Position);
			RemoteJoined?.Invoke(host.Entity);
		}
		else
		{
			var member = GetOrCreateMember(msg.GuestSteamId);
			member.Entity.EntityId = msg.GuestEntityId.ToNetworkEntityId();
			member.Entity.Position = msg.GuestPosition.ToNetVector2();
			member.Entity.InWorld = true;
			member.Handshaken = true;
			_log.LogInformation("Roster join: member {Guest} ({GuestId}) at {Position}.",
				msg.GuestSteamId, member.Entity.EntityId, member.Entity.Position);
			RemoteJoined?.Invoke(member.Entity);
		}
	}

	/// <summary>Guest side: the host announced a member left — drop it (clone teardown via RemoteSceneChanged).</summary>
	private void OnPlayerLeave(ulong sender, PlayerLeaveMsg msg)
	{
		if (Role != SessionRole.Guest || msg.SteamId == _localPlayer.SteamId)
		{
			return;
		}

		if (!_members.TryGetValue(msg.SteamId, out var member))
		{
			return;
		}

		_members.Remove(msg.SteamId);
		_log.LogInformation("Member {Member} left (PlayerLeave).", msg.SteamId);
		RemoteSceneChanged?.Invoke(msg.SteamId, false);
	}

	/// <summary>Guest → host: the guest's locally simulated state (host renders it, no host-side simulation).</summary>
	private void OnPlayerStateReport(ulong sender, PlayerStateReportMsg msg)
	{
		if (Role != SessionRole.Host || !_members.TryGetValue(sender, out var member))
		{
			return;
		}

		// Unreliable stream: drop stale snapshots (reordered or duplicate).
		// Each member has its own sequence space — the counter lives on the member.
		if (msg.Seq <= member.LastReportSeq)
		{
			return;
		}

		member.LastReportSeq = msg.Seq;

		// Ownership check: the report must carry the member's own entity id —
		// an id we allocated to the member (or stale) means a misbehaving peer.
		var reportedId = msg.Entity.Id.ToNetworkEntityId();
		if (reportedId != member.Entity.EntityId)
		{
			_log.LogWarning("Dropping report from {Sender}: entity {Id} is not the member's {Expected}.",
				sender, reportedId, member.Entity.EntityId);
			return;
		}

		ApplyEntityState(msg.Entity, member.Entity);
		StateReceived?.Invoke(member.Entity);
	}

	private void OnPlayerState(ulong sender, PlayerStateMsg msg)
	{
		if (Role != SessionRole.Guest)
		{
			return;
		}

		// Unreliable stream: drop stale snapshots (reordered or duplicate).
		// The broadcast stream has a single source (the host).
		if (msg.Seq <= _lastStateSeq)
		{
			return;
		}

		_lastStateSeq = msg.Seq;

		foreach (var entity in msg.Entities)
		{
			var id = entity.Id.ToNetworkEntityId();
			var target = id == _localPlayer.EntityId ? _localPlayer
				: _members.Values.FirstOrDefault(m => m.Entity.EntityId == id)?.Entity;
			if (target is null)
			{
				_log.LogWarning("Dropping entity state {Id} from {Sender}: no member with that entity id.",
					id, sender);
				continue;
			}

			ApplyEntityState(entity, target);
		}

		StateReceived?.Invoke(_localPlayer);
	}

	/// <summary>Applies a decoded entity state, preserving the first-snapshot rule
	/// (Prev = current on the first report — the buffer defaults are (0,0) and
	/// interpolating from them would slide the proxy in from the world origin).</summary>
	private static void ApplyEntityState(EntityStateMsg msg, PlayerEntity target)
	{
		var firstSnapshot = target.StateReceivedMs < 0;
		target.PrevPosition = firstSnapshot ? msg.Position.ToNetVector2() : target.Position;
		target.PrevLookPos = firstSnapshot ? msg.LookPos.ToNetVector2() : target.LookPos;
		target.PrevVelocity = firstSnapshot ? msg.Velocity.ToNetVector2() : target.Velocity;
		msg.ApplyTo(target);
		target.StateReceivedMs = Environment.TickCount;
	}

	/// <summary>Host side: broadcast the authoritative snapshot (local + every synced member) to all synced members.</summary>
	private void BroadcastPlayerState()
	{
		var synced = _members.Values.Where(m => m.EntitySync).ToList();
		if (synced.Count == 0)
		{
			return;
		}

		var payload = new PlayerStateMsg
		{
			Seq = ++_nextStateSeq,
			Entities = BuildEntityList(),
		};
		foreach (var member in synced)
		{
			Send(member.SteamId, NetMsg.PlayerState, payload, reliable: false);
		}
	}

	private List<EntityStateMsg> BuildEntityList()
	{
		var list = new List<EntityStateMsg> { EntityStateMsg.From(_localPlayer) };
		foreach (var member in _members.Values)
		{
			if (member.EntitySync)
			{
				list.Add(EntityStateMsg.From(member.Entity));
			}
		}

		return list;
	}

	/// <summary>Guest side: report the locally simulated state to the host (20 Hz).</summary>
	private void SendPlayerStateReport()
	{
		if (Role != SessionRole.Guest || HostSteamId == 0)
		{
			return;
		}

		Send(HostSteamId, NetMsg.PlayerStateReport,
			new PlayerStateReportMsg
			{
				Seq = ++_nextReportSeq,
				Entity = EntityStateMsg.From(_localPlayer),
			}, reliable: false);
	}

	// ---- Character data (session-scoped save/restore) ----

	private void OnCharacterData(ulong sender, CharacterDataMsg msg)
	{
		if (Role == SessionRole.Host)
		{
			_savedCharacters[sender] = msg;
			_log.LogDebug("Saved character data for {Peer} ({Items} items).", sender, msg.Items.Count);
			return;
		}

		CharacterDataReceived?.Invoke(msg);
	}

	/// <summary>Host side: hand the saved character data back to a reconnecting player.</summary>
	private void SendSavedCharacter(ulong steamId)
	{
		if (_savedCharacters.TryGetValue(steamId, out var data))
		{
			Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

	// ---- Ping / pong (diagnostics) ----

	private void OnPing(ulong sender, PingMsg msg) => Send(sender, NetMsg.Pong, new PongMsg { Ticks = msg.Ticks });

	private void OnPong(ulong sender, PongMsg msg)
	{
		LastRttMs = (DateTime.UtcNow.Ticks - msg.Ticks) / 10_000f;
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

		// Peer vanished from the lobby — end the session immediately (either
		// role: host's guest is gone, or MVP guest's host is gone — no host
		// migration). Reconnects are cheap: Role stays (it follows the lobby
		// identity), the character save is kept per SteamID, and the next
		// handshake rebuilds the session from scratch (new entity + restore).
		if (_steam.GetLobbyMembers().Length < 2)
		{
			_log.LogWarning("Peer left the lobby — ending session (save kept).");
			EndSession();
		}
	}

	/// <summary>Host side: end one member's entity sync (its stream stops, its clone is torn down).</summary>
	private void EndMemberSync(MemberState member)
	{
		if (!member.EntitySync)
		{
			return;
		}

		member.EntitySync = false;
		_log.LogInformation("Entity sync ended for {Member}.", member.SteamId);
	}

	/// <summary>End entity sync for every member (host) or the self sync (guest).</summary>
	private void EndEntitySync()
	{
		if (Role == SessionRole.Host)
		{
			foreach (var member in _members.Values)
			{
				EndMemberSync(member);
			}

			return;
		}

		if (!_entitySyncActive)
		{
			return;
		}

		_entitySyncActive = false;
		_log.LogInformation("Entity sync ended.");
	}

	private void EndSession()
	{
		if (!SessionActive && _members.Count == 0)
		{
			return;
		}

		EndEntitySync();
		_members.Clear();
		SessionActive = false;
		HostSteamId = 0;
		// Role is NOT reset here: it follows the lobby identity (the lobby
		// creator stays Host, a joiner stays Guest) — the session content is
		// gone, but a returning guest's handshake is still accepted and rebuilds
		// everything (new member + character save restore).
		_log.LogInformation("Session ended (role {Role} kept).", Role);
		SessionEnded?.Invoke();
	}

	// ---- Wire helpers ----

	private void OnMessage(ulong sender, byte[] frame)
	{
		if (frame.Length < 1)
		{
			return;
		}

		var msgId = (NetMsg)frame[0];
		if (!IsValidDirection(msgId))
		{
			_log.LogWarning("Dropping {Msg} from {Sender}: illegal direction for role {Role}.", msgId, sender, Role);
			return;
		}

		switch (msgId)
		{
			case NetMsg.Ping:
				OnPing(sender, NetPacket.DecodePayload<PingMsg>(frame));
				break;
			case NetMsg.Pong:
				OnPong(sender, NetPacket.DecodePayload<PongMsg>(frame));
				break;
			case NetMsg.Handshake:
				OnHandshake(sender, NetPacket.DecodePayload<HandshakeMsg>(frame));
				break;
			case NetMsg.HandshakeAck:
				OnHandshakeAck(sender, NetPacket.DecodePayload<HandshakeAckMsg>(frame));
				break;
			case NetMsg.SceneState:
				OnSceneState(sender, NetPacket.DecodePayload<SceneStateMsg>(frame));
				break;
			case NetMsg.WorldStartParams:
				OnWorldStartParams(sender, NetPacket.DecodePayload<WorldStartParamsMsg>(frame));
				break;
			case NetMsg.PlayerJoin:
				OnPlayerJoin(sender, NetPacket.DecodePayload<PlayerJoinMsg>(frame));
				break;
			case NetMsg.PlayerLeave:
				OnPlayerLeave(sender, NetPacket.DecodePayload<PlayerLeaveMsg>(frame));
				break;
			case NetMsg.PlayerStateReport:
				OnPlayerStateReport(sender, NetPacket.DecodePayload<PlayerStateReportMsg>(frame));
				break;
			case NetMsg.PlayerState:
				OnPlayerState(sender, NetPacket.DecodePayload<PlayerStateMsg>(frame));
				break;
			case NetMsg.CharacterData:
				OnCharacterData(sender, NetPacket.DecodePayload<CharacterDataMsg>(frame));
				break;
			case NetMsg.BlockDamaged:
				OnBlockDamaged(NetPacket.DecodePayload<BlockDamagedMsg>(frame));
				break;
		}
	}

	/// <summary>
	/// One-way messages must arrive at the role they were sent to. Anything
	/// else means a misbehaving peer or a stale message from a previous
	/// session — drop it instead of processing.
	/// </summary>
	private bool IsValidDirection(NetMsg msgId)
	{
		switch (msgId)
		{
			case NetMsg.Handshake:
			case NetMsg.PlayerStateReport:
				return Role == SessionRole.Host;
			case NetMsg.HandshakeAck:
			case NetMsg.WorldStartParams:
			case NetMsg.PlayerJoin:
			case NetMsg.PlayerLeave:
			case NetMsg.PlayerState:
				return Role == SessionRole.Guest;
			default:
				// Ping/Pong/SceneState/BlockDamaged/CharacterData: bidirectional —
				// report up (guest → host) and broadcast down (host → guest)
				// share one message id.
				return true;
		}
	}

	private void OnBlockDamaged(BlockDamagedMsg msg) => BlockDamagedReceived?.Invoke(msg.Position.ToNetVector2(), msg.Damage);

	// ---- Broadcast helpers (star fan-out) ----

	/// <summary>Send a message to every member (host side; no-op as guest — the only peer is the host).</summary>
	private void Broadcast(NetMsg msg, object payload)
	{
		foreach (var member in _members.Values)
		{
			Send(member.SteamId, msg, payload);
		}
	}

	/// <summary>Broadcast to every member except one — relay semantics: the source already applied the change locally.</summary>
	private void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload)
	{
		foreach (var member in _members.Values)
		{
			if (member.SteamId != excludeSteamId)
			{
				Send(member.SteamId, msg, payload);
			}
		}
	}

	private MemberState GetOrCreateMember(ulong steamId)
	{
		if (!_members.TryGetValue(steamId, out var member))
		{
			member = new MemberState
			{
				SteamId = steamId,
				Entity = new PlayerEntity(steamId, default, isLocal: false),
			};
			_members[steamId] = member;
		}

		return member;
	}

	/// <summary>
	/// Send a message. Reliable by default — only the 20 Hz state stream
	/// (PlayerState/PlayerStateReport) goes unreliable, where overwrite
	/// semantics + snapshot sequence make drops harmless and avoid head-of-line
	/// blocking of the newest snapshot behind retransmissions.
	/// </summary>
	private void Send(ulong steamId, NetMsg msg, object? payload = null, bool reliable = true)
	{
		if (steamId == 0)
		{
			return;
		}

		_transport.SendTo(steamId, NetPacket.Encode(msg, payload), reliable);
	}

	private NetworkEntityId AllocateEntityId()
	{
		if (_localPlayer.EntityId.Counter == 0 && _localPlayer.EntityId.Epoch == 0)
		{
			_localPlayer.EntityId = new NetworkEntityId(_epoch, _nextEntityCounter++, generation: 0);
		}

		return new NetworkEntityId(_epoch, _nextEntityCounter++, generation: 0);
	}

	private SceneStateType SceneStateForLocal() => _localPlayer.InWorld ? SceneStateType.InWorld : SceneStateType.InMenu;

	private SceneStateMsg CreateSceneStateMsg() => new()
	{
		State = (byte)SceneStateForLocal(),
		SceneName = "",
		Position = new NetVector2Msg(),
	};

	private HandshakeMsg CreateHandshakeMsg() => new()
	{
		Protocol = ProtocolVersion.Current,
		Scene = CreateSceneStateMsg(),
	};
}
