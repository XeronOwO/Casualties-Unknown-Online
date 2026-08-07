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
/// §3). Owns the wire protocol dispatch on top of SteamTransport and the player
/// entity table. Phase 1 supports a single guest (first peer in the lobby).
/// </summary>
public sealed class SessionService : ICuoService
{
	private const float StateSendInterval = 0.05f; // 20 Hz authoritative snapshot
	private const float ReportSendInterval = 0.05f; // 20 Hz guest state report
	private const float PingInterval = 5f;
	private const float MemberCheckInterval = 2f;
	private const float HandshakeRetryInterval = 3f; // lazy Steam P2P sessions swallow early messages

	private readonly SteamService _steam;
	private readonly SteamTransport _transport;
	private readonly ILogger<SessionService> _log;

	private readonly PlayerEntity _localPlayer;
	private PlayerEntity? _remotePlayer; // Phase 1: single guest
	private WorldStartParams? _worldParams;
	private ulong _epoch;
	private uint _nextEntityCounter;

	private long _nextStateSendMs;
	private long _nextReportSendMs;
	private long _nextPingMs;
	private long _nextMemberCheckMs;
	private long _nextHandshakeRetryMs;

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

	/// <summary>True once both sides are InWorld and PlayerJoin has been exchanged.</summary>
	public bool EntitySyncActive { get; private set; }

	public ulong HostSteamId { get; private set; }

	public PlayerEntity LocalPlayer => _localPlayer;

	public PlayerEntity? RemotePlayer => _remotePlayer;

	public WorldStartParams? WorldParams => _worldParams;

	public float LastRttMs { get; private set; } = -1f;

	/// <summary>Raised when the handshake completes and scene exchange can start.</summary>
	public event Action? SessionActivated;

	/// <summary>Raised when the session ends (peer left, lobby left, …).</summary>
	public event Action? SessionEnded;

	/// <summary>Raised when the remote peer is ready and both sides are in-world.</summary>
	public event Action<PlayerEntity>? RemoteJoined;

	public event Action<PlayerEntity>? RemoteLeft;

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
	/// </summary>
	public void ReportSceneState(SceneStateType state, string sceneName, NetVector2? localPosition = null)
	{
		_localPlayer.InWorld = state == SceneStateType.InWorld;
		if (SessionActive)
		{
			Send(PeerSteamId(), NetMsg.SceneState, new SceneStateMsg
			{
				State = (byte)state,
				SceneName = sceneName,
				Position = NetVector2Msg.From(localPosition ?? default),
			});
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

		Send(PeerSteamId(), NetMsg.WorldStartParams, WorldStartParamsMsg.From(parameters));
		_log.LogInformation("Published world params ({StateBytes} bytes) to {Peer}",
			parameters.RandomState.Length, PeerSteamId());
	}

	/// <summary>Diagnostics: ping the peer (RTT recorded in <see cref="LastRttMs"/>).</summary>
	public void RequestPing() => Send(PeerSteamId(), NetMsg.Ping, new PingMsg { Ticks = DateTime.UtcNow.Ticks });

	/// <summary>
	/// Report a locally-performed block damage (local compute) so the peer can
	/// apply the same damage at the same world position (remote verify/sync).
	/// </summary>
	public void SendBlockDamaged(NetVector2 worldPos, float damage)
	{
		if (!SessionActive)
		{
			return;
		}

		Send(PeerSteamId(), NetMsg.BlockDamaged, new BlockDamagedMsg
		{
			Position = NetVector2Msg.From(worldPos),
			Damage = damage,
		});
	}

	/// <summary>The peer damaged a block — apply it locally (both directions).</summary>
	public event Action<NetVector2, float>? BlockDamagedReceived;

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

		if (Role == SessionRole.Host && EntitySyncActive && nowMs >= _nextStateSendMs)
		{
			_nextStateSendMs = nowMs + (long)(StateSendInterval * 1000f);
			BroadcastPlayerState();
		}

		// Opportunistic entity-sync start: retried every frame (cheap, idempotent)
		// instead of only on message arrival — the local InWorld flag is set by
		// the Game Adapter's Update, which may run after a peer's InWorld message
		// was already processed, and the sync would otherwise never start.
		if (Role == SessionRole.Host && !EntitySyncActive)
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

		if (_remotePlayer is null)
		{
			_remotePlayer = new PlayerEntity(sender, default, isLocal: false)
			{
				InWorld = peerState == SceneStateType.InWorld,
			};
			SessionActive = true;
			_log.LogInformation("Handshake complete with {Peer}.", sender);
			SessionActivated?.Invoke();
			MaybeStartEntitySync();
		}

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

		_remotePlayer = new PlayerEntity(sender, default, isLocal: false)
		{
			InWorld = hostState == SceneStateType.InWorld,
		};
		SessionActive = true;
		_log.LogInformation("Handshake complete with host {Host}.", sender);
		SessionActivated?.Invoke();
	}

