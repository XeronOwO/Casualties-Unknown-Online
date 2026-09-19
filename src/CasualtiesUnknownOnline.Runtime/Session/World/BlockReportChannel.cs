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

	/// <summary>
	/// The generation every direct world report of this family is stamped with, and
	/// the one a received report is compared against: the kernel run baseline. The
	/// stamp is what tells a stale previous-layer report apart from a legitimate
	/// one, and the comparison is the single place "is this report about MY world?"
	/// is answered.
	/// </summary>
	private readonly KernelWorldGenerationSource _generations = generations;

	private readonly GuestReportRecovery _guestReports = new(session, sender, nativeWorldFacts, generations, log);

	private readonly BlockDamageSnapshotSender _blockDamageSnapshot = new(session, sender, nativeWorldFacts, generations, log);

	// ---- Block damage (late-joiner backfill) ----

	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived;

	/// <summary>
	/// Compare a received direct world report's generation stamp with this side's
	/// own generation, logging every STALE verdict with both sides of the
	/// comparison (kind, sender, reported generation, current generation). A
	/// refusal of this kind must be distinguishable in the field from a
	/// first-writer loss or a malformed report, so it is never silent; an
	/// UNKNOWN verdict (no stamp, or no committed run baseline here) is not stale
	/// and leaves the caller's pre-stamp behaviour in place.
	/// </summary>
	private WorldGenerationRelation RelateReportGeneration(WorldGenerationMsg? generation, string kind, ulong sender)
	{
		var relation = WorldReportGeneration.Relate(_generations.Current, generation);
		if (relation == WorldGenerationRelation.Stale)
		{
			_log.LogWarning("[WorldReportGeneration] {Kind} from {Sender} belongs to {Reported} while this side is at {Current} — the report is about another world generation and is refused as stale.",
				kind, sender, WorldReportGeneration.Describe(generation), WorldReportGeneration.Describe(_generations.Current));
		}

		return relation;
	}

	/// <summary>
	/// Guest: the host's partial-damage snapshot arrived — it is also the ANSWER
	/// to every outstanding absolute re-report whose cell it names (the
	/// world-entry / 60 s snapshot and the per-report answer share this message),
	/// so those pending entries are done. A snapshot from another generation is
	/// refused before either half runs: its rows are keyed by block cell, so
	/// applying them would write another layer's damage onto this side's cells.
	/// </summary>
	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries, WorldGenerationMsg? generation)
	{
		if (RelateReportGeneration(generation, "BlockDamageSnapshot", _session.HostSteamId) == WorldGenerationRelation.Stale)
		{
			return;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_guestReports.AnswerDamage(entries);
		}

		BlockDamageSnapshotReceived?.Invoke(entries);
	}

	/// <summary>Host only: send the partial damage the GAME's own list holds (see <see cref="BlockDamageSnapshotSender"/>).</summary>
	public void SendBlockDamageSnapshot(ulong targetSteamId) => _blockDamageSnapshot.Send(targetSteamId);

	public event Action<ulong, NetVector2, float, bool, IReadOnlyList<BlockDropEntryMsg>?, IReadOnlyList<TrapDropEntryMsg>?, WorldGenerationRelation>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, WorldGenerationMsg? generation)
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
		BlockDamagedReceived?.Invoke(sender, pos, damage, metalBonus, drops, buildingDrops,
			RelateReportGeneration(generation, "BlockDamaged", sender));
	}

	public event Action<ulong, int, int, ushort, WorldGenerationRelation>? BlockPlacedReceived;

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block, WorldGenerationMsg? generation)
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

		OnBlockPlacedReceived(sender, x, y, block, relation);
	}

	/// <summary>Guest: a block was placed locally — record it as an unacknowledged pending report and send it (the host arbitrates + answers; a swallowed report is re-reported by the fallback).</summary>
	public void SendBlockPlacedReport(int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_guestReports.ReportBlock(x, y, block);
		_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
			new BlockPlacedMsg { X = x, Y = y, Block = block, Generation = _generations.Stamp() });
	}

	public void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockPlacedMsg { X = x, Y = y, Block = block, Generation = _generations.Stamp() };
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockPlaced, msg);
	}

	public void SendBlockDamaged(NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
			MetalBonus = metalBonus,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
			BuildingDrops = buildingDrops is { Count: > 0 } ? [.. buildingDrops] : null,
			Generation = _generations.Stamp(),
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BlockDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockDamaged, msg);
		}
	}

	public void BroadcastBlockDamaged(ulong excludeSteamId, NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
			MetalBonus = metalBonus,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
			BuildingDrops = buildingDrops is { Count: > 0 } ? [.. buildingDrops] : null,
			Generation = _generations.Stamp(),
		};
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockDamaged, msg);
	}

	// ---- Guest report recovery (audit gaps W1/W2) ----
	// The re-send/answer WIRE logic lives in GuestReportRecovery; the recovery
	// STATE lives in its two bookkeeping collaborators. These members are only
	// the surface WorldService and the handlers call.

	/// <summary>How many unacknowledged guest block reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockReportCount => _guestReports.PendingBlockCount;

	/// <summary>Guest only: re-report every unacknowledged block mutation to the host (the fallback pump's action).</summary>
	public void ResendPendingBlockReports() => _guestReports.ResendBlocks();

	/// <summary>Host only: answer a guest's refused block report with the host's authoritative block at that cell.</summary>
	public void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block) =>
		_guestReports.SendBlockPlacedCorrection(targetSteamId, x, y, block);

	/// <summary>Guest: the host answered for this cell (relay or correction) — its value is authoritative, the pending report is done.</summary>
	private void OnBlockPlacedReceived(ulong sender, int x, int y, ushort block, WorldGenerationRelation generation)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_guestReports.AnswerBlock(x, y);
		}

		BlockPlacedReceived?.Invoke(sender, x, y, block, generation);
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

	/// <summary>Guest only: record the cell's current ABSOLUTE damage before the live delta report goes out (the fallback's re-report source).</summary>
	public void ReportBlockDamage(int x, int y, float damage) => _guestReports.ReportDamage(x, y, damage);

	/// <summary>Either role: the cell went air — its pending partial-damage report dies with the block.</summary>
	public void ForgetPendingBlockDamage(int x, int y) => _guestReports.ForgetDamage(x, y);

	/// <summary>Guest only: a new world/layer baseline was applied — the previous world's pending partial-damage reports are dropped.</summary>
	public void ResetPendingBlockDamageReports() => _guestReports.ResetDamages();

	/// <summary>Guest only: re-report every unacknowledged cell's absolute damage to the host (the fallback pump's action).</summary>
	public void ResendPendingBlockDamageReports() => _guestReports.ResendDamages();

	/// <summary>Host only: merge a guest's absolute partial-damage report and answer every reported cell authoritatively. A report from another generation is refused before the merge: its rows name THIS side's cells with another world's damage, and the reporter's own boundary drops its pending set, so no answer is owed (an answer would write this generation's values into the reporter's older world).</summary>
	public void HandleBlockDamageReport(ulong sender, IReadOnlyList<BlockDamageEntryMsg> entries, WorldGenerationMsg? generation)
	{
		if (RelateReportGeneration(generation, "BlockDamageReport", sender) == WorldGenerationRelation.Stale)
		{
			return;
		}

		_guestReports.HandleDamageReport(sender, entries);
	}
	/// <summary>The session that owned these reports is gone: every channel's unacknowledged set dies with it, so the next world cannot inherit the previous one's reports.</summary>
	internal void ResetPendingReports()
	{
		_guestReports.ResetBlocks();
		_guestReports.ResetDamages();
		_guestReports.ResetBreakDrops();
	}
}
