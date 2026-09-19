using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The runtime world-entity CREATION channel (outside world generation) and its
/// recovery state (sync-coverage audit E3). Report/relay shape: the creating
/// side keeps its local copy and reports (guest → host) or broadcasts (host →
/// every member, the reporter included); the host creates its own copy, enriches
/// the record (a created keypad's code is host authority) and relays.
/// <para>
/// A creation this host cannot materialize is the one exception: accept-first
/// never covers state the host cannot own, so it is REJECTED — neither recorded
/// nor relayed — and answered to the reporter, whose pending report and local
/// copy both end there (decision 161).
/// </para>
/// <para>
/// The live message is one-shot, so this channel also owns the tables that make
/// a swallowed report or relay heal without a reconnect: the host's
/// accepted-creation table (<see cref="RuntimeEntityRegistry"/>, sent absolutely
/// on world entry and by the 60 s cycle) and the guest's unacknowledged-report
/// table (<see cref="PendingEntityReportTable"/>, re-reported by
/// <see cref="WorldReportFallbackPump"/> until the host answers). Both tables
/// drop an entry when the entity dies, and reset at their world/layer boundary —
/// a dead entity is never resurrected and a previous layer's creations never
/// leak into the next.
/// </para>
/// <para>
/// The channel also owns the family's world/layer GENERATION gate (protocol 30):
/// every send is stamped with this side's kernel run baseline, and an arrived
/// report or absolute table of another generation is refused before anything is
/// created, recorded or relayed — a creation materializes at the reported
/// position, and positions are layer-relative. The host answers a refused
/// reporter through the existing rejection path so its pending re-report ends.
/// </para>
/// </summary>
public sealed class RuntimeEntityChannel(ISessionControl session, PacketSender sender, RuntimeEntityRegistry runtimeEntities, ItemKernelAuthority kernelAuthority, ILogger<RuntimeEntityChannel> log)
{
	/// <summary>Fallback windows after which a still-unanswered creation is reported as stalled (once — the entry keeps retrying).</summary>
	internal const int StallWarnAttempts = 10;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly RuntimeEntityRegistry _runtimeEntities = runtimeEntities;
	private readonly ILogger<RuntimeEntityChannel> _log = log;

	/// <summary>
	/// The world/layer generation this side is at: the stamp every send reads
	/// live, and the side an arrived report or absolute table is compared
	/// against. A creation is materialized at its reported position, and
	/// positions are layer-relative, so a report of another generation is
	/// refused before anything is created — in the same words every stamped
	/// family uses (<see cref="WorldReportGenerationGate"/>).
	/// </summary>
	private readonly KernelWorldGenerationSource _generations = new(kernelAuthority);

	/// <summary>
	/// Guest-side table of runtime entity creations whose report the host has
	/// not answered yet — the recovery source for a swallowed guest → host
	/// creation report, symmetric to the host's accepted-creation table.
	/// Populated by <see cref="SendEntitySpawned"/> before the send, re-reported
	/// by <see cref="ResendPendingEntityReports"/> and dropped when the host
	/// answers for the creation (its relay echo or the absolute snapshot) or
	/// when the local copy dies.
	/// </summary>
	private readonly PendingEntityReportTable _pendingEntityReports = new();

	/// <summary>The guest re-report cadence for <see cref="_pendingEntityReports"/> (shared policy with the block table).</summary>
	private readonly PendingReportFallback _entityReportFallback = new(session);

	private bool _pendingEntityOverflowLogged;
	private bool _acceptedCreationOverflowLogged;
	private bool _acceptedAnimalOverflowLogged;
	private bool _tokenlessCreationLogged;

	/// <summary>An entity-creation record arrived — the receiver creates its own copy (host: then relays; guest: remote apply).</summary>
	public event Action<ulong, EntitySpawnedMsg>? EntitySpawnedReceived;

	/// <summary>
	/// The host rejected a creation this side reported: the pending re-report is
	/// already dropped by the channel; the adapter destroys the local copy through
	/// the entity death funnel (idempotent when the copy is already gone).
	/// </summary>
	public event Action<RuntimeEntityKey, RuntimeEntityRejectReason>? RuntimeEntityRejectedReceived;

	/// <summary>How many unacknowledged guest creations are waiting for the host's answer (the fallback pump's work check and the tests' seam).</summary>
	internal int PendingEntityReportCount => _pendingEntityReports.Count;

