using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Entity-sync domain: the entity table, id allocation, the sync decisions and
/// the configured state exchange (host broadcast + guest report, default
/// 20 Hz) with the join/leave
/// announcements. SessionService (control plane) owns the handshake and the
/// world/diagnostics surface; this service owns what the members ARE in the
/// world: entity buffers, entity ids, per-member sync state and the state
/// stream (throttling + snapshot seq, formerly EntitySyncStream, absorbed).
/// Reads session state through <see cref="ISessionControl"/> (resolved after
/// the session is built) instead of depending on SessionService itself —
/// acyclic constructor graph, abstract extraction (user rule).
/// </summary>
public sealed class EntitySyncService : ICuoService, IEntitySyncControl
{
	/// <summary>
	/// One member's entity-sync state. Host: one entry per synced guest. Guest:
	/// the host plus roster members (the host broadcasts the full entity list, so
	/// every side renders every member). Key = SteamId (stable across reconnects);
	/// EntityId is re-allocated per join. Presence in this table IS the member's
	/// sync state (host side: only synced members are tracked here).
	/// </summary>
	public sealed class SyncedEntity
	{
		public ulong SteamId;
		public PlayerEntity Entity = null!; // state buffer (render source / report target)
		public uint LastReportSeq; // host side: last applied PlayerStateReport seq
	}

	private readonly ISessionControl _session;

	private readonly PacketSender _sender;

	private readonly ITimeSource _time;

	private readonly IOptionsMonitor<StateStreamOptions> _stateStreamOptions;

	private readonly ILogger<EntitySyncService> _log;

	private readonly PlayerEntity _localPlayer;

	/// <summary>The local swing-presentation window (state belongs to its owner — this service owns the local player's presentation state).</summary>
	private readonly AttackSwingState _attackSwing = new();

	/// <summary>The rolling per-swing sequence (0 = never swung): every swing increments it so the peers' proxies replay the clip even for swings merged inside one held flag window (rapid mining swings).</summary>
	private byte _swingSeq;

	private readonly Dictionary<ulong, SyncedEntity> _entities = [];
	private ulong _epoch;
	private uint _nextEntityCounter;
	private bool _selfSyncActive; // guest: self sync state (host derives from the entity table)

	private long _nextStateSendMs;
	private long _nextReportSendMs;

	// Snapshot sequence for the unreliable state stream: the sender numbers
	// every broadcast/report, the receiver drops anything at or below the last
	// applied one (the unreliable channel can reorder and duplicate).
	private uint _nextStateSeq; // host: PlayerState broadcasts
	private uint _nextReportSeq; // guest: PlayerStateReport broadcasts

	public EntitySyncService(ISessionControl session, PacketSender sender, ITimeSource time,
		IOptionsMonitor<StateStreamOptions> stateStreamOptions, ILogger<EntitySyncService> log)
	{
		_session = session;

		_sender = sender;

		_time = time;

		_stateStreamOptions = stateStreamOptions;

		_log = log;
		_localPlayer = new PlayerEntity(session.LocalSteamId, default, isLocal: true);

		session.MemberRemoved += OnMemberRemoved;
		session.SessionEnded += OnSessionEnded;
	}

	// ---- Public surface (Game Adapter + Plugin HUD) ----

	public PlayerEntity LocalPlayer => _localPlayer;

	/// <summary>
	/// True while entity sync is active. Host: any member in the entity table
	/// (each member syncs independently); guest: the self-sync flag (set by the
	/// self PlayerJoin, cleared on leave). The Game Adapter renders per member
	/// and does not gate on this — it is informational (HUD).
	/// </summary>
	public bool EntitySyncActive => _session.Role == SessionRole.Host
		? _entities.Count > 0
		: _selfSyncActive;

	/// <summary>All synced remote entities (host: one per guest; guest: the host plus roster guests).</summary>
	public IEnumerable<PlayerEntity> RemotePlayers => _entities.Values.Select(m => m.Entity);

	/// <summary>Synced remote entity by SteamId, or null.</summary>
	public PlayerEntity? GetRemotePlayer(ulong steamId) =>
		_entities.TryGetValue(steamId, out var member) ? member.Entity : null;

