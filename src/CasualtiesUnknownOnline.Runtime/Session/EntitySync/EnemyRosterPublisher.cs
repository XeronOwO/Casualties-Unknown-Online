using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Host-side publication of the enemy roster: owns the "is the roster complete"
/// gate, the peers whose snapshot was HELD while it was not, the generation
/// stamp and the absolute payload. Split out of <see cref="EnemySyncService"/>
/// when the held-peer flush crossed the service's line cap, and because the
/// host's publication policy is a responsibility of its own: the service keeps
/// the enemy buffers and the stream, this class decides when a buffer may be
/// published as the authoritative roster and makes sure a held member is not
/// left waiting for the next repair wave.
/// </summary>
internal sealed class EnemyRosterPublisher(
	ISessionControl session,
	PacketSender sender,
	EnemyKernelRestoreProjection restore,
	KernelWorldGenerationSource generations,
	Func<IReadOnlyList<EnemyEntity>> currentEnemies,
	ILogger<EnemySyncService> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly EnemyKernelRestoreProjection _restore = restore;
	private readonly KernelWorldGenerationSource _generations = generations;
	private readonly Func<IReadOnlyList<EnemyEntity>> _currentEnemies = currentEnemies;
	private readonly ILogger<EnemySyncService> _log = log;

	private bool _rosterReady = true;
	private readonly HashSet<ulong> _heldPeers = [];

	/// <summary>Host side: the world is settled — a complete roster may be published. The first ready transition flushes every peer whose snapshot was held.</summary>
	internal void MarkReady()
	{
		if (_rosterReady)
		{
			return;
		}

		_rosterReady = true;
		_log.LogInformation("[Enemy] host roster is ready over {Count} enemies — snapshots may be sent.", _currentEnemies().Count);
		FlushHeld();
	}

	/// <summary>Host side: a world/layer is generating, regenerating or absent — snapshots are held rather than telling a member to delete enemies its own generation has not produced yet.</summary>
	internal void MarkUnstable()
	{
		if (!_rosterReady)
		{
			return;
		}

		_rosterReady = false;
		_log.LogInformation("[Enemy] host roster is unstable (transition boundary) — snapshots are held until the new layer's capture is complete.");
	}

	/// <summary>Host side: send the absolute roster to one member (entry, reconnect or the 60 s repair). Held while the roster is not ready; the peer is recorded for the ready-transition flush.</summary>
	internal void Send(ulong steamId)
	{
		if (!_rosterReady)
		{
			// The member's own generated enemies must NOT be cleared against a
			// partial table: while a layer generates, the host has not captured its
			// whole set yet, and an authoritative-looking empty snapshot would delete
			// the copies the member already has. The peer is flushed at the first
			// ready transition — the entry window may close on the completion marker
			// before the next repair wave.
			_heldPeers.Add(steamId);
			_log.LogInformation("[Enemy] snapshot to {Peer} held: the host roster is not ready (world generating, regenerating or absent); flushed when it is ready.", steamId);
			return;
		}

		var snapshotEnemies = _currentEnemies().ToList();
		_restore.Apply(snapshotEnemies);
		var payload = new EnemySnapshotMsg
		{
			Enemies = [.. snapshotEnemies.Select(e => e.ToEnemyStateMsg())],
			RuntimeSpawns =
			[
				.. snapshotEnemies
					.Where(e => e.RuntimeSpawned && e.PrefabId.Length > 0)
					.Select(e => e.ToEnemySpawnEntryMsg()),
			],
			Generation = _generations.Stamp(),
		};
		_sender.Send(steamId, NetMsg.EnemySnapshot, payload);
		// The entry fan-out and the 60 s in-session repair share this sender; the
		// line is what tells a post-mortem which members were (re)adopted, when and
		// with what. An empty list is authoritative: the member destroys its
		// surplus copies.
		_log.LogInformation("[Enemy] snapshot sent to {Peer}: {Enemies} enemies, {Runtime} runtime spawns, {Generation}.",
			steamId, snapshotEnemies.Count, payload.RuntimeSpawns.Count, WorldReportGeneration.Describe(payload.Generation));
	}

	/// <summary>Session ended — the held peers belong to the ended session; the ready flag is left to the next world's capture.</summary>
	internal void Reset() => _heldPeers.Clear();

	/// <summary>Every held peer that is still in the world is sent the roster now; a peer that left is dropped with a line.</summary>
	private void FlushHeld()
	{
		if (_heldPeers.Count == 0)
		{
			return;
		}

		var peers = _heldPeers.ToList();
		_heldPeers.Clear();
		var inWorld = _session.InWorldRemoteSteamIds().ToHashSet();
		foreach (var peer in peers)
		{
			if (!inWorld.Contains(peer))
			{
				_log.LogInformation("[Enemy] held snapshot for {Peer} dropped: the member is no longer in the world.", peer);
				continue;
			}

			_log.LogInformation("[Enemy] flushing the held snapshot to {Peer}: the host roster is ready.", peer);
			Send(peer);
		}
	}
}