	public void FireEntitySpawnedReceived(ulong sender, EntitySpawnedMsg msg)
	{
		// A creation of ANOTHER world/layer generation is refused WHOLE: its
		// position key is layer-relative, so materializing it would create the
		// previous layer's entity in this one, and relaying it would spread that
		// to every peer. The host also answers the reporter, whose pending
		// re-report would otherwise keep asking for a report this host will
		// never take.
		if (WorldReportGenerationGate.Relate(msg.Generation, _generations.Current, "EntitySpawned", sender, _log) == WorldGenerationRelation.Stale)
		{
			AnswerStaleCreation(sender, msg);
			return;
		}

		// The host answered this creation (the relay echo reaches the reporter
		// too) — its pending re-report is done. This is also the acknowledgement
		// for a creation the host enriched (a generated keypad code): the
		// carried payload is applied by the adapter's create path.
		if (_session.Role == SessionRole.Guest && _pendingEntityReports.Remove(RuntimeEntityKey.From(msg)))
		{
			if (_pendingEntityReports.Count == 0)
			{
				_pendingEntityOverflowLogged = false; // the overflow episode ended — a later fill must log again
			}

			_log.LogDebug("[EntitySpawn] host answered {Id} at ({X:F1},{Y:F1}) — dropped the pending report ({Remaining} left).",
				msg.Id, msg.Position.X, msg.Position.Y, _pendingEntityReports.Count);
		}

		EntitySpawnedReceived?.Invoke(sender, msg);
	}

	/// <summary>
	/// Host: a creation report of another world/layer generation was refused — it
	/// is neither recorded nor relayed, and the reporter is answered through the
	/// existing rejection path so a report this host will never take stops
	/// re-reporting (the answer also ends the reporter's pending entry and asks
	/// it to remove its local copy, idempotently). Only a remote reporter is
	/// answered: the host's own creations never arrive here, and a guest refuses
	/// the host's stale relay silently — its own pending entry is about its own
	/// world, not the relay's.
	/// </summary>
	private void AnswerStaleCreation(ulong sender, EntitySpawnedMsg msg)
	{
		if (_session.Role != SessionRole.Host || sender == _session.LocalSteamId)
		{
			return;
		}

		var key = RuntimeEntityKey.From(msg);
		_log.LogWarning("[EntitySpawn] refused stale creation {Id} at ({X:F1},{Y:F1}) (creation {Creator}:{Sequence}) from {Reporter} — answering the reporter so its pending report ends.",
			msg.Id, msg.Position.X, msg.Position.Y, key.CreatorSteamId, key.CreationSequence, sender);

		_sender.Send(sender, NetMsg.RuntimeEntityRejected, new RuntimeEntityRejectedMsg
		{
			Key = key.ToKeyMsg(),
			Reason = RuntimeEntityRejectReason.StaleGeneration,
		});
	}

	/// <summary>
	/// Report a runtime world-entity creation (outside generation — the spawn
	/// command): guest → host as a report (the host creates its own copy and
	/// relays), host → broadcast to all synced members. Same shape as
	/// SendEntityEvent: the creating side keeps its local copy.
	/// <para>
	/// An ANIMAL creation enters only the GUEST pending table and the host's
	/// animal ACK set, never the host's materializable creation table. Its
	/// world-entry/60 s recovery is owned by the enemy domain
	/// (<c>EnemySnapshot.RuntimeSpawns</c> materializes at the animal's CURRENT
	/// position with a host-allocated id), so a materializable host record would
	/// make a late joiner create a second copy at the creation position; the
	/// guest-side pending re-report is still needed because a swallowed
	/// guest → host animal report has no other in-session recovery, and it is
	/// safe: the host's answer (relay echo or the snapshot's key list) and the
	/// entity's death both drop the entry.
	/// </para>
	/// </summary>
	public void SendEntitySpawned(EntitySpawnedMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		// A tokenless report is a silent-collapse hazard: two creations of the
		// same prefab inside one cell would share one record. The adapter stamps
		// every local creation, so this is a hand-built message — surface it once
		// instead of letting the records merge invisibly.
		if (!msg.HasCreationToken && !_tokenlessCreationLogged)
		{
			_tokenlessCreationLogged = true;
			_log.LogWarning("[EntitySpawn] creation {Id} at ({X:F1},{Y:F1}) carries no creation token — a second creation of the same prefab in the same cell would share this record.",
				msg.Id, msg.Position.X, msg.Position.Y);
		}

		// The stamp is read LIVE at send time, BEFORE the record is taken: the
		// record and the wire must carry the same generation, and a creation
		// belongs to the world this side simulates now.
		msg.Generation = _generations.Stamp();

		if (_session.Role == SessionRole.Host)
		{
			if (msg.IsAnimal)
			{
				RecordAcceptedAnimal(msg);
			}
			else
			{
				RecordAcceptedCreation(msg);
			}

			_session.Broadcast(NetMsg.EntitySpawned, msg);
		}
		else
		{
			// Record BEFORE the send: a send that never lands is exactly what
			// the fallback exists for.
			RecordPendingEntityReport(msg);
			_sender.Send(_session.HostSteamId, NetMsg.EntitySpawned, msg);
		}
	}