	/// <summary>Host side: current authoritative state of the local body.</summary>
	public void PublishLocalState(NetVector2 position, NetVector2 lookPos, NetVector2 velocity,
		bool isRight, bool standing, bool alive, bool conscious, bool crouching,
		NetVector2? lookOverridePos = null, float lookOverrideTime = 0f, float eyeScareTime = 0f,
		float eyePanicTime = 0f, float eyeCloseTime = 0f,
		bool sitting = false, bool sleeping = false, bool climbing = false,
		byte workoutType = 0, byte napVariant = 0, float dogShakeIntensity = 0f,
		bool slidingLeft = false, bool slidingRight = false)
	{
		_localPlayer.Position = position;
		_localPlayer.LookPos = lookPos;
		_localPlayer.LookOverridePos = lookOverridePos;
		_localPlayer.LookOverrideTime = lookOverrideTime;
		_localPlayer.EyeScareTime = eyeScareTime;
		_localPlayer.EyePanicTime = eyePanicTime;
		_localPlayer.EyeCloseTime = eyeCloseTime;
		_localPlayer.Velocity = velocity;
		_localPlayer.IsRight = isRight;
		_localPlayer.Standing = standing;
		_localPlayer.Alive = alive;
		_localPlayer.Conscious = conscious;
		_localPlayer.Crouching = crouching;
		_localPlayer.Sitting = sitting;
		_localPlayer.Sleeping = sleeping;
		_localPlayer.Climbing = climbing;
		_localPlayer.WorkoutType = workoutType;
		_localPlayer.NapVariant = napVariant;
		_localPlayer.DogShakeIntensity = dogShakeIntensity;
		_localPlayer.SlidingLeft = slidingLeft;
		_localPlayer.SlidingRight = slidingRight;
		_localPlayer.IsAttacking = _attackSwing.IsAttacking;
		_localPlayer.SwingSeq = _swingSeq;
	}

	/// <summary>The local player swung (Body.Attack or Body.ThrowItem — both play ArmsSwing) — mark the swing so the peers' clones replay the animation via the IsAttacking snapshot flag and the per-swing sequence.</summary>
	public void MarkLocalAttackSwing()
	{
		ConfigureSwingHold();
		_attackSwing.MarkAttack(_time.NowMs);
		_swingSeq = (byte)(_swingSeq + 1); // wraps — the peer edge-detects the CHANGE, so a wrap reads as a new swing
	}

	/// <summary>Raised when a member's entity sync starts (host: that guest; guest: host or a roster member).</summary>
	public event Action<PlayerEntity>? RemoteJoined;

	/// <summary>Both sides: a state message refreshed the entity buffer (PlayerState on guest, PlayerStateReport on host).</summary>
	public event Action<PlayerEntity>? StateReceived;

	// ---- IEntitySyncControl (the packet handlers' control surface) ----

	uint IEntitySyncControl.LastStateSeq { get => LastStateSeq; set => LastStateSeq = value; }

	IEnumerable<SyncedEntity> IEntitySyncControl.Members => _entities.Values;

	bool IEntitySyncControl.TryGetSynced(ulong steamId, out SyncedEntity member) =>
		_entities.TryGetValue(steamId, out member!);

	void IEntitySyncControl.ApplyEntityState(EntityStateMsg msg, PlayerEntity target) => ApplyEntityState(msg, target);

	void IEntitySyncControl.FireStateReceived(PlayerEntity entity) => StateReceived?.Invoke(entity);

	void IEntitySyncControl.ProcessPlayerJoin(PlayerJoinMsg msg) => ProcessPlayerJoin(msg);

	void IEntitySyncControl.MaybeStartEntitySync() => MaybeStartEntitySync();

	void IEntitySyncControl.EndMemberSync(ulong steamId) => EndMemberSync(steamId);

	void IEntitySyncControl.EndEntitySync() => EndEntitySync();

	// ---- Internal surface for the packet handlers (Session/Handlers/) ----

	/// <summary>Guest side: last applied host snapshot seq (stream gate).</summary>
	internal uint LastStateSeq { get; set; }

	internal IEnumerable<SyncedEntity> Members => _entities.Values;

	internal bool TryGetSynced(ulong steamId, out SyncedEntity member) =>
		_entities.TryGetValue(steamId, out member!);

	internal void FireRemoteJoined(PlayerEntity entity) => RemoteJoined?.Invoke(entity);

	internal void FireStateReceived(PlayerEntity entity) => StateReceived?.Invoke(entity);

