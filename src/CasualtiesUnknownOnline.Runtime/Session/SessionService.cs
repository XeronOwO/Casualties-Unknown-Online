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
/// → scene-state exchange → entity sync (local compute, remote verify/sync,
/// architecture.md §3). Owns the member table and the business-level send/receive
/// APIs; the data plane (transport binding, direction validation, dispatch to
/// the packet handlers) lives in <see cref="PacketGateway"/>. Star topology:
/// every message flows guest → host; the host arbitrates and decides the fan-out.
/// </summary>
public sealed class SessionService : ICuoService
{
	private const float PingInterval = 5f;
	private const float MemberCheckInterval = 2f;
	private const float HandshakeRetryInterval = 3f; // lazy Steam P2P sessions swallow early messages

	/// <summary>
	/// One remote peer's session state. Host: one entry per guest. Guest: one
	/// for the host plus roster entries for the other guests — the host
	/// broadcasts the full entity list, so every side renders every member.
	/// Key = SteamId (stable across reconnects); EntityId is re-allocated per join.
	/// </summary>
	internal sealed class MemberState
	{
		public ulong SteamId;
		public PlayerEntity Entity = null!; // remote render buffer
		public bool Handshaken; // protocol handshake completed
		public bool EntitySync; // entity sync active for this member (host side)
		public uint LastReportSeq; // host side: last applied PlayerStateReport seq
		public float RttMs = -1f; // per-member ping diagnostics
	}

	private readonly SteamService _steam;
	private readonly SessionIdentity _identity;
	private readonly PacketGateway _gateway;
	private readonly ILogger<SessionService> _log;

	private readonly PlayerEntity _localPlayer;
	private readonly Dictionary<ulong, MemberState> _members = [];
	private WorldStartParams? _worldParams;
	private readonly Dictionary<ulong, CharacterDataMsg> _savedCharacters = []; // host: last report per SteamID
	private ulong _epoch;
	private uint _nextEntityCounter;

	private long _nextPingMs;
	private long _nextMemberCheckMs;
	private long _nextHandshakeRetryMs;

	private bool _entitySyncActive; // guest: self sync state (host derives per member)

	public SessionService(SteamService steam, SessionIdentity identity, PacketGateway gateway, ILogger<SessionService> log)
	{
		_steam = steam;
		_identity = identity;
		_gateway = gateway;
		_log = log;
		_localPlayer = new PlayerEntity(steam.LocalSteamId, default, isLocal: true);

		steam.LobbyCreated += OnLobbyCreated;
		steam.LobbyEntered += OnLobbyEntered;
	}

	public SessionRole Role => _identity.Role;

	/// <summary>True once the handshake completed (protocol versions agreed). Set by the handshake handlers.</summary>
	public bool SessionActive { get; internal set; }

	/// <summary>
	/// True while entity sync is active. Host: any member in sync (each member
	/// syncs independently); guest: the self-sync flag (set by the self
	/// PlayerJoin, cleared on leave). The Game Adapter renders per member and
	/// does not gate on this — it is informational (HUD).
	/// </summary>
	public bool EntitySyncActive => Role == SessionRole.Host
		? _members.Values.Any(m => m.EntitySync)
		: _entitySyncActive;

	public ulong HostSteamId => _identity.HostSteamId;

	public PlayerEntity LocalPlayer => _localPlayer;

	/// <summary>All remote members (host: one per guest; guest: the host plus roster guests).</summary>
	public IEnumerable<PlayerEntity> RemotePlayers => _members.Values.Select(m => m.Entity);

	/// <summary>Remote member by SteamId, or null.</summary>
	public PlayerEntity? GetRemotePlayer(ulong steamId) =>
		_members.TryGetValue(steamId, out var member) ? member.Entity : null;

	/// <summary>Set by the world-params handler on the guest side.</summary>
	public WorldStartParams? WorldParams { get; internal set; }

	public float LastRttMs { get; private set; } = -1f;

	// ---- Internal surface for the packet handlers (Session/Handlers/) ----