	/// <summary>
	/// Host only: a guest reported a runtime creation this host could NOT
	/// materialize locally (its content set lacks the prefab/template). The
	/// corrected accept-first precondition is explicit — accept-first never
	/// covers state the host cannot own, and this creation is such state: the
	/// host has no local copy whose death could ever drop a record, so a record
	/// would re-materialize the creation on a member that later destroyed its
	/// copy, and a relay would hand every peer that HAS the prefab an entity the
	/// host can never back up or retract (an unowned accept that leaks into every
	/// later snapshot). The creation is therefore REJECTED: neither recorded nor
	/// relayed, and the reporter is answered directly with
	/// <see cref="NetMsg.RuntimeEntityRejected"/> — which stops its 60 s re-report,
	/// destroys its local copy, and makes the session's inconsistent content sets
	/// visible instead of silently divergent.
	/// </summary>
	public void ReportEntitySpawnUnmaterialized(ulong sender, EntitySpawnedMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || sender == _session.LocalSteamId)
		{
			return;
		}

		var key = RuntimeEntityKey.From(msg);
		_log.LogWarning("[EntitySpawn] REJECTED {Id} at ({X:F1},{Y:F1}) (creation {Creator}:{Sequence}) reported by {Reporter}: this host cannot materialize the prefab, so the creation is neither recorded nor relayed.",
			msg.Id, msg.Position.X, msg.Position.Y, key.CreatorSteamId, key.CreationSequence, sender);