	// ---- Scene state ----

	private void OnSceneState(ulong sender, SceneStateMsg msg)
	{
		if (_remotePlayer is null)
		{
			return;
		}

		var wasInWorld = _remotePlayer.InWorld;
		_remotePlayer.InWorld = msg.State == (byte)SceneStateType.InWorld;
		_remotePlayer.ReportedSpawnPos = msg.Position.ToNetVector2();

		_log.LogInformation("Peer {Peer} scene state: {State} ({SceneName})", sender, (SceneStateType)msg.State, msg.SceneName);
		if (wasInWorld != _remotePlayer.InWorld)
		{
			if (_remotePlayer.InWorld)
			{
				if (Role == SessionRole.Host)
				{
					MaybeStartEntitySync();
				}
			}
			else if (Role == SessionRole.Host)
			{
				EndEntitySync();
				RemoteLeft?.Invoke(_remotePlayer);
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

	private void MaybeStartEntitySync()
	{
		if (Role != SessionRole.Host || !SessionActive || _remotePlayer is null || EntitySyncActive)
		{
			return;
		}

		if (!_localPlayer.InWorld || !_remotePlayer.InWorld)
		{
			return;
		}

		_remotePlayer.EntityId = AllocateEntityId();
		EntitySyncActive = true;

		// Tell the guest: our entity id, their entity id, and our current position
		// (spawn anchor for the remote clone).
		Send(_remotePlayer.SteamId, NetMsg.PlayerJoin, new PlayerJoinMsg
		{
			HostSteamId = _localPlayer.SteamId,
			HostEntityId = NetworkEntityIdMsg.From(_localPlayer.EntityId),
			GuestEntityId = NetworkEntityIdMsg.From(_remotePlayer.EntityId),
			HostPosition = NetVector2Msg.From(_localPlayer.Position),
		});
		_log.LogInformation("PlayerJoin sent: local {Local} ({LocalId}), guest {Guest} ({GuestId}).",
			_localPlayer.SteamId, _localPlayer.EntityId, _remotePlayer.SteamId, _remotePlayer.EntityId);
		RemoteJoined?.Invoke(_remotePlayer);
	}

	private void OnPlayerJoin(ulong sender, PlayerJoinMsg msg)
	{
		if (Role != SessionRole.Guest || _remotePlayer is null)
		{
			return;
		}

		_localPlayer.EntityId = msg.GuestEntityId.ToNetworkEntityId();
		_remotePlayer.SteamId = msg.HostSteamId; // backfill (session already knows it)
		_remotePlayer.EntityId = msg.HostEntityId.ToNetworkEntityId();
		_remotePlayer.Position = msg.HostPosition.ToNetVector2();
		EntitySyncActive = true;
		_log.LogInformation("PlayerJoin received: local {Local}, host {Host} at {Position}.",
			_localPlayer.EntityId, _remotePlayer.EntityId, _remotePlayer.Position);
		RemoteJoined?.Invoke(_remotePlayer);
	}

	/// <summary>Guest → host: the guest's locally simulated state (host renders it, no host-side simulation).</summary>
	private void OnPlayerStateReport(ulong sender, PlayerStateReportMsg msg)
	{
		if (Role != SessionRole.Host || _remotePlayer is null)
		{
			return;
		}

		ApplyEntityState(msg.Entity, _remotePlayer);
		StateReceived?.Invoke(_remotePlayer);
	}

	private void OnPlayerState(ulong sender, PlayerStateMsg msg)
	{
		if (Role != SessionRole.Guest || _remotePlayer is null)
		{
			return;
		}

		foreach (var entity in msg.Entities)
		{
			var target = entity.Id.ToNetworkEntityId() == _localPlayer.EntityId ? _localPlayer : _remotePlayer;
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

	private void BroadcastPlayerState()
	{
		if (_remotePlayer is null)
		{
			return;
		}

		Send(PeerSteamId(), NetMsg.PlayerState, new PlayerStateMsg
		{
			Entities = [EntityStateMsg.From(_localPlayer), EntityStateMsg.From(_remotePlayer)],
		});
	}

	/// <summary>Guest side: broadcast the locally simulated state to the host (20 Hz).</summary>
	private void SendPlayerStateReport()
	{
		if (_remotePlayer is null)
		{
			return;
		}

		Send(_remotePlayer.SteamId, NetMsg.PlayerStateReport,
			new PlayerStateReportMsg { Entity = EntityStateMsg.From(_localPlayer) });
	}

	// ---- Ping / pong (diagnostics) ----

	private void OnPing(ulong sender, PingMsg msg) => Send(sender, NetMsg.Pong, new PongMsg { Ticks = msg.Ticks });

	private void OnPong(ulong sender, PongMsg msg) => LastRttMs = (DateTime.UtcNow.Ticks - msg.Ticks) / 10_000f;

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

		if (_steam.GetLobbyMembers().Length < 2)
		{
			_log.LogWarning("Peer left the lobby — ending session.");
			EndSession();
		}
	}

	private void EndEntitySync()
	{
		if (!EntitySyncActive)
		{
			return;
		}

		EntitySyncActive = false;
		_log.LogInformation("Entity sync ended.");
	}

	private void EndSession()
	{
		if (!SessionActive && _remotePlayer is null)
		{
			return;
		}

		EndEntitySync();
		_remotePlayer = null;
		SessionActive = false;
		HostSteamId = 0;
		Role = SessionRole.None;
		_log.LogInformation("Session ended.");
		SessionEnded?.Invoke();
	}

	// ---- Wire helpers ----

	private void OnMessage(ulong sender, byte[] frame)
	{
		if (frame.Length < 1)
		{
			return;
		}

		switch ((NetMsg)frame[0])
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
			case NetMsg.PlayerStateReport:
				OnPlayerStateReport(sender, NetPacket.DecodePayload<PlayerStateReportMsg>(frame));
				break;
			case NetMsg.PlayerState:
				OnPlayerState(sender, NetPacket.DecodePayload<PlayerStateMsg>(frame));
				break;
			case NetMsg.BlockDamaged:
				OnBlockDamaged(NetPacket.DecodePayload<BlockDamagedMsg>(frame));
				break;
		}
	}

	private void OnBlockDamaged(BlockDamagedMsg msg) => BlockDamagedReceived?.Invoke(msg.Position.ToNetVector2(), msg.Damage);

	private ulong PeerSteamId()
	{
		if (Role == SessionRole.Host)
		{
			return _remotePlayer?.SteamId ?? 0;
		}

		return HostSteamId;
	}

	private void Send(ulong steamId, NetMsg msg, object? payload = null)
	{
		if (steamId == 0)
		{
			return;
		}

		_transport.SendTo(steamId, NetPacket.Encode(msg, payload), reliable: true);
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
