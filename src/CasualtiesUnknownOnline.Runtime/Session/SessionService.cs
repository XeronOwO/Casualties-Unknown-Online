using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
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
/// exchange → entity sync (host-authoritative, architecture.md §3). Owns the
/// wire protocol dispatch on top of SteamTransport and the player entity table.
/// Phase 1 supports a single guest (first peer in the lobby).
/// </summary>
public sealed class SessionService : ICuoService
{
	private const float StateSendInterval = 0.05f; // 20 Hz authoritative snapshot
	private const float InputSendInterval = 0.05f; // 20 Hz guest input
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

	private NetVector2 _pendingMoveDir;
	private NetVector2 _pendingLookPos;
	private bool _pendingJump;
	private bool _pendingCrouch;
	private long _nextStateSendMs;
	private long _nextInputSendMs;
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

	/// <summary>Both sides: a PlayerState batch refreshed the local entity buffer.</summary>
	public event Action<PlayerEntity>? StateReceived;

	/// <summary>Host side: fresh input for the guest's clone was buffered.</summary>
	public event Action<PlayerEntity>? InputReceived;

	// ---- Local state submission (Game Adapter → session) ----

	/// <summary>Host side: current authoritative state of the local body.</summary>
	public void PublishLocalState(NetVector2 position, NetVector2 lookPos, NetVector2 velocity,
		bool isRight, bool standing, bool alive, bool conscious, bool crouching)
	{
		_localPlayer.Position = position;
		_localPlayer.LookPos = lookPos;
		_localPlayer.Velocity = velocity;
		_localPlayer.IsRight = isRight;
		_localPlayer.Standing = standing;
		_localPlayer.Alive = alive;
		_localPlayer.Conscious = conscious;
		_localPlayer.Crouching = crouching;
	}

	/// <summary>Host side: state of the guest's simulated clone.</summary>
	public void PublishRemoteState(PlayerEntity remote, NetVector2 position, NetVector2 lookPos, NetVector2 velocity,
		bool isRight, bool standing, bool alive, bool conscious, bool crouching)
	{
		remote.Position = position;
		remote.LookPos = lookPos;
		remote.Velocity = velocity;
		remote.IsRight = isRight;
		remote.Standing = standing;
		remote.Alive = alive;
		remote.Conscious = conscious;
		remote.Crouching = crouching;
	}

	/// <summary>Guest side: submit local input (direction + look target + one-shot jump).</summary>
	public void SubmitLocalInput(NetVector2 moveDir, NetVector2 lookPos, bool jump, bool crouching)
	{
		_pendingMoveDir = moveDir;
		_pendingLookPos = lookPos;
		_pendingJump |= jump;
		_pendingCrouch = crouching;
	}

	/// <summary>Either side: report the local scene state (menu / in world).</summary>
	public void ReportSceneState(SceneStateType state, string sceneName)
	{
		_localPlayer.InWorld = state == SceneStateType.InWorld;
		if (SessionActive)
		{
			Send(PeerSteamId(), NetMsg.SceneState, w => WriteSceneState(w, state, sceneName));
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

		Send(PeerSteamId(), NetMsg.WorldStartParams, w => WriteWorldParams(w, parameters));
		_log.LogInformation("Published world params ({StateBytes} bytes) to {Peer}",
			parameters.RandomState.Length, PeerSteamId());
	}

	/// <summary>Diagnostics: ping the peer (RTT recorded in <see cref="LastRttMs"/>).</summary>
	public void RequestPing() => Send(PeerSteamId(), NetMsg.Ping, w => w.Write(DateTime.UtcNow.Ticks));

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

		if (Role == SessionRole.Guest && EntitySyncActive && nowMs >= _nextInputSendMs)
		{
			_nextInputSendMs = nowMs + (long)(InputSendInterval * 1000f);
			SendPlayerInput();
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
		Send(HostSteamId, NetMsg.Handshake, w =>
		{
			w.Write(ProtocolVersion.Current);
			WriteSceneState(w, SceneStateForLocal());
		});
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
		Send(HostSteamId, NetMsg.Handshake, w =>
		{
			w.Write(ProtocolVersion.Current);
			WriteSceneState(w, SceneStateForLocal());
		});
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
			Send(peer, NetMsg.Ping, w => w.Write(DateTime.UtcNow.Ticks));
		}
	}