	internal ulong LocalSteamId => _localPlayer.SteamId;

	/// <summary>Guest side: last applied host snapshot seq (stream gate).</summary>
	internal uint LastStateSeq { get; set; }

	internal IEnumerable<MemberState> Members => _members.Values;

	internal bool TryGetMember(ulong steamId, out MemberState member) =>
		_members.TryGetValue(steamId, out member!);

	internal bool IsLobbyMember(ulong steamId) => _steam.GetLobbyMembers().Contains(steamId);

	internal void SetEntitySyncActive(bool active) => _entitySyncActive = active;

	internal void ResetLastStateSeq() => LastStateSeq = 0;

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

	/// <summary>Host side: a member's sync state just flipped on — the sync stream sends the join packets.</summary>
	internal event Action<MemberState>? MemberSyncStarted;

	// ---- Event fires for the packet handlers (the events stay public — the
	// Game Adapter subscribes from another assembly; handlers fire through these). ----

	internal void FireSessionActivated() => SessionActivated?.Invoke();

	internal void FireRemoteJoined(PlayerEntity entity) => RemoteJoined?.Invoke(entity);

	internal void FireRemoteSceneChanged(ulong steamId, bool inWorld) => RemoteSceneChanged?.Invoke(steamId, inWorld);

	internal void FireStateReceived(PlayerEntity entity) => StateReceived?.Invoke(entity);

	internal void FireBlockDamagedReceived(NetVector2 pos, float damage) => BlockDamagedReceived?.Invoke(pos, damage);

	internal void FireCharacterDataReceived(CharacterDataMsg msg) => CharacterDataReceived?.Invoke(msg);

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

