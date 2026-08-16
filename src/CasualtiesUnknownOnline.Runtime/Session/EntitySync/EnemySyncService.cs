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

	private readonly Dictionary<NetworkEntityId, EnemyEntity> _enemies = [];
	private IReadOnlyList<EnemySpawnEntryMsg> _runtimeSpawns = [];
	private uint _nextEnemySeq; // host: EnemyState broadcast seq
	private long _nextStateSendMs;
	private uint _lastEnemyStateSeq; // guest: last applied seq (the unreliable-stream gate)
	private ulong _epoch; // host: the enemy-id epoch (set on Initialize)
	private uint _nextEnemyCounter; // host: enemy-id allocation counter

	public EnemySyncService(ISessionControl session, PacketSender sender, ITimeSource time,
		IOptionsMonitor<StateStreamOptions> stateStreamOptions, ILogger<EnemySyncService> log)
	{
		_session = session;
		_sender = sender;
		_time = time;
		_stateStreamOptions = stateStreamOptions;
		_log = log;
		session.SessionEnded += OnSessionEnded;
	}

	/// <summary>Raised after the full world-entry snapshot is applied (the Game Adapter binds its local enemy copies to the host's ids on this).</summary>
	public event Action? EnemySnapshotReceived;

	/// <summary>Raised after a state-stream batch is applied (the Game Adapter drives the frozen render copies from the buffered state on this).</summary>
	public event Action? EnemyStateReceived;

	/// <summary>Raised when an enemy bite arrives (report or relay) — the Game Adapter applies the post-bite limb/body state to the victim's clone.</summary>
	public event Action<ulong, EnemyBiteMsg>? EnemyBiteReceived;

	/// <summary>Raised on the victim's side when a host-ordered enemy attack arrives — the Game Adapter applies it to the local body and reports the terminal state.</summary>
	public event Action<EnemyAttackMsg>? EnemyAttackReceived;

	/// <summary>Raised when a crystal-lunge terminal state arrives (report or relay) — the Game Adapter applies the post-lunge limb/body state to the victim's clone.</summary>
	public event Action<ulong, EnemyLungeMsg>? EnemyLungeReceived;

	/// <summary>Raised when an enemy-proximity side effect arrives (report or relay) — the Game Adapter applies the post-effect body state to the victim's clone.</summary>
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
	/// Report/broadcast an enemy bite: a guest reports its own bite to the host
	/// (the victim is the reporter); the host broadcasts its own bite to every
	/// guest (its body is already damaged locally). Reliable — a lost event
	/// self-heals on the next 1 Hz character snapshot, but the event itself must
	/// arrive to remove the use latency (the trigger rides the event, never the
	/// snapshot).
	/// </summary>
	public void SendEnemyBite(EnemyBiteMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.EnemyBite, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.EnemyBite, msg);
		}
	}

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
	/// Report/broadcast a crystal-lunge terminal state (the same star semantics
	/// as EnemyBite): a guest reports its locally-applied lunge to the host; the
	/// host broadcasts its own lunge to every guest.
	/// </summary>
	public void SendEnemyLunge(EnemyLungeMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.EnemyLunge, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.EnemyLunge, msg);
		}
	}

	/// <summary>A crystal-lunge terminal state arrived (report or relay) — surface it for the Game Adapter to apply.</summary>
	public void FireEnemyLungeReceived(ulong sender, EnemyLungeMsg msg)
		=> EnemyLungeReceived?.Invoke(sender, msg);

	/// <summary>
	/// Report/broadcast an enemy-proximity side effect (ElderThornback horror,
	/// Xaloris septic tick, GrabberPlant grab — the same star semantics as
	/// EnemyBite): a guest reports its own effect to the host; the host
	/// broadcasts its own effect to every guest. Reliable — the event carries
	/// the post-effect terminal state, never a delta.
	/// </summary>
	public void SendEnemyEffect(EnemyEffectMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.EnemyEffect, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.EnemyEffect, msg);
		}
	}

	/// <summary>An enemy-proximity side effect arrived (report or relay) — surface it for the Game Adapter to apply.</summary>
	public void FireEnemyEffectReceived(ulong sender, EnemyEffectMsg msg)
		=> EnemyEffectReceived?.Invoke(sender, msg);

	/// <summary>Host side: publish the authoritative enemy states (the Game Adapter captures the simulated enemies and overwrites the buffer each tick).</summary>
	public void PublishEnemyStates(IEnumerable<EnemyEntity> states)
	{
		var published = states.ToList();
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
	}

	// ---- IEnemySyncControl (the packet handlers' control surface) ----

	uint IEnemySyncControl.LastEnemyStateSeq { get => _lastEnemyStateSeq; set => _lastEnemyStateSeq = value; }

	void IEnemySyncControl.ApplyEnemyState(EnemyStateBatchMsg msg) => ApplyEnemyState(msg);

	void IEnemySyncControl.ApplyEnemySnapshot(EnemySnapshotMsg msg) => ApplyEnemySnapshot(msg);

	void IEnemySyncControl.SendEnemySnapshot(ulong steamId) => SendEnemySnapshot(steamId);

	void IEnemySyncControl.SendEnemyAttack(EnemyAttackMsg msg) => SendEnemyAttack(msg);

	void IEnemySyncControl.FireEnemyAttackReceived(EnemyAttackMsg msg) => FireEnemyAttackReceived(msg);

	void IEnemySyncControl.SendEnemyLunge(EnemyLungeMsg msg) => SendEnemyLunge(msg);

	void IEnemySyncControl.FireEnemyLungeReceived(ulong sender, EnemyLungeMsg msg) => FireEnemyLungeReceived(sender, msg);

	void IEnemySyncControl.SendEnemyEffect(EnemyEffectMsg msg) => SendEnemyEffect(msg);

	void IEnemySyncControl.FireEnemyEffectReceived(ulong sender, EnemyEffectMsg msg) => FireEnemyEffectReceived(sender, msg);

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

	void IDisposable.Dispose() => _session.SessionEnded -= OnSessionEnded;

	// ---- Broadcast / snapshot ----

	private void BroadcastEnemyState()
	{
		var payload = new EnemyStateBatchMsg
		{
			Seq = ++_nextEnemySeq,
			Enemies = [.. _enemies.Values.Select(e => e.ToEnemyStateMsg())],
		};
		foreach (var member in _session.Members)
		{
			if (member.Handshaken && member.InWorld && member.SteamId != _session.LocalSteamId)
			{
				_sender.Send(member.SteamId, NetMsg.EnemyState, payload, reliable: false);
			}
		}
	}

	private void SendEnemySnapshot(ulong steamId)
	{
		if (_enemies.Count == 0)
		{
			return; // an empty table is a no-op — the member's own generated enemies stay
		}

		var payload = new EnemySnapshotMsg
		{
			Enemies = [.. _enemies.Values.Select(e => e.ToEnemyStateMsg())],
			RuntimeSpawns =
			[
				.. _enemies.Values
					.Where(e => e.RuntimeSpawned && e.PrefabId.Length > 0)
					.Select(e => e.ToEnemySpawnEntryMsg()),
			],
		};
		_sender.Send(steamId, NetMsg.EnemySnapshot, payload);
	}

	// ---- Apply (guest) ----

	private void ApplyEnemyState(EnemyStateBatchMsg msg) => Replace(msg.Enemies, EnemyStateReceived);

	private void ApplyEnemySnapshot(EnemySnapshotMsg msg)
	{
		_runtimeSpawns = msg.RuntimeSpawns;
		Replace(msg.Enemies, EnemySnapshotReceived);
	}

	/// <summary>Full-overwrite semantics: the host's batch IS the whole enemy set —
	/// a disappeared enemy (destroyed, off-screen) must drop out, not linger.</summary>
	private void Replace(IEnumerable<EnemyStateMsg> states, Action? notify)
	{
		_enemies.Clear();
		foreach (var state in states)
		{
			var entity = new EnemyEntity(default);
			state.ApplyTo(entity);
			_enemies[entity.EntityId] = entity;
		}

		notify?.Invoke();
	}

	private void OnSessionEnded()
	{
		_enemies.Clear();
		_runtimeSpawns = [];
		_nextEnemySeq = 0;
		_lastEnemyStateSeq = 0;
		_nextStateSendMs = 0;
	}
}