		_sender.Send(sender, NetMsg.RuntimeEntityRejected, new RuntimeEntityRejectedMsg
		{
			Key = key.ToKeyMsg(),
			Reason = RuntimeEntityRejectReason.PrefabUnavailable,
		});
	}

	/// <summary>
	/// Guest: the host rejected a creation this side reported — the answer the
	/// pending re-report waited for. Only the host's own message is considered,
	/// and only when the creation is this member's own: either the creation token's creator half is this
	/// member, or the pending table still holds the report — the proof that this
	/// member reported it (a mod-built report can carry a token naming another
	/// creator, and then the creator half alone would leave the entry
	/// re-reporting forever). A rejection that reaches another member changes
	/// nothing there. The pending entry is dropped so the 60 s fallback stops,
	/// and the adapter is asked to destroy the local copy — idempotent, so a
	/// replayed rejection that arrives after the copy already died is a no-op
	/// rather than a resurrection.
	/// </summary>
	public void FireRuntimeEntityRejectedReceived(ulong sender, RuntimeEntityRejectedMsg msg)
	{
		if (_session.Role != SessionRole.Guest || msg.Key is null)
		{
			return;
		}

		if (sender != _session.HostSteamId)
		{
			_log.LogWarning("[EntitySpawn] ignoring a rejection from {Sender} — only the host answers a creation report.", sender);
			return;
		}

		var key = RuntimeEntityKey.FromKeyMsg(msg.Key);
		if (key.CreatorSteamId != _session.LocalSteamId && !_pendingEntityReports.Contains(key))
		{
			_log.LogDebug("[EntitySpawn] ignoring a rejection for {Id} at ({X},{Y}) — creation {Creator} is neither this member's token nor its pending report.",
				key.Id, key.X, key.Y, key.CreatorSteamId);
			return;
		}

		if (_pendingEntityReports.Remove(key) && _pendingEntityReports.Count == 0)
		{
			_pendingEntityOverflowLogged = false; // the overflow episode ended — a later fill must log again
		}

		_log.LogWarning("[EntitySpawn] host rejected {Id} at ({X:F1},{Y:F1}) (creation {Creator}:{Sequence}): {Reason} — the pending report is dropped; the reporter is asked to remove the local copy.",
			key.Id, key.X, key.Y, key.CreatorSteamId, key.CreationSequence, msg.Reason);

		RuntimeEntityRejectedReceived?.Invoke(key, msg.Reason);
	}

	/// <summary>Host only: send the accepted-creation table to one member (world entry, or the 60 s cycle), stamped with this side's current world/layer generation.</summary>
	public void SendRuntimeEntitySnapshot(ulong targetSteamId) => _runtimeEntities.SendSnapshot(targetSteamId, _generations.Stamp());

	/// <summary>
	/// Guest: the host's absolute runtime-entity table arrived (world entry or
	/// the 60 s re-broadcast). The animal key list is an acknowledgement ONLY —
	/// it drops the matching pending re-reports and never materializes
	/// anything. Every entry is the host's answer for that creation and then
	/// runs the LIVE creation path — missing entities materialize, existing ones
	/// are deduped by their creation key, so the snapshot is additive and
	/// idempotent.
	/// </summary>
	public void FireRuntimeEntitySnapshotReceived(ulong sender, RuntimeEntitySnapshotMsg snapshot)
	{
		// The absolute table is refused WHOLE when it describes another
		// world/layer generation: its entries materialize at layer-relative
		// positions, so a snapshot of the previous layer would create the old
		// layer's entities in the new one — and its animal acknowledgements
		// would settle pending reports that belong to a world this host is not
		// describing. One log line covers the whole table.
		if (WorldReportGenerationGate.Relate(snapshot.Generation, _generations.Current, "RuntimeEntitySnapshot", sender, _log) == WorldGenerationRelation.Stale)
		{
			return;
		}

		foreach (var animalKey in snapshot.AcceptedAnimalKeys)
		{
			var key = RuntimeEntityKey.FromKeyMsg(animalKey);
			if (_session.Role == SessionRole.Guest && _pendingEntityReports.Remove(key))
			{
				_log.LogDebug("[EntitySpawn] host accepted animal creation {Id} at ({X},{Y}) — dropped the pending report ({Remaining} left).",
					key.Id, key.X, key.Y, _pendingEntityReports.Count);
			}
		}

		if (_session.Role == SessionRole.Guest && _pendingEntityReports.Count == 0)
		{
			_pendingEntityOverflowLogged = false;
		}

		foreach (var entry in snapshot.Entries)
		{
			FireEntitySpawnedReceived(sender, entry);
		}

		_log.LogInformation("[EntitySpawn] applied host runtime-entity snapshot ({Count} entries, {Animals} animal acknowledgements).",
			snapshot.Entries.Count, snapshot.AcceptedAnimalKeys.Count);
	}

	/// <summary>
	/// Either side: the runtime-created entity with this CREATION key is gone
	/// (the adapter's death hook) — the host drops the accepted record (or the
	/// animal acknowledgement) so no snapshot or re-broadcast resurrects it;
	/// the guest drops the pending re-report — a dead entity must never be
	/// reported again.
	/// </summary>
	public void ReportRuntimeEntityDestroyed(RuntimeEntityKey key)
	{
		if (string.IsNullOrEmpty(key.Id))
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			if (_runtimeEntities.Remove(key))
			{
				_log.LogInformation("[EntitySpawn] {Id} at ({X},{Y}) died — dropped the accepted creation record.",
					key.Id, key.X, key.Y);
			}

			return;
		}

		if (_pendingEntityReports.Remove(key))
		{
			if (_pendingEntityReports.Count == 0)
			{
				_pendingEntityOverflowLogged = false;
			}

			_log.LogInformation("[EntitySpawn] {Id} at ({X},{Y}) died before the host answered — dropped the pending report.",
				key.Id, key.X, key.Y);
		}
	}

	/// <summary>
	/// Guest only: re-report every unacknowledged creation to the host. Each
	/// entry is one <see cref="NetMsg.EntitySpawned"/> report, so the host's
	/// existing accept-and-relay path handles it unchanged (idempotent — the
	/// adapter binds the copy by its creation key). Called by
	/// <see cref="WorldReportFallbackPump"/>; a no-op when nothing is
	/// outstanding, when this side is not a guest, or when the session ended.
	/// Entries are never dropped for age: an unreachable host keeps them until
	/// the answer, the entity's death or a world/session boundary. A creation
	/// the host cannot materialize is answered with a rejection, which drops the
	/// entry; only a genuinely unreachable host leaves it retrying, and the stall
	/// warning makes that visible instead of silent.
	/// </summary>
	public void ResendPendingEntityReports()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || _pendingEntityReports.Count == 0)
		{
			return;
		}

		foreach (var entry in _pendingEntityReports.Entries)
		{
			// Read live at send time: a surviving pending entry belongs to the
			// world this side simulates now (the generation boundary drops the
			// table), so the re-report carries the CURRENT generation.
			entry.Msg.Generation = _generations.Stamp();
			_sender.Send(_session.HostSteamId, NetMsg.EntitySpawned, entry.Msg);
			var attempts = _pendingEntityReports.RecordAttempt(entry.Key);
			if (attempts == StallWarnAttempts)
			{
				_log.LogWarning("[EntitySpawn] {Id} at ({X:F1},{Y:F1}) is still unacknowledged after {Attempts} fallback re-reports — the host may lack the prefab or never received the report.",
					entry.Msg.Id, entry.Msg.Position.X, entry.Msg.Position.Y, attempts);
			}
		}

		_log.LogInformation("[EntitySpawn] re-reported {Count} unacknowledged creation(s) to the host.",
			_pendingEntityReports.Count);
	}

	/// <summary>The guest entity-report fallback's time edge (driven by <see cref="WorldReportFallbackPump"/>; the cadence policy lives in <see cref="PendingReportFallback"/>).</summary>
	internal void PumpEntityReportFallback(long nowMs) =>
		_entityReportFallback.Pump(nowMs, _pendingEntityReports.Count, ResendPendingEntityReports);

	/// <summary>
	/// Guest: a new world/layer baseline was applied — every pending creation
	/// belongs to the previous world and must not be materialized into the new
	/// one. Called from the guest's world-params apply boundary (the adapter's
	/// WorldParamsService), symmetric to the host's accepted-table reset at its
	/// generation boundary. The world-entry completion marker deliberately does
	/// NOT clear the table: a reconnect-while-in-world keeps the guest's local
	/// creations, and re-reporting them is exactly the recovery.
	/// </summary>
	public void ResetPendingEntityReports()
	{
		if (_pendingEntityReports.Count > 0)
		{
			_log.LogInformation("[EntitySpawn] world baseline replaced — cleared {Count} pending creation report(s) from the previous world.",
				_pendingEntityReports.Count);
			_pendingEntityReports.Clear();
		}

		_pendingEntityOverflowLogged = false;
		_entityReportFallback.Reset();
	}

	/// <summary>Host only: a new world layer is generating — the accepted-creation table starts empty again (the runtime entities are gone with the scene).</summary>
	public void ResetRuntimeEntities()
	{
		_runtimeEntities.Reset();
		_acceptedCreationOverflowLogged = false;
		_acceptedAnimalOverflowLogged = false;
	}

	/// <summary>Host only: record an accepted non-animal creation (the host's own local creation or a relayed guest report) into the absolute table.</summary>
	private void RecordAcceptedCreation(EntitySpawnedMsg msg)
	{
		if (_runtimeEntities.Report(msg))
		{
			return;
		}

		if (_acceptedCreationOverflowLogged)
		{
			return;
		}

		_acceptedCreationOverflowLogged = true;
		_log.LogWarning("[EntitySpawn] accepted-creation table is full ({Cap} creations) — new creations are not re-broadcast until a layer reset (creation {Id} at ({X:F1},{Y:F1}) dropped).",
			_runtimeEntities.Cap, msg.Id, msg.Position.X, msg.Position.Y);
	}

	/// <summary>Host only: record an accepted animal creation by key — acknowledgement only, never materialized from the snapshot.</summary>
	private void RecordAcceptedAnimal(EntitySpawnedMsg msg)
	{
		if (_runtimeEntities.ReportAnimal(msg))
		{
			return;
		}

		if (_acceptedAnimalOverflowLogged)
		{
			return;
		}

		_acceptedAnimalOverflowLogged = true;
		_log.LogWarning("[EntitySpawn] accepted-animal key set is full ({Cap} creations) — new animal reports are not acknowledged until a layer reset (creation {Id} at ({X:F1},{Y:F1}) dropped).",
			_runtimeEntities.Cap, msg.Id, msg.Position.X, msg.Position.Y);
	}

	/// <summary>
	/// Guest only: record the creation as unacknowledged before the live send.
	/// A newer report for the same creation supersedes the older record (the
	/// host only needs the current record).
	/// </summary>
	private void RecordPendingEntityReport(EntitySpawnedMsg msg)
	{
		if (_pendingEntityReports.Report(msg))
		{
			return;
		}

		if (_pendingEntityOverflowLogged)
		{
			return;
		}

		_pendingEntityOverflowLogged = true;
		_log.LogWarning("[EntitySpawn] pending creation table is full ({Cap} creations) — new creations are not re-reported until the host answers (creation {Id} at ({X:F1},{Y:F1}) dropped).",
			_pendingEntityReports.Cap, msg.Id, msg.Position.X, msg.Position.Y);
	}
}
