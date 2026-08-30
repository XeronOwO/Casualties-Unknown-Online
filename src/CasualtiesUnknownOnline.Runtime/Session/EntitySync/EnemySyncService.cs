using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Enemy-sync domain (host-authoritative, reusing the player entity-stream
/// pattern): the host simulates the enemies (AI + physics) and publishes their
/// presentation state here; this service broadcasts it at the configured
/// cadence (default 20 Hz; unreliable,
/// seq-gated) and fans out a full snapshot on member world-entry. The guest
/// receives into the same buffer and raises <see cref="EnemyStateReceived"/>
/// for the Game Adapter to drive its frozen render copies. Only the
/// presentation subset travels — position / velocity / rotation / health + a
/// few animation flags — never the AI internal state.
/// </summary>
public sealed class EnemySyncService : ICuoService, IEnemySyncControl
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ITimeSource _time;

	private readonly IOptionsMonitor<StateStreamOptions> _stateStreamOptions;

	private readonly ILogger<EnemySyncService> _log;

	private readonly EnemyKernelProjection _enemyKernel;
	private readonly EnemyKernelRestoreProjection _enemyKernelRestore;
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly IKernelProtocolControl _kernelProtocol;
	private readonly EnemyCombatKernelSubmitter _enemyCombatKernel;
	private readonly EnemyCombatKernelProjection _enemyCombatProjection;
	private readonly Dictionary<NetworkEntityId, ulong> _terminalHealthRevision = [];

	private readonly Dictionary<NetworkEntityId, EnemyEntity> _enemies = [];
	private readonly HashSet<NetworkEntityId> _removedEnemies = [];
	private IReadOnlyList<EnemySpawnEntryMsg> _runtimeSpawns = [];
	private uint _nextEnemySeq; // host: EnemyState broadcast seq
	private long _nextStateSendMs;
	private uint _lastEnemyStateSeq; // guest: last applied seq (the unreliable-stream gate)
	private ulong _epoch; // host: the enemy-id epoch (set on Initialize)
	private uint _nextEnemyCounter; // host: enemy-id allocation counter

	public EnemySyncService(ISessionControl session, PacketSender sender, ITimeSource time,
		IOptionsMonitor<StateStreamOptions> stateStreamOptions, ILogger<EnemySyncService> log,
		EnemyKernelProjection enemyKernel, EnemyKernelRestoreProjection enemyKernelRestore,
		ItemKernelAuthority kernelAuthority, IKernelProtocolControl kernelProtocol,
		ICharacterDataControl characterData)
	{
		_session = session;
		_sender = sender;
		_time = time;
		_stateStreamOptions = stateStreamOptions;
		_log = log;
		_enemyKernel = enemyKernel;
		_enemyKernelRestore = enemyKernelRestore;
		_kernelAuthority = kernelAuthority;
		_kernelProtocol = kernelProtocol;
		_enemyCombatKernel = new EnemyCombatKernelSubmitter(session, kernelAuthority, kernelProtocol, log);
		_enemyCombatProjection = new EnemyCombatKernelProjection(kernelAuthority, this, characterData, session, log);
		_kernelProtocol.EntityStateStreamReceived += OnEntityStateStreamReceived;
		session.SessionEnded += OnSessionEnded;
		_kernelAuthority.BatchCommitted += OnKernelBatchCommitted;
		_kernelAuthority.BatchApplied += OnKernelBatchApplied;
		_kernelAuthority.CheckpointRestored += OnKernelCheckpointRestored;
	}

	/// <summary>Raised after the full world-entry snapshot is applied (the Game Adapter binds its local enemy copies to the host's ids on this).</summary>
	public event Action? EnemySnapshotReceived;

	/// <summary>Raised after a state-stream batch is applied (the Game Adapter drives the frozen render copies from the buffered state on this).</summary>
	public event Action? EnemyStateReceived;

	/// <summary>Raised after an explicit enemy aggregate removal is applied (the Game Adapter destroys the frozen copy).</summary>
	public event Action<NetworkEntityId>? EnemyRemovedReceived;

	/// <summary>Raised when an enemy bite result is projected from the kernel — the Game Adapter applies the post-bite limb/body state to the victim's clone (source victim excluded).</summary>
	public event Action<ulong, EnemyBiteMsg>? EnemyBiteReceived;

	/// <summary>Raised on the victim's side when a host-ordered enemy attack arrives — the Game Adapter applies it to the local body and reports the terminal state.</summary>
	public event Action<EnemyAttackMsg>? EnemyAttackReceived;

	/// <summary>Raised when a crystal-lunge result is projected from the kernel — the Game Adapter applies the post-lunge limb/body state to the victim's clone (source victim excluded).</summary>
	public event Action<ulong, EnemyLungeMsg>? EnemyLungeReceived;

	/// <summary>Raised when an enemy-proximity side-effect result is projected from the kernel — the Game Adapter applies the post-effect body state to the victim's clone (source victim excluded).</summary>
	public event Action<ulong, EnemyEffectMsg>? EnemyEffectReceived;

	// ---- Public surface (Game Adapter) ----

	/// <summary>All enemy buffers (host: authoritative; guest: received).</summary>
	public IEnumerable<EnemyEntity> Enemies => _enemies.Values;

	/// <summary>Runtime-spawn facts carried by the last applied enemy snapshot (guest) or published by the host (its authoritative set).</summary>
	public IReadOnlyList<EnemySpawnEntryMsg> RuntimeSpawns => _runtimeSpawns;

	public EnemyEntity? GetEnemy(NetworkEntityId id) =>
		_enemies.TryGetValue(id, out var entity) ? entity : null;

	/// <summary>Host side: allocate the next enemy id (the host assigns ids in the deterministic EnemySpawnArbitration order).</summary>
	public NetworkEntityId AllocateEnemyId() => new(_epoch, _nextEnemyCounter++, 0);

	/// <summary>
	/// Report an enemy bite: the victim's local body already applied the bite.
	/// Host reports commit a journal-only kernel command directly; guest reports
	/// ride the Phase C command envelope to the host. The committed batch is
	/// projected back through <see cref="EnemyCombatKernelProjection"/> as the
	/// post-bite presentation event on every peer except the source victim.
	/// </summary>
	public void SendEnemyBite(EnemyBiteMsg msg) => _enemyCombatKernel.SendEnemyBite(msg);

	/// <summary>An enemy bite arrived (report or relay) — surface it for the Game Adapter to apply.</summary>
	public void FireEnemyBiteReceived(ulong sender, EnemyBiteMsg msg)
		=> EnemyBiteReceived?.Invoke(sender, msg);

	/// <summary>
	/// Host side: order one member to apply an enemy attack locally. The remote
	/// clone has no colliders, so the host's own collision callback can never
	/// reach the guest — the host simulation decides, the victim applies and
	/// reports the terminal state. Reliable: the command is one-shot.
	/// </summary>
	public void SendEnemyAttack(EnemyAttackMsg msg)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		var target = _session.Members.FirstOrDefault(m =>
			m.SteamId == msg.VictimSteamId && m.Handshaken && m.InWorld);
		if (target == null)
		{
			_log.LogWarning("[EnemyAttack] victim {Victim} is not an in-world member — command dropped.", msg.VictimSteamId);
			return;
		}

		_sender.Send(msg.VictimSteamId, NetMsg.EnemyAttack, msg, reliable: true);
	}

	/// <summary>A host-ordered enemy attack arrived at the victim — surface it for the Game Adapter to apply locally.</summary>
	public void FireEnemyAttackReceived(EnemyAttackMsg msg) => EnemyAttackReceived?.Invoke(msg);

	/// <summary>
	/// Report a crystal-lunge terminal state: the victim's local body already
	/// applied the lunge. Host reports commit a journal-only kernel command
	/// directly; guest reports ride the Phase C command envelope to the host.
	/// </summary>
	public void SendEnemyLunge(EnemyLungeMsg msg) => _enemyCombatKernel.SendEnemyLunge(msg);

	/// <summary>A crystal-lunge terminal state arrived (report or relay) — surface it for the Game Adapter to apply.</summary>
	public void FireEnemyLungeReceived(ulong sender, EnemyLungeMsg msg)
		=> EnemyLungeReceived?.Invoke(sender, msg);

	/// <summary>
	/// Report an enemy-proximity side effect (ElderThornback horror, Xaloris
	/// septic tick, GrabberPlant grab): the affected player's local body already
	/// applied the effect. Host reports commit a journal-only kernel command
	/// directly; guest reports ride the Phase C command envelope to the host.
	/// </summary>
	public void SendEnemyEffect(EnemyEffectMsg msg) => _enemyCombatKernel.SendEnemyEffect(msg);

	/// <summary>An enemy-proximity side effect arrived (report or relay) — surface it for the Game Adapter to apply.</summary>
	public void FireEnemyEffectReceived(ulong sender, EnemyEffectMsg msg)
		=> EnemyEffectReceived?.Invoke(sender, msg);

	/// <summary>Host side: publish the authoritative enemy states (the Game Adapter captures the simulated enemies and overwrites the buffer each tick).</summary>
	public void PublishEnemyStates(IEnumerable<EnemyEntity> states)
	{
		var published = states.ToList();
		var publishedIds = published.Select(e => e.EntityId).ToHashSet();
		var removed = _enemies.Keys.Where(id => !publishedIds.Contains(id)).ToList();
		_enemies.Clear();
		foreach (var state in published)
		{
			_enemies[state.EntityId] = state;
		}

		_runtimeSpawns =
		[
			.. published
				.Where(e => e.RuntimeSpawned && e.PrefabId.Length > 0)
				.Select(e => e.ToEnemySpawnEntryMsg()),
		];

		if (_session.Role == SessionRole.Host)
		{
			_enemyKernel.Sync(published);
		}
	}

	// ---- IEnemySyncControl (the packet handlers' control surface) ----

	uint IEnemySyncControl.LastEnemyStateSeq { get => _lastEnemyStateSeq; set => _lastEnemyStateSeq = value; }

	void IEnemySyncControl.ApplyEnemyStream(WireStateStream stream) => ApplyEnemyStream(stream);

	void IEnemySyncControl.ApplyEnemySnapshot(EnemySnapshotMsg msg) => ApplyEnemySnapshot(msg);

	void IEnemySyncControl.SendEnemySnapshot(ulong steamId) => SendEnemySnapshot(steamId);

	void IEnemySyncControl.SendEnemyAttack(EnemyAttackMsg msg) => SendEnemyAttack(msg);

	void IEnemySyncControl.FireEnemyAttackReceived(EnemyAttackMsg msg) => FireEnemyAttackReceived(msg);

	// ---- ICuoService ----

	void ICuoService.Initialize() => _epoch = (ulong)_time.UtcNowTicks;

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			var nowMs = _time.NowMs;
			if (nowMs >= _nextStateSendMs)
			{
				_nextStateSendMs = nowMs + (long)(_stateStreamOptions.CurrentValue.SendIntervalSeconds * 1000f);
				BroadcastEnemyState();
			}
		}
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
		_enemyCombatProjection.Dispose();
		_kernelProtocol.EntityStateStreamReceived -= OnEntityStateStreamReceived;
		_session.SessionEnded -= OnSessionEnded;
		_kernelAuthority.BatchCommitted -= OnKernelBatchCommitted;
		_kernelAuthority.BatchApplied -= OnKernelBatchApplied;
		_kernelAuthority.CheckpointRestored -= OnKernelCheckpointRestored;
	}

	// ---- Broadcast / snapshot ----

	private void BroadcastEnemyState()
	{
		var stream = new WireStateStream
		{
			Seq = ++_nextEnemySeq,
			BaseGlobalRevision = _kernelAuthority.CurrentGlobalRevision,
			EnemyStates = [.. _enemies.Values.Select(e => e.ToWireEnemyStreamState())],
		};
		var targets = _session.Members
			.Where(m => m.Handshaken && m.InWorld && m.SteamId != _session.LocalSteamId)
			.Select(m => m.SteamId)
			.ToList();
		_kernelProtocol.BroadcastStateStreamTo(targets, stream, WirePayloadType.EnemyStateStream, reliable: false);
	}

	private void SendEnemySnapshot(ulong steamId)
	{
		if (_enemies.Count == 0)
		{
			return; // an empty table is a no-op — the member's own generated enemies stay
		}

		var snapshotEnemies = _enemies.Values.ToList();
		_enemyKernelRestore.Apply(snapshotEnemies);
		var payload = new EnemySnapshotMsg
		{
			Enemies = [.. snapshotEnemies.Select(e => e.ToEnemyStateMsg())],
			RuntimeSpawns =
			[
				.. snapshotEnemies
					.Where(e => e.RuntimeSpawned && e.PrefabId.Length > 0)
					.Select(e => e.ToEnemySpawnEntryMsg()),
			],
		};
		_sender.Send(steamId, NetMsg.EnemySnapshot, payload);
	}

	// ---- Apply (guest) ----

	/// <summary>Update-only stream semantics: the 20 Hz batch refreshes the
	/// convergent fields of the enemies it contains but never removes an id that
	/// is absent from the batch. Aggregate lifecycle travels through the kernel
	/// <c>EnemyRemovedEvent</c> committed batch.</summary>
	private void ApplyEnemyStream(WireStateStream stream) =>
		Merge(stream.EnemyStates, stream.BaseGlobalRevision, EnemyStateReceived);

	private void OnEntityStateStreamReceived(ulong sender, WirePayloadType payloadType, WireStateStream stream)
	{
		if (payloadType != WirePayloadType.EnemyStateStream || stream.EnemyStates.Count == 0)
		{
			return;
		}

		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		if (stream.Seq <= _lastEnemyStateSeq)
		{
			return;
		}

		_lastEnemyStateSeq = stream.Seq;
		ApplyEnemyStream(stream);
	}

	private void RemoveEnemy(NetworkEntityId id)
	{
		var wasPresent = _enemies.Remove(id);
		_removedEnemies.Add(id);
		_terminalHealthRevision.Remove(id);
		if (wasPresent)
		{
			_log.LogInformation("[Enemy] guest removed enemy {Enemy} from kernel batch.", id);
			EnemyRemovedReceived?.Invoke(id);
		}
	}

	private void ApplyEnemySnapshot(EnemySnapshotMsg msg)
	{
		_runtimeSpawns = msg.RuntimeSpawns;
		Replace(msg.Enemies, EnemySnapshotReceived);
	}

	/// <summary>Merge the batch into the existing buffer. This is the update-only
	/// stream path: no implicit removal when an id disappears from the batch.
	/// If the stream batch predates a newer kernel terminal-health event, the
	/// terminal fields (health, stunned) are preserved while continuous
	/// presentation fields (position/velocity/rotation) still update.</summary>
	private void Merge(IEnumerable<WireEnemyStreamState> states, ulong baseGlobalRevision, Action? notify)
	{
		foreach (var state in states)
		{
			var id = PlayerStreamWireMapper.ToNetworkEntityId(state.EntityId);
			if (_removedEnemies.Contains(id))
			{
				_log.LogDebug("[Enemy] ignored stream update for removed enemy {Enemy}.", id);
				continue;
			}

			var entity = new EnemyEntity(default);
			state.ApplyTo(entity);
			if (_terminalHealthRevision.TryGetValue(id, out var terminalRevision)
				&& baseGlobalRevision < terminalRevision
				&& _enemies.TryGetValue(id, out var existing))
			{
				entity.Health = existing.Health;
				entity.Stunned = existing.Stunned;
			}

			_enemies[entity.EntityId] = entity;
		}

		notify?.Invoke();
	}

	/// <summary>Full-overwrite semantics for the world-entry / reconnect snapshot:
	/// the snapshot is the complete authoritative set at that moment. A snapshot
	/// also cannot resurrect an id that already received an explicit removal in
	/// this session — lifecycle is final.</summary>
	private void Replace(IEnumerable<EnemyStateMsg> states, Action? notify)
	{
		_enemies.Clear();
		foreach (var state in states)
		{
			var id = state.Id.ToNetworkEntityId();
			if (_removedEnemies.Contains(id))
			{
				_log.LogWarning("[Enemy] ignored snapshot entry for removed enemy {Enemy}.", id);
				continue;
			}

			var entity = new EnemyEntity(default);
			state.ApplyTo(entity);
			_enemies[entity.EntityId] = entity;
		}

		// The kernel is the authority for terminal enemy facts; project it over
		// the full snapshot before the Game Adapter consumes the restored set.
		_enemyKernelRestore.Apply(_enemies.Values);
		notify?.Invoke();
	}

	// ---- Kernel terminal revision tracking (guest) ----

	private void OnKernelBatchCommitted(CommittedBatch batch)
	{
		if (_session.Role == SessionRole.Guest)
		{
			TrackEnemyTerminalRevisions(batch);
		}
	}

	private void OnKernelBatchApplied(CommittedBatch batch)
	{
		if (_session.Role == SessionRole.Guest)
		{
			TrackEnemyTerminalRevisions(batch);
			ApplyEnemyRemovals(batch);
		}
	}

	private void ApplyEnemyRemovals(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			if (@event is EnemyRemovedEvent removed)
			{
				RemoveEnemy(ToRuntimeId(removed.EntityId));
			}
		}
	}

	private void OnKernelCheckpointRestored(GameCheckpoint checkpoint)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_terminalHealthRevision.Clear();
		if (checkpoint.Enemies is null)
		{
			return;
		}

		foreach (var enemy in checkpoint.Enemies.Enemies)
		{
			_terminalHealthRevision[ToRuntimeId(enemy.EntityId)] = checkpoint.GlobalRevision;
		}
	}

	private void TrackEnemyTerminalRevisions(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			switch (@event)
			{
				case EnemyUpsertedEvent upserted:
					_terminalHealthRevision[ToRuntimeId(upserted.State.EntityId)] = batch.GlobalRevision;
					break;
				case EnemyRemovedEvent removed:
					_terminalHealthRevision.Remove(ToRuntimeId(removed.EntityId));
					break;
			}
		}
	}

	private static NetworkEntityId ToRuntimeId(EntityId id) =>
		new(id.Epoch, id.Counter, id.Generation);

	private void OnSessionEnded()
	{
		_enemies.Clear();
		_removedEnemies.Clear();
		_terminalHealthRevision.Clear();
		_runtimeSpawns = [];
		_nextEnemySeq = 0;
		_lastEnemyStateSeq = 0;
		_nextStateSendMs = 0;
	}
}