	/// <summary>Guest side: process a join announcement — self-activation (the host
	/// assigned our id) or a roster announcement (another member joined — upsert
	/// with its spawn anchor).</summary>
	internal void ProcessPlayerJoin(PlayerJoinMsg msg)
	{
		if (msg.GuestSteamId == _localPlayer.SteamId)
		{
			_localPlayer.EntityId = msg.GuestEntityId.ToNetworkEntityId();
			var hostEntity = UpsertEntity(msg.HostSteamId, msg.HostEntityId.ToNetworkEntityId());
			hostEntity.Position = msg.HostPosition.ToNetVector2();
			_session.GetOrCreateMember(msg.HostSteamId).InWorld = true; // the host is in the world with us
			_selfSyncActive = true;
			LastStateSeq = 0; // the host's snapshot sequence restarts with this join
			_log.LogInformation("PlayerJoin received: local {Local}, host {Host} at {Position}.",
				_localPlayer.EntityId, hostEntity.EntityId, hostEntity.Position);
			RemoteJoined?.Invoke(hostEntity);
			return;
		}

		var presence = _session.GetOrCreateMember(msg.GuestSteamId);
		presence.InWorld = true;
		presence.Handshaken = true;
		var entity = UpsertEntity(msg.GuestSteamId, msg.GuestEntityId.ToNetworkEntityId());
		entity.Position = msg.GuestPosition.ToNetVector2();
		_log.LogInformation("Roster join: member {Guest} ({GuestId}) at {Position}.",
			msg.GuestSteamId, entity.EntityId, entity.Position);
		RemoteJoined?.Invoke(entity);
	}

