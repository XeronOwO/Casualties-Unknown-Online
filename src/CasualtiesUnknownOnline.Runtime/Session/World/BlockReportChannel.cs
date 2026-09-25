using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The block-report channel: the block state / block damage / partial-damage
/// family of the world message flow, its world/layer GENERATION GATE, and the
/// guest-side recovery surface those reports answer. Every report of the family
/// is stamped on send and compared on receipt (see
/// <see cref="WorldReportGeneration.Relate"/>), so a stale previous-layer report is
/// refused instead of written into a freshly generated world — the cell keys of
/// this family are layer-relative, which is what makes the identity necessary.
/// <para>
/// Extracted from <see cref="WorldStateMessageService"/> at the 600-line
/// aggregate gate: every arrival path of the family passes the gate here, while
/// the rest of the world message flow (world join, block-state table, building
/// entities, earthquakes, keypads/geysers, radiation) stays in the message
/// service. The two collaborators it owns are older than this split
/// (<see cref="GuestReportRecovery"/> carries the guest's pending tables and
/// their fallback windows, <see cref="BlockDamageSnapshotSender"/> the host's
/// snapshot); they moved under the channel unchanged, and only the generation
/// stamp was added to them.
/// </para>
/// </summary>
internal sealed class BlockReportChannel(
	ISessionControl session,
	PacketSender sender,
	INativeWorldFacts? nativeWorldFacts,
	KernelWorldGenerationSource generations,
	ILogger<WorldService> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;

	/// <summary>The adapter's native world-fact port (optional): the host's report answer reads this host's own rows through it, and the periodic snapshot sends them.</summary>
	private readonly INativeWorldFacts? _nativeWorldFacts = nativeWorldFacts;

	/// <summary>
	/// The generation every direct world report of this family is stamped with, and
	/// the one a received report is compared against: the kernel run baseline. The
	/// stamp is what tells a stale previous-layer report apart from a legitimate
	/// one, and the comparison is the single place "is this report about MY world?"
	/// is answered.
	/// </summary>
	private readonly KernelWorldGenerationSource _generations = generations;

	private readonly GuestReportRecovery _guestReports = new(session, sender, generations, log);

	private readonly BlockDamageSnapshotSender _blockDamageSnapshot = new(session, sender, nativeWorldFacts, generations, log);

	// ---- Block damage (late-joiner backfill) ----

	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived;

	/// <summary>
	/// Compare a received direct world report's generation stamp with this side's
	/// own generation. The verdict and its refusal log are the shared
	/// <see cref="WorldReportGenerationGate"/> that the trap-layout and
	/// runtime-entity families use too, so every stamped family refuses a stale
	/// report in the same words.
	/// </summary>
	private WorldGenerationRelation RelateReportGeneration(WorldGenerationMsg? generation, string kind, ulong sender) =>
		WorldReportGenerationGate.Relate(generation, _generations.Current, kind, sender, _log);

	/// <summary>
	/// Guest: the host's partial-damage row set arrived — authoritative STATE for
	/// every cell it names, and, when <paramref name="answersReport"/> is set, the
	/// ANSWER to this side's own report: those cells are accounted for, so their
	/// outstanding entries are done (the cumulative contribution stays, because
	/// the host's ledger now holds it). The periodic / world-entry snapshot
	/// deliberately does NOT clear anything: it says nothing about whether a
	/// particular sender's report was accounted for, and clearing a third party's
	/// entry would drop its contribution without it ever being reported.
	/// A snapshot from another generation is refused before either half runs: its
	/// rows are keyed by block cell, so applying them would write another layer's
	/// damage onto this side's cells.
	/// </summary>
	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries, WorldGenerationMsg? generation, bool answersReport)
	{
		if (RelateReportGeneration(generation, "BlockDamageSnapshot", _session.HostSteamId) == WorldGenerationRelation.Stale)
		{
			return;
		}

		if (_session.Role == SessionRole.Guest && answersReport)
		{
			_guestReports.AnswerDamage(entries);
		}

		BlockDamageSnapshotReceived?.Invoke(entries);
	}

	/// <summary>Host only: send the partial damage the GAME's own list holds (see <see cref="BlockDamageSnapshotSender"/>).</summary>
	public void SendBlockDamageSnapshot(ulong targetSteamId) => _blockDamageSnapshot.Send(targetSteamId);

	public event Action<ulong, int, int, float, bool, IReadOnlyList<BlockDropEntryMsg>?, IReadOnlyList<TrapDropEntryMsg>?, WorldGenerationRelation>? BlockDamagedReceived;

	/// <summary>
	/// Host only: what each sender has cumulatively contributed to each cell —
	/// the accounting that makes a report idempotent and a delayed delta a no-op
	/// (review/partial-damage-delta-report-overlap). A guest never fills it: it is
	/// the RECEIVER's record of other senders' contributions, and a guest only
	/// ever receives the host's relays, which it applies without accounting.
	/// </summary>
	private readonly RemoteBlockDamageLedger _damageLedger = new();

	private bool _ledgerOverflowLogged;

	public void FireBlockDamagedReceived(ulong sender, int x, int y, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, float contribution, WorldGenerationMsg? generation)
	{
		// A stale report still travels to the domain WITH its verdict: the break's
		// drops are the breaker's LOCAL copies, and a report that belongs to
		// another generation must roll them back exactly as a first-writer loss
		// does. Dropping the message here would leave the reporter's items alive
		// against a world that no longer holds them, with no observable reason.
		//
		// The break-drop report's acknowledgement is NOT cleared here: the cell a
		// report belongs to is the game's own world→cell conversion (it offsets by
		// the world's half extent), which only the adapter can run — BlockBreakSync
		// answers the pending drop report with the cell it resolved.
		var relation = RelateReportGeneration(generation, "BlockDamaged", sender);

		// A partial-damage report carries its sender's cumulative contribution:
		// this host hands the domain the DIFFERENCE it has not accounted for, so a
		// repeat, a stale frame or a duplicate report resolves to nothing and is
		// neither applied nor relayed. Everything else — a break with its drops, a
		// drop re-report, a sender that could not account for the cell — keeps the
		// raw damage path unchanged.
		if (AdmitRemoteContribution(sender, x, y, contribution, drops, buildingDrops, relation) is { } increment)
		{
			if (increment > 0f)
			{
				BlockDamagedReceived?.Invoke(sender, x, y, increment, false, null, null, relation);
			}

			return;
		}

		BlockDamagedReceived?.Invoke(sender, x, y, damage, metalBonus, drops, buildingDrops, relation);
	}

	/// <summary>
	/// Host only: resolve ONE sender's reported contribution for ONE cell against
	/// the ledger. Null = this message is not a contribution (the caller keeps the
	/// raw path); 0 = the contribution is already accounted for, so nothing is
	/// applied and nothing is relayed; anything above 0 is the increment this host
	/// has not seen and now commits.
	/// </summary>
	private float? AdmitRemoteContribution(ulong sender, int x, int y, float contribution, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, WorldGenerationRelation relation)
	{
		if (_session.Role != SessionRole.Host || relation == WorldGenerationRelation.Stale
			|| contribution <= 0f || drops is { Count: > 0 } || buildingDrops is { Count: > 0 })
		{
			return null;
		}

		var resolution = _damageLedger.Resolve(sender, x, y, contribution);
		_damageLedger.Commit(resolution);
		LogLedgerOverflow(resolution, sender);
		return resolution.Increment;
	}

	/// <summary>The ledger's own cap is far above any real damage set; a full one degrades to "every report is new" instead of losing the sender's damage, and the episode is logged exactly once.</summary>
	private void LogLedgerOverflow(RemoteBlockDamageLedger.Resolution resolution, ulong sender)
	{
		if (resolution.Trackable)
		{
			_ledgerOverflowLogged = false; // the episode ended — a later fill must log again
			return;
		}

		if (_ledgerOverflowLogged)
		{
			return;
		}

		_ledgerOverflowLogged = true;
		_log.LogWarning(
			"[BlockSync] per-sender partial-damage ledger is full ({Cap} cells) — {Peer}'s contribution at ({X},{Y}) is applied without being tracked, so a repeat of it can count twice until the world is reset.",
			RemoteBlockDamageLedger.DefaultCap, sender, resolution.X, resolution.Y);
	}

	public event Action<ulong, int, int, ushort, bool, WorldGenerationRelation>? BlockPlacedReceived;

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block, bool playerBreak, WorldGenerationMsg? generation)
	{
		var relation = RelateReportGeneration(generation, "BlockPlaced", sender);
		if (relation == WorldGenerationRelation.Stale)
		{
			// An air write of another generation would clear (or place) a block on a
			// cell this side generated differently — refused before it can become
			// arbitration evidence or a world write. No answer is owed: the
			// reporter's own generation boundary drops its pending report, and
			// answering with this side's cell value would write THIS generation's
			// block into the reporter's older world.
			return;
		}

		OnBlockPlacedReceived(sender, x, y, block, playerBreak, relation);
	}

	/// <summary>Guest: a block was placed locally — record it as an unacknowledged pending report and send it (the host arbitrates + answers; a swallowed report is re-reported by the fallback). The write's presentation claim is recorded with the entry: a re-report must reach the same verdict, so a lost break report cannot turn a break into a silent write.</summary>
	public void SendBlockPlacedReport(int x, int y, ushort block, bool playerBreak)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_guestReports.ReportBlock(x, y, block, playerBreak);
		_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
			new BlockPlacedMsg { X = x, Y = y, Block = block, PlayerBreak = playerBreak, Generation = _generations.Stamp() });
	}

	public void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block, bool playerBreak)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockPlacedMsg { X = x, Y = y, Block = block, PlayerBreak = playerBreak, Generation = _generations.Stamp() };
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockPlaced, msg);
	}

	public void SendBlockDamaged(int x, int y, float damage, bool metalBonus, float contribution, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = BuildBlockDamaged(x, y, damage, metalBonus, contribution, drops, buildingDrops);
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BlockDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockDamaged, msg);
		}
	}

	public void BroadcastBlockDamaged(ulong excludeSteamId, int x, int y, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// A relay carries the damage the host actually applied (an increment in
		// the game's accumulated units, so no multiplier rides along) and no
		// contribution: only the host keeps the per-sender ledger, and a third
		// party's row accumulates what it is handed.
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockDamaged, BuildBlockDamaged(x, y, damage, metalBonus, 0f, drops, buildingDrops));
	}

	private BlockDamagedMsg BuildBlockDamaged(int x, int y, float damage, bool metalBonus, float contribution, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		new()
		{
			X = x,
			Y = y,
			Damage = damage,
			MetalBonus = metalBonus,
			Contribution = contribution,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
			BuildingDrops = buildingDrops is { Count: > 0 } ? [.. buildingDrops] : null,
			Generation = _generations.Stamp(),
		};

	// ---- Guest report recovery (audit gaps W1/W2) ----
	// The re-send/answer WIRE logic lives in GuestReportRecovery; the recovery
	// STATE lives in its two bookkeeping collaborators. These members are only
	// the surface WorldService and the handlers call.

	/// <summary>How many unacknowledged guest block reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockReportCount => _guestReports.PendingBlockCount;

	/// <summary>Guest only: re-report every unacknowledged block mutation to the host (the fallback pump's action).</summary>
	public void ResendPendingBlockReports() => _guestReports.ResendBlocks();

	/// <summary>
	/// Host only: answer a guest's refused block report with the host's
	/// authoritative block at that cell. A correction carries no presentation
	/// claim: it is this host's current cell value, not a break this side watched
	/// happen — the reporter already presented its own break where it computed it
	/// (see <see cref="GuestReportRecovery.SendBlockPlacedCorrection"/>).
	/// </summary>
	public void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block) =>
		_guestReports.SendBlockPlacedCorrection(targetSteamId, x, y, block);

	/// <summary>Guest: the host answered for this cell (relay or correction) — its value is authoritative, the pending report is done. The write's presentation claim travels with the answer (a correction carries none).</summary>
	private void OnBlockPlacedReceived(ulong sender, int x, int y, ushort block, bool playerBreak, WorldGenerationRelation generation)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_guestReports.AnswerBlock(x, y);
		}

		BlockPlacedReceived?.Invoke(sender, x, y, block, playerBreak, generation);
	}

	/// <summary>
	/// Guest: a new world/layer baseline was applied — every pending report
	/// belongs to the previous world and must not be arbitrated into the new
	/// one. Called from the guest's world-params apply boundary (the adapter's
	/// WorldParamsService), symmetric to the host's ResetDamagedBlocks at the
	/// generation boundary. The world-entry completion marker deliberately does
	/// NOT clear the table: a reconnect-while-in-world keeps the guest's local
	/// mutations, and re-reporting them is exactly the recovery.
	/// </summary>
	public void ResetPendingBlockReports() => _guestReports.ResetBlocks();

	// ---- Guest partial-damage report recovery (audit gap W2) ----

	/// <summary>How many unacknowledged guest partial-damage reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockDamageReportCount => _guestReports.PendingDamageCount;

	/// <summary>The guest report recovery (W1 block state, W2 partial damage, W1's drop half) — <see cref="WorldService"/> relays that surface to the adapter without this message surface growing a member per channel (the 600-line gate).</summary>
	internal GuestReportRecovery GuestReports => _guestReports;

	/// <summary>Guest only: add a locally-applied hit to the cell's own contribution and return the new cumulative value (0 when the cell cannot be tracked — the live report then carries no contribution and the host applies the raw delta).</summary>
	public float AddLocalBlockDamage(int x, int y, float increment) => _guestReports.AddLocalDamage(x, y, increment);

	/// <summary>
	/// Either role: a block write landed on the cell (air, placement, restore) —
	/// the partial damage accounted for it belonged to the block that is gone, so
	/// the guest's outstanding contribution and every sender's ledger entry for
	/// the cell die with it. Without this a fresh block at the same cell would
	/// inherit the old one's accounting, and a contribution below the old value
	/// would resolve to "already accounted for" and never be applied.
	/// </summary>
	public void ForgetBlockDamageAccounting(int x, int y)
	{
		_guestReports.ForgetDamage(x, y);
		_damageLedger.Forget(x, y);
	}

	/// <summary>Guest only: a new world/layer baseline was applied — the previous world's outstanding contributions are dropped, and so is every ledger entry.</summary>
	public void ResetPendingBlockDamageReports()
	{
		_guestReports.ResetDamages();
		_damageLedger.Clear();
		_ledgerOverflowLogged = false;
	}

	/// <summary>Guest only: re-report every unacknowledged cell's absolute damage to the host (the fallback pump's action).</summary>
	public void ResendPendingBlockDamageReports() => _guestReports.ResendDamages();

	/// <summary>
	/// Host only: a guest reported the cells whose live delta this host never
	/// accounted for — and each row is that SENDER's own cumulative contribution,
	/// never the cell's total. Every row is resolved against the ledger and the
	/// difference is applied through the SAME path a live delta takes (apply →
	/// relay to the rest of the members → break handling), so the report cannot
	/// count a hit the ledger already holds and two senders add up instead of one
	/// being swallowed by a per-cell maximum. The reporter is then answered with
	/// this host's authoritative value for every reported cell — the answer, not
	/// the periodic snapshot, is what clears its outstanding entries.
	/// A report from another generation is refused before any of it runs: its rows
	/// name THIS side's cells with another world's damage, and the reporter's own
	/// boundary drops its pending set, so no answer is owed (an answer would write
	/// this generation's values into the reporter's older world).
	/// </summary>
	public void HandleBlockDamageReport(ulong sender, IReadOnlyList<BlockDamageEntryMsg> entries, WorldGenerationMsg? generation)
	{
		var relation = RelateReportGeneration(generation, "BlockDamageReport", sender);
		if (relation == WorldGenerationRelation.Stale)
		{
			return;
		}

		if (_session.Role != SessionRole.Host || !_session.SessionActive || entries.Count == 0)
		{
			return;
		}

		if (_nativeWorldFacts is null)
		{
			// No live world to read this host's own values from: an invented answer
			// would clear the reporter's entries against a table that does not
			// exist, so nothing is applied and nothing is answered — the report
			// stays outstanding for the next cycle.
			_log.LogDebug("[BlockSync] no native world-fact port is registered — {Peer}'s partial-damage report is neither applied nor answered.",
				sender);
			return;
		}

		var unaccounted = 0;
		foreach (var entry in entries)
		{
			if (AdmitRemoteContribution(sender, entry.X, entry.Y, entry.Damage, null, null, relation) is not { } increment || increment <= 0f)
			{
				continue;
			}

			unaccounted++;
			BlockDamagedReceived?.Invoke(sender, entry.X, entry.Y, increment, false, null, null, relation);
		}

		// The answer is read AFTER the applies above, so it carries what this host
		// holds now — the reporter's own contribution included. It goes to the
		// REPORTER only: a broadcast would clear every other member's outstanding
		// entry for those cells, and their contributions would never be reported.
		var rows = _nativeWorldFacts.CaptureBlockDamages();
		if (rows is null)
		{
			_log.LogWarning("[BlockSync] no live world to read this host's partial-damage rows from — {Peer}'s report is not answered and stays outstanding.",
				sender);
			return;
		}

		var answers = new List<BlockDamageEntryMsg>(entries.Count);
		foreach (var entry in entries)
		{
			answers.Add(new BlockDamageEntryMsg { X = entry.X, Y = entry.Y, Damage = ValueAt(rows, entry.X, entry.Y) });
		}

		_sender.Send(sender, NetMsg.BlockDamageSnapshot, new BlockDamageSnapshotMsg { Entries = answers, Generation = _generations.Stamp(), AnswersReport = true });
		_log.LogInformation("[BlockSync] accounted {Peer}'s partial-damage report ({Count} cell(s), {Unaccounted} unaccounted) and answered every reported cell.",
			sender, entries.Count, unaccounted);
	}

	/// <summary>This host's own value for one cell — 0 when it holds no row for it (which is a real answer: "no damage here").</summary>
	private static float ValueAt(IReadOnlyList<BlockDamageEntryMsg> rows, int x, int y)
	{
		foreach (var row in rows)
		{
			if (row.X == x && row.Y == y)
			{
				return row.Damage;
			}
		}

		return 0f;
	}
	/// <summary>The session that owned these reports is gone: every channel's unacknowledged set dies with it, so the next world cannot inherit the previous one's reports.</summary>
	internal void ResetPendingReports()
	{
		_guestReports.ResetBlocks();
		_guestReports.ResetDamages();
		_guestReports.ResetBreakDrops();
	}
}