		// Opportunistic entity-sync start: retried every frame (cheap, idempotent)
		// instead of only on message arrival — the local InWorld flag is set by
		// the Game Adapter's Update, which may run after a peer's InWorld message
		// was already processed, and the sync would otherwise never start. Runs
		// unconditionally (per member, each starts on its own conditions).
		// The 20 Hz state-stream send/report throttling lives in EntitySyncStream.
		if (Role == SessionRole.Host)
		{
			MaybeStartEntitySync();
		}

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
		var ping = new PingMsg { Ticks = DateTime.UtcNow.Ticks };
		foreach (var peer in _steam.GetLobbyMembers())
		{
			if (peer != _steam.LocalSteamId)
			{
				Send(peer, NetMsg.Ping, ping);
			}
		}
	}

	// ---- Message handlers moved to Session/Handlers/ (HandshakeHandlers, SceneStateHandler, …) ----

	// ---- Entities ----

	/// <summary>
	/// Host side: start entity sync for every handshaken member that is in
	/// world (idempotent, retried every frame). Members sync independently — a
	/// mid-session joiner starts on its own.
	/// </summary>
	internal void MaybeStartEntitySync()
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

	/// <summary>
	/// Host side: allocate ids and flip the member's sync state — the packet
	/// assembly and sending (PlayerJoin self-activation + roster + first
	/// snapshot) are EntitySyncStream's job, driven by <see cref="MemberSyncStarted"/>.
	/// </summary>
	private void StartMemberSync(MemberState member)
	{
		member.Entity.EntityId = AllocateEntityId();
		member.EntitySync = true;
		member.LastReportSeq = 0; // the member re-joins with a fresh sequence space
		RemoteJoined?.Invoke(member.Entity);
		MemberSyncStarted?.Invoke(member);
	}

	/// <summary>Applies a decoded entity state, preserving the first-snapshot rule
	/// (Prev = current on the first report — the buffer defaults are (0,0) and
	/// interpolating from them would slide the proxy in from the world origin).</summary>
	internal static void ApplyEntityState(EntityStateMsg msg, PlayerEntity target)
	{
		var firstSnapshot = target.StateReceivedMs < 0;
		target.PrevPosition = firstSnapshot ? msg.Position.ToNetVector2() : target.Position;
		target.PrevLookPos = firstSnapshot ? msg.LookPos.ToNetVector2() : target.LookPos;
		target.PrevVelocity = firstSnapshot ? msg.Velocity.ToNetVector2() : target.Velocity;
		msg.ApplyTo(target);
		target.StateReceivedMs = Environment.TickCount;
	}

	// ---- Character data (session-scoped save/restore) ----

	/// <summary>Host side: keep the latest report per SteamID (session-scoped save).</summary>
	internal void SaveCharacterData(ulong steamId, CharacterDataMsg msg) => _savedCharacters[steamId] = msg;

	/// <summary>Host side: hand the saved character data back to a reconnecting player.</summary>
	internal void SendSavedCharacter(ulong steamId)
	{
		if (_savedCharacters.TryGetValue(steamId, out var data))
		{
			Send(steamId, NetMsg.CharacterData, data);
			_log.LogInformation("Sent saved character data to {Peer} ({Items} items).", steamId, data.Items.Count);
		}
	}

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

	/// <summary>Host side: drop a member (sync off, roster PlayerLeave, clone teardown).</summary>
	internal void RemoveMember(ulong steamId, string reason)
	{
		if (!_members.TryGetValue(steamId, out var member))
		{
			return;
		}

		EndMemberSync(member);
		_members.Remove(steamId);
		_log.LogInformation("Member {Member} removed: {Reason}.", steamId, reason);

		// Tell the other guests to drop the member's clone too.
		BroadcastExcept(steamId, NetMsg.PlayerLeave, new PlayerLeaveMsg
		{
			SteamId = steamId,
			EntityId = NetworkEntityIdMsg.From(member.Entity.EntityId),
		});
		FireRemoteSceneChanged(steamId, false);
	}

	/// <summary>Guest side: drop a roster member (no broadcast — only the host fans out).</summary>
	internal void RemoveGuestMember(ulong steamId)
	{
		_members.Remove(steamId);
		FireRemoteSceneChanged(steamId, false);
	}

	/// <summary>Host side: end one member's entity sync (its stream stops, its clone is torn down).</summary>
	internal void EndMemberSync(MemberState member)
	{
		if (!member.EntitySync)
		{
			return;
		}

		member.EntitySync = false;
		_log.LogInformation("Entity sync ended for {Member}.", member.SteamId);
	}

	/// <summary>End entity sync for every member (host) or the self sync (guest).</summary>
	internal void EndEntitySync()
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

	internal void EndSession()
	{
		if (!SessionActive && _members.Count == 0)
		{
			return;
		}

		EndEntitySync();
		_members.Clear();
		SessionActive = false;
		_identity.HostSteamId = 0;
		// Role is NOT reset here: it follows the lobby identity (the lobby
		// creator stays Host, a joiner stays Guest) — the session content is
		// gone, but a returning guest's handshake is still accepted and rebuilds
		// everything (new member + character save restore).
		_log.LogInformation("Session ended (role {Role} kept).", Role);
		SessionEnded?.Invoke();
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

	internal MemberState GetOrCreateMember(ulong steamId)
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
	/// Send a message through the gateway. Reliable by default — only the
	/// 20 Hz state stream (PlayerState/PlayerStateReport) goes unreliable, where
	/// overwrite semantics + snapshot sequence make drops harmless and avoid
	/// head-of-line blocking of the newest snapshot behind retransmissions.
	/// </summary>
	internal void Send(ulong steamId, NetMsg msg, object? payload = null, bool reliable = true) =>
		_gateway.Send(steamId, msg, payload, reliable);

	private NetworkEntityId AllocateEntityId()
	{
		if (_localPlayer.EntityId.Counter == 0 && _localPlayer.EntityId.Epoch == 0)
		{
			_localPlayer.EntityId = new NetworkEntityId(_epoch, _nextEntityCounter++, generation: 0);
		}

		return new NetworkEntityId(_epoch, _nextEntityCounter++, generation: 0);
	}

	private SceneStateType SceneStateForLocal() => _localPlayer.InWorld ? SceneStateType.InWorld : SceneStateType.InMenu;

	internal SceneStateMsg CreateSceneStateMsg() => new()
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