	/// <summary>Host side: start entity sync for every handshaken member that is in
	/// world (idempotent, retried every frame). Members sync independently — a
	/// mid-session joiner starts on its own.</summary>
	internal void MaybeStartEntitySync()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		foreach (var presence in _session.Members)
		{
			if (presence.Handshaken && !_entities.ContainsKey(presence.SteamId)
				&& _session.LocalInWorld && presence.InWorld)
			{
				StartMemberSync(presence);
			}
		}
	}

	/// <summary>Host side: end one member's entity sync (its stream stops, its entity is dropped).</summary>
	internal void EndMemberSync(ulong steamId)
	{
		if (_entities.Remove(steamId))
		{
			_log.LogInformation("Entity sync ended for {Member}.", steamId);
		}
	}

	/// <summary>End entity sync for every member (host) or the self sync (guest).</summary>
	internal void EndEntitySync()
	{
		_attackSwing.Reset(); // the swing cannot outlive the world (either role, either branch)
		_swingSeq = 0; // same for the sequence — a stale sequence would replay a clip on the next world entry
		if (_session.Role == SessionRole.Host)
		{
			if (_entities.Count == 0)
			{
				return;
			}

			_entities.Clear();
			_log.LogInformation("Entity sync ended for all members.");
			return;
		}

		if (!_selfSyncActive)
		{
			return;
		}

		_selfSyncActive = false;
		_log.LogInformation("Entity sync ended.");
	}

	/// <summary>Applies a decoded entity state, preserving the first-snapshot rule
	/// (Prev = current on the first report — the buffer defaults are (0,0) and
	/// interpolating from them would slide the proxy in from the world origin).</summary>
	internal void ApplyEntityState(EntityStateMsg msg, PlayerEntity target)
	{
		var firstSnapshot = target.StateReceivedMs < 0;
		var now = (int)_time.NowMs; // the interpolation buffer's ms stamps (GameAdapter's SessionStatePump diffs them against Environment.TickCount)
		target.PrevStateMs = firstSnapshot ? now : target.StateReceivedMs;
		target.PrevPosition = firstSnapshot ? msg.Position.ToNetVector2() : target.Position;
		target.PrevLookPos = firstSnapshot ? msg.LookPos.ToNetVector2() : target.LookPos;
		target.PrevVelocity = firstSnapshot ? msg.Velocity.ToNetVector2() : target.Velocity;
		msg.ApplyTo(target);
		target.StateReceivedMs = now;
	}

	// ---- Lifecycle ----

	void ICuoService.Initialize()
	{
		_epoch = (ulong)_time.UtcNowTicks;
		// Steam API init (SteamService.Initialize) runs before us in registration
		// order — refresh the local SteamID captured at construction time (it was
		// still 0 then, and join/host messages carry it).
		_localPlayer.SteamId = _session.LocalSteamId;
	}

	void ICuoService.Start()
	{
	}

	/// <summary>
	/// Keep the attack-swing flag held for six stream ticks (never shorter than
	/// the 300 ms clip) at the currently configured cadence.
	/// </summary>
	private void ConfigureSwingHold()
	{
		var intervalMs = Math.Max(1L, (long)(_stateStreamOptions.CurrentValue.SendIntervalSeconds * 1000f));
		_attackSwing.SetStreamHoldMs(intervalMs * 6);
	}

	void ICuoService.Update()
	{
		// Steam init may have succeeded on a LATER retry (the F8 path after a
		// startup init failure) — the local entity's SteamId was snapshotted
		// from the session at Initialize and would otherwise stay 0, so the
		// self-activation PlayerJoin never matches and the host's state stream
		// is dropped as "no member with that entity id" (observed 2026-08-15
		// in the sandbox guest whose Steam client started after the plugin).
		if (_localPlayer.SteamId != _session.LocalSteamId)
		{
			_localPlayer.SteamId = _session.LocalSteamId;
			_log.LogInformation("Entity sync local SteamId refreshed to {SteamId} (late Steam init).", _localPlayer.SteamId);
		}

		// The swing window ticks unconditionally — even a solo swing (no sync
		// active) must expire so a later lobby open never re-sends a stale flag.
		// Its hold follows the configured stream cadence (six ticks, at least the
		// clip span) so the rising edge survives drops at any frequency.
		ConfigureSwingHold();
		_attackSwing.Tick(_time.NowMs);

		// Either side leaving the world ends the sync: the peer renders our clone
		// from state we no longer publish. The SceneState message we reported
		// already told the peer — its own self-check (this same branch) ends its
		// side, so both stop the stream without waiting for a round-trip.
		if (EntitySyncActive && !_session.LocalInWorld)
		{
			EndEntitySync();
		}

		// Opportunistic entity-sync start: retried every frame (cheap, idempotent)
		// instead of only on message arrival — the local InWorld flag is set by
		// the Game Adapter's Update, which may run after a peer's InWorld message
		// was already processed, and the sync would otherwise never start. Runs
		// unconditionally (per member, each starts on its own conditions).
		if (_session.Role == SessionRole.Host)
		{
			MaybeStartEntitySync();
		}

		var intervalSeconds = _stateStreamOptions.CurrentValue.SendIntervalSeconds;
		var nowMs = _time.NowMs;
		if (_session.Role == SessionRole.Host && EntitySyncActive && nowMs >= _nextStateSendMs)
		{
			_nextStateSendMs = nowMs + (long)(intervalSeconds * 1000f);
			BroadcastPlayerState();
		}

		if (_session.Role == SessionRole.Guest && EntitySyncActive && nowMs >= _nextReportSendMs)
		{
			_nextReportSendMs = nowMs + (long)(intervalSeconds * 1000f);
			SendPlayerStateReport();
		}
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
		_session.MemberRemoved -= OnMemberRemoved;
		_session.SessionEnded -= OnSessionEnded;
	}

	// ---- Member lifecycle ----

	/// <summary>Host side: a member left the session — drop its entity and tell the
	/// other guests (PlayerLeave, entity id included); the render clones follow via
	/// RemoteSceneChanged. Guest side: drop a roster member (no broadcast — only
	/// the host fans out).</summary>
	private void OnMemberRemoved(ulong steamId)
	{
		if (!_entities.TryGetValue(steamId, out var member))
		{
			return;
		}

		_entities.Remove(steamId);
		if (_session.Role == SessionRole.Host)
		{
			BroadcastExcept(steamId, NetMsg.PlayerLeave, new PlayerLeaveMsg
			{
				SteamId = steamId,
				EntityId = member.Entity.EntityId.ToNetworkEntityIdMsg(),
			});
		}

		_session.FireRemoteSceneChanged(steamId, false);
	}

	/// <summary>Bulk teardown: the session ended (all members gone / host gone) —
	/// drop every entity and the self-sync flag. Per-member removals during the
	/// session go through <see cref="OnMemberRemoved"/> instead.</summary>
	private void OnSessionEnded()
	{
		_entities.Clear();
		_selfSyncActive = false;
		_attackSwing.Reset();
		_swingSeq = 0;
	}

	// ---- Sync decisions ----

	/// <summary>Host side: allocate ids, register the member and announce the join
	/// (self-activation + roster backfill + roster announce), then push the first
	/// snapshot so the clone renders immediately instead of waiting up to one
	/// 20 Hz tick for the next broadcast (same mechanism serves
	/// respawn/reconnect).</summary>
	private void StartMemberSync(MemberPresenceTable.MemberPresence presence)
	{
		var entity = new PlayerEntity(presence.SteamId, default, isLocal: false)
		{
			EntityId = AllocateEntityId(),
		};
		_entities[presence.SteamId] = new SyncedEntity
		{
			SteamId = presence.SteamId,
			Entity = entity,
			LastReportSeq = 0, // the member re-joins with a fresh sequence space
		};
		RemoteJoined?.Invoke(entity);

		var joinMsg = BuildJoinMsg(presence.SteamId, entity.EntityId, presence.ReportedSpawnPos);
		_sender.Send(presence.SteamId, NetMsg.PlayerJoin, joinMsg); // self-activation
		_log.LogInformation("PlayerJoin sent: local {Local} ({LocalId}), member {Guest} ({GuestId}).",
			_localPlayer.SteamId, _localPlayer.EntityId, presence.SteamId, entity.EntityId);

		// Roster backfill: the newcomer also needs every already-synced member —
		// their PlayerJoin predates it, and state messages only update existing
		// entity buffers, so without this it never learns they exist.
		foreach (var existing in _entities.Values)
		{
			if (existing.SteamId == presence.SteamId)
			{
				continue;
			}

			var existingPresence = _session.GetOrCreateMember(existing.SteamId);
			_sender.Send(presence.SteamId, NetMsg.PlayerJoin,
				BuildJoinMsg(existing.SteamId, existing.Entity.EntityId, existingPresence.ReportedSpawnPos));
		}

		BroadcastExcept(presence.SteamId, NetMsg.PlayerJoin, joinMsg); // roster: announce to the others
		BroadcastPlayerState();
	}

	private PlayerJoinMsg BuildJoinMsg(ulong guestSteamId, NetworkEntityId guestId, NetVector2 guestPosition)
	{
		var displayName = _session.TryGetMember(guestSteamId, out var presence)
			? presence.DisplayName
			: "";
		return new PlayerJoinMsg
		{
			HostSteamId = _localPlayer.SteamId,
			HostEntityId = _localPlayer.EntityId.ToNetworkEntityIdMsg(),
			HostPosition = _localPlayer.Position.ToNetVector2Msg(),
			GuestSteamId = guestSteamId,
			GuestEntityId = guestId.ToNetworkEntityIdMsg(),
			GuestPosition = guestPosition.ToNetVector2Msg(),
			DisplayName = displayName,
		};
	}

	// ---- State stream ----

	/// <summary>Host side: broadcast the authoritative snapshot (local + every synced member) to all synced members.</summary>
	private void BroadcastPlayerState()
	{
		var synced = _entities.Values.ToList();
		if (synced.Count == 0)
		{
			return;
		}

		var payload = new PlayerStateMsg
		{
			Seq = ++_nextStateSeq,
			Entities = BuildEntityList(synced),
		};
		_sender.SendToAll(synced.Select(m => m.SteamId), NetMsg.PlayerState, payload, reliable: false);
	}

	private List<EntityStateMsg> BuildEntityList(List<SyncedEntity> synced)
	{
		var list = new List<EntityStateMsg>(synced.Count + 1) { _localPlayer.ToEntityStateMsg() };
		foreach (var member in synced)
		{
			list.Add(member.Entity.ToEntityStateMsg());
		}

		return list;
	}

	/// <summary>Guest side: report the locally simulated state to the host (20 Hz).</summary>
	private void SendPlayerStateReport()
	{
		if (_session.HostSteamId == 0)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.PlayerStateReport,
			new PlayerStateReportMsg
			{
				Seq = ++_nextReportSeq,
				Entity = _localPlayer.ToEntityStateMsg(),
			}, reliable: false);
	}

	/// <summary>Upsert a remote entity buffer (id updated on rejoin; the buffer is
	/// rebuilt, which is fine — a rejoin is a fresh sync).</summary>
	private PlayerEntity UpsertEntity(ulong steamId, NetworkEntityId entityId)
	{
		if (!_entities.TryGetValue(steamId, out var member))
		{
			member = new SyncedEntity
			{
				SteamId = steamId,
				Entity = new PlayerEntity(steamId, entityId, isLocal: false),
			};
			_entities[steamId] = member;
		}

		member.Entity.EntityId = entityId;
		return member.Entity;
	}

	private NetworkEntityId AllocateEntityId()
	{
		if (_localPlayer.EntityId.Counter == 0 && _localPlayer.EntityId.Epoch == 0)
		{
			_localPlayer.EntityId = new NetworkEntityId(_epoch, _nextEntityCounter++, generation: 0);
		}

		return new NetworkEntityId(_epoch, _nextEntityCounter++, generation: 0);
	}

	/// <summary>Broadcast to every synced member except one — relay semantics: the source already applied the change locally.</summary>
	private void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload)
	{
		foreach (var member in _entities.Values)
		{
			if (member.SteamId != excludeSteamId)
			{
				_sender.Send(member.SteamId, msg, payload);
			}
		}
	}
}