	private void OnHandshake(ulong sender, BinaryReader reader)
	{
		if (Role != SessionRole.Host)
		{
			return;
		}

		var protocol = reader.ReadInt32();
		ReadSceneState(reader, out var peerState, out _);
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
		Send(sender, NetMsg.HandshakeAck, w =>
		{
			w.Write(ProtocolVersion.Current);
			WriteSceneState(w, SceneStateForLocal());
			w.Write(_worldParams is not null);
		});
		if (_worldParams is not null)
		{
			Send(sender, NetMsg.WorldStartParams, w => WriteWorldParams(w, _worldParams));
		}
	}

	private void OnHandshakeAck(ulong sender, BinaryReader reader)
	{
		var protocol = reader.ReadInt32();
		ReadSceneState(reader, out var hostState, out _);
		_ = reader.ReadBoolean(); // host has world params — they arrive as their own message
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

	private void OnSceneState(ulong sender, BinaryReader reader)
	{
		ReadSceneState(reader, out var state, out var sceneName);
		if (_remotePlayer is null)
		{
			return;
		}

		var wasInWorld = _remotePlayer.InWorld;
		_remotePlayer.InWorld = state == SceneStateType.InWorld;
		_log.LogInformation("Peer {Peer} scene state: {State} ({SceneName})", sender, state, sceneName);
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

	private void OnWorldStartParams(ulong sender, BinaryReader reader)
	{
		var parameters = ReadWorldParams(reader);
		if (parameters is null)
		{
			return;
		}

		_worldParams = parameters;
		_log.LogInformation("Received world params ({StateBytes} bytes, loaded run: {LoadedRun}).",
			parameters.RandomState.Length, parameters.LoadedRun);
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
		Send(_remotePlayer.SteamId, NetMsg.PlayerJoin, w =>
		{
			w.Write(_localPlayer.SteamId);
			WriteEntityId(w, _localPlayer.EntityId);
			WriteEntityId(w, _remotePlayer.EntityId);
			NetPacket.WriteVector2(w, _localPlayer.Position);
		});
		_log.LogInformation("PlayerJoin sent: local {Local} ({LocalId}), guest {Guest} ({GuestId}).",
			_localPlayer.SteamId, _localPlayer.EntityId, _remotePlayer.SteamId, _remotePlayer.EntityId);
		RemoteJoined?.Invoke(_remotePlayer);
	}

	private void OnPlayerJoin(ulong sender, BinaryReader reader)
	{
		if (Role != SessionRole.Guest || _remotePlayer is null)
		{
			return;
		}

		// [hostSteamId][hostEntityId][guestEntityId][hostPosition]
		var hostSteamId = reader.ReadUInt64();
		var hostEntityId = ReadEntityId(reader);
		var guestEntityId = ReadEntityId(reader);
		var hostPosition = NetPacket.ReadVector2(reader);

		_localPlayer.EntityId = guestEntityId;
		_remotePlayer.SteamId = hostSteamId; // backfill (session already knows it)
		_remotePlayer.EntityId = hostEntityId;
		_remotePlayer.Position = hostPosition;
		EntitySyncActive = true;
		_log.LogInformation("PlayerJoin received: local {Local}, host {Host} at {Position}.",
			_localPlayer.EntityId, hostEntityId, hostPosition);
		RemoteJoined?.Invoke(_remotePlayer);
	}

	private void OnPlayerInput(ulong sender, BinaryReader reader)
	{
		if (Role != SessionRole.Host || _remotePlayer is null)
		{
			return;
		}

		var flags = reader.ReadByte();
		_remotePlayer.MoveDir = NetPacket.ReadVector2(reader);
		_remotePlayer.LookInput = NetPacket.ReadVector2(reader);
		_remotePlayer.JumpQueued = (flags & 0x01) != 0;
		_remotePlayer.Crouching = (flags & 0x02) != 0;
		InputReceived?.Invoke(_remotePlayer);
	}

	private void OnPlayerState(ulong sender, BinaryReader reader)
	{
		if (Role != SessionRole.Guest || _remotePlayer is null)
		{
			return;
		}

		var count = reader.ReadByte();
		for (var i = 0; i < count; i++)
		{
			var entityId = ReadEntityId(reader);
			var position = NetPacket.ReadVector2(reader);
			var lookPos = NetPacket.ReadVector2(reader);
			var velocity = NetPacket.ReadVector2(reader);
			var flags = reader.ReadByte();

			var target = entityId == _localPlayer.EntityId ? _localPlayer : _remotePlayer;
			// Keep the previous snapshot for render interpolation on the guest.
			target.PrevPosition = target.Position;
			target.PrevLookPos = target.LookPos;
			target.PrevVelocity = target.Velocity;
			target.Position = position;
			target.LookPos = lookPos;
			target.Velocity = velocity;
			target.IsRight = (flags & 0x01) != 0;
			target.Standing = (flags & 0x02) != 0;
			target.Alive = (flags & 0x04) != 0;
			target.Conscious = (flags & 0x08) != 0;
			target.Crouching = (flags & 0x10) != 0;
			target.StateReceivedMs = Environment.TickCount;
		}
		StateReceived?.Invoke(_localPlayer);
	}

	private void BroadcastPlayerState()
	{
		if (_remotePlayer is null)
		{
			return;
		}

		Send(PeerSteamId(), NetMsg.PlayerState, w =>
		{
			w.Write((byte)2);
			WriteEntity(w, _localPlayer);
			WriteEntity(w, _remotePlayer);
		});
	}

	private void SendPlayerInput()
	{
		if (_remotePlayer is null)
		{
			return;
		}

		var flags = (byte)((_pendingJump ? 0x01 : 0) | (_pendingCrouch ? 0x02 : 0));
		_pendingJump = false;
		Send(_remotePlayer.SteamId, NetMsg.PlayerInput, w =>
		{
			w.Write(flags);
			NetPacket.WriteVector2(w, _pendingMoveDir);
			NetPacket.WriteVector2(w, _pendingLookPos);
		});
	}

	// ---- Ping / pong (diagnostics) ----

	private void OnPing(ulong sender, BinaryReader reader)
	{
		var ticks = reader.ReadInt64();
		Send(sender, NetMsg.Pong, w => w.Write(ticks));
	}

	private void OnPong(ulong sender, BinaryReader reader)
	{
		var sentTicks = reader.ReadInt64();
		LastRttMs = (DateTime.UtcNow.Ticks - sentTicks) / 10_000f;
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

		using var stream = new MemoryStream(frame, 1, frame.Length - 1);
		using var reader = new BinaryReader(stream, Encoding.UTF8);
		switch ((NetMsg)frame[0])
		{
			case NetMsg.Ping:
				OnPing(sender, reader);
				break;
			case NetMsg.Pong:
				OnPong(sender, reader);
				break;
			case NetMsg.Handshake:
				OnHandshake(sender, reader);
				break;
			case NetMsg.HandshakeAck:
				OnHandshakeAck(sender, reader);
				break;
			case NetMsg.SceneState:
				OnSceneState(sender, reader);
				break;
			case NetMsg.WorldStartParams:
				OnWorldStartParams(sender, reader);
				break;
			case NetMsg.PlayerJoin:
				OnPlayerJoin(sender, reader);
				break;
			case NetMsg.PlayerInput:
				OnPlayerInput(sender, reader);
				break;
			case NetMsg.PlayerState:
				OnPlayerState(sender, reader);
				break;
		}
	}

	private ulong PeerSteamId()
	{
		if (Role == SessionRole.Host)
		{
			return _remotePlayer?.SteamId ?? 0;
		}

		return HostSteamId;
	}

	private void Send(ulong steamId, NetMsg msg, Action<BinaryWriter>? writePayload = null)
	{
		if (steamId == 0)
		{
			return;
		}

		_transport.SendTo(steamId, NetPacket.Encode(msg, writePayload), reliable: true);
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

	private static void WriteSceneState(BinaryWriter w, SceneStateType state, string? sceneName = null)
	{
		w.Write((byte)state);
		w.Write(sceneName ?? "");
	}

	private static void ReadSceneState(BinaryReader r, out SceneStateType state, out string sceneName)
	{
		state = (SceneStateType)r.ReadByte();
		sceneName = r.ReadString();
	}

	private static void WriteEntityId(BinaryWriter w, NetworkEntityId id)
	{
		w.Write(id.Epoch);
		w.Write(id.Counter);
		w.Write(id.Generation);
	}

	private static NetworkEntityId ReadEntityId(BinaryReader r) => new(r.ReadUInt64(), r.ReadUInt32(), r.ReadByte());

	private static void WriteEntity(BinaryWriter w, PlayerEntity entity)
	{
		WriteEntityId(w, entity.EntityId);
		NetPacket.WriteVector2(w, entity.Position);
		NetPacket.WriteVector2(w, entity.LookPos);
		NetPacket.WriteVector2(w, entity.Velocity);
		var flags = (byte)(
			(entity.IsRight ? 0x01 : 0) | (entity.Standing ? 0x02 : 0) |
			(entity.Alive ? 0x04 : 0) | (entity.Conscious ? 0x08 : 0) | (entity.Crouching ? 0x10 : 0));
		w.Write(flags);
	}

	private static void WriteWorldParams(BinaryWriter w, WorldStartParams p)
	{
		w.Write(p.RandomState.Length);
		w.Write(p.RandomState);
		w.Write(p.BiomeOverride);
		w.Write(p.BiomeDepth);
		w.Write(p.TotalTraveled);
		w.Write(p.LoadedRun);
		WriteRunSettings(w, p.RunSettings);
	}

	private static WorldStartParams? ReadWorldParams(BinaryReader reader)
	{
		try
		{
			var state = reader.ReadBytes(reader.ReadInt32());
			return new WorldStartParams
			{
				RandomState = state,
				BiomeOverride = reader.ReadByte(),
				BiomeDepth = reader.ReadByte(),
				TotalTraveled = reader.ReadInt32(),
				LoadedRun = reader.ReadBoolean(),
				RunSettings = ReadRunSettings(reader),
			};
		}
		catch (EndOfStreamException)
		{
			return null;
		}
	}

	private static void WriteRunSettings(BinaryWriter w, Dictionary<string, object>? settings)
	{
		if (settings is null)
		{
			w.Write(0);
			return;
		}
		w.Write(settings.Count);
		foreach (var setting in settings)
		{
			w.Write(setting.Key);
			switch (setting.Value)
			{
				case int i:
					w.Write((byte)1);
					w.Write(i);
					break;
				case float f:
					w.Write((byte)2);
					w.Write(f);
					break;
				case bool b:
					w.Write((byte)3);
					w.Write(b);
					break;
				case string s:
					w.Write((byte)4);
					w.Write(s);
					break;
				default:
					w.Write((byte)0);
					break;
			}
		}
	}

	private static Dictionary<string, object>? ReadRunSettings(BinaryReader r)
	{
		var count = r.ReadInt32();
		if (count == 0)
		{
			return null;
		}

		var settings = new Dictionary<string, object>(count);
		for (var i = 0; i < count; i++)
		{
			var key = r.ReadString();
			switch (r.ReadByte())
			{
				case 1:
					settings[key] = r.ReadInt32();
					break;
				case 2:
					settings[key] = r.ReadSingle();
					break;
				case 3:
					settings[key] = r.ReadBoolean();
					break;
				case 4:
					settings[key] = r.ReadString();
					break;
			}
		}
		return settings;
	}
}
