using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest-side report recovery's WIRE half: the two unacknowledged-report
/// channels — the block state (sync-coverage audit W1) and the partial block
/// damage (W2) — from the reporter's re-send to the host's answer. The recovery
/// STATE lives in the two bookkeeping collaborators
/// (<see cref="GuestBlockReportBookkeeping"/> and
/// <see cref="GuestBlockDamageReportBookkeeping"/>); this type owns when a report
/// goes out and what an answer does, so <see cref="WorldStateMessageService"/>
/// stays a message surface instead of growing a third responsibility. The
/// fallback's clock is <see cref="WorldReportFallbackPump"/> and its cadence
/// policy is <see cref="PendingReportFallback"/>, shared by both channels — each
/// table keeps its own window because they fill and drain independently.
/// </summary>
internal sealed class GuestReportRecovery(
	ISessionControl session,
	PacketSender sender,
	KernelWorldGenerationSource generations,
	ILogger<WorldService> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;

	/// <summary>
	/// The world/layer generation every report here is stamped with, read LIVE at
	/// send time. A fallback re-report therefore carries this side's CURRENT
	/// generation, which is the correct one by construction: the generation
	/// boundary drops every pending entry (see <see cref="ResetBlocks"/> and the
	/// damage/drop counterparts), so a surviving entry always belongs to the world
	/// this side is simulating now.
	/// </summary>
	private readonly KernelWorldGenerationSource _generations = generations;

	/// <summary>The W1 state: block cell → the block this side wrote there, unacknowledged by the host.</summary>
	private readonly GuestBlockReportBookkeeping _blocks = new(log);

	/// <summary>The W2 state: block cell → the damage THIS side has applied to it, and whether the host has answered for it since the last report.</summary>
	private readonly GuestBlockDamageReportBookkeeping _damages = new(log);

	/// <summary>The W1 drop half's state: block cell → the break's locally-created drops, unacknowledged by the host (the host learns a guest's break drops ONLY from the report, so their loss is an item-domain divergence the keyframe cannot heal).</summary>
	private readonly GuestBreakDropReportBookkeeping _breakDrops = new(log);

	// ---- W1: unacknowledged block-state reports ----

	/// <summary>How many unacknowledged guest block reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockCount => _blocks.Count;

	/// <summary>Guest: record the cell BEFORE its live report is sent — a send that never lands is exactly what the fallback exists for.</summary>
	internal void ReportBlock(int x, int y, ushort block) => _blocks.Report(x, y, block);

	/// <summary>Guest: the host answered for this cell (relay echo or correction) — its value is authoritative, the pending report is done.</summary>
	internal void AnswerBlock(int x, int y) => _blocks.Answer(x, y);

	/// <summary>The world these reports belonged to is gone — every pending block report dies with it.</summary>
	internal void ResetBlocks() => _blocks.Reset();

	/// <summary>
	/// Guest only: re-report every unacknowledged block mutation to the host.
	/// Each entry is one <see cref="NetMsg.BlockPlaced"/> report, so the host's
	/// existing first-writer-wins arbitration handles it unchanged (idempotent
	/// — a cell the host already agrees with is answered, not re-applied).
	/// Called by the fallback pump; a no-op when nothing is outstanding, when
	/// this side is not a guest, or when the session ended.
	/// </summary>
	internal void ResendBlocks()
	{
		if (!HasReportWork(_blocks.Count))
		{
			return;
		}

		foreach (var entry in _blocks.Entries)
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
				new BlockPlacedMsg { X = entry.X, Y = entry.Y, Block = entry.Block, Generation = _generations.Stamp() });
		}

		_log.LogInformation("[BlockSync] re-reported {Count} unacknowledged block mutation(s) to the host.",
			_blocks.Count);
	}

	/// <summary>
	/// Host only: answer a guest's block report with the host's authoritative
	/// block at that cell. Sent when the report was refused (first-writer-wins:
	/// the host's cell stands) — the reporter applies the correction and drops
	/// its pending entry. An ACCEPTED report needs no correction: its relay now
	/// includes the reporter (the echo is the acknowledgement).
	/// </summary>
	internal void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || targetSteamId == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.BlockPlaced, new BlockPlacedMsg { X = x, Y = y, Block = block, Generation = _generations.Stamp() });
		_log.LogDebug("[BlockSync] answered {Peer}'s report at ({X},{Y}) with the authoritative block {Block}.",
			targetSteamId, x, y, block);
	}

	// ---- W2: unaccounted partial-damage contributions ----

	/// <summary>How many cells are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingDamageCount => _damages.Count;

	/// <summary>
	/// Guest only: add a locally-applied hit to the cell's own contribution,
	/// BEFORE the live delta report goes out (a send that never lands is exactly
	/// what the fallback exists for), and return the new cumulative value for the
	/// wire. The value is what THIS SIDE has applied to the cell — the host
	/// accounts damage per sender, so a report carrying the cell's total would
	/// merge two senders into one number and could only ever take their maximum
	/// (review/partial-damage-delta-report-overlap). 0 = the cell could not be
	/// tracked (the table's cap): the live report then carries no contribution and
	/// the host applies its raw damage, which is the pre-ledger behaviour.
	/// </summary>
	internal float AddLocalDamage(int x, int y, float increment)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return 0f;
		}

		return _damages.Add(x, y, increment);
	}

	/// <summary>
	/// Either role: a block write landed on the cell (air, placement, restore) —
	/// the contribution belonged to the block that is gone, so the outstanding
	/// entry dies with it. A no-op outside a guest's own table.
	/// </summary>
	internal void ForgetDamage(int x, int y) => _damages.Forget(x, y);

	/// <summary>
	/// Guest: the host's answer arrived — the cells it names are accounted for, so
	/// their entries are no longer outstanding. The cumulative value STAYS (the
	/// next hit at that cell reports cumulative + its increment, and the host
	/// resolves the difference against the ledger it now holds).
	/// </summary>
	internal void AnswerDamage(IReadOnlyList<BlockDamageEntryMsg> entries) => _damages.Answer(entries);

	/// <summary>The world these contributions belonged to is gone — every entry dies with it.</summary>
	internal void ResetDamages() => _damages.Reset();

	/// <summary>
	/// Guest only: re-report every outstanding cell's own cumulative contribution
	/// to the host — one <see cref="NetMsg.BlockDamageReport"/> carrying the whole
	/// set (one operation, one message). The host resolves each row against its
	/// ledger, so a row it already accounts for changes nothing, and answers with
	/// its own value for every reported cell — that answer is what clears the
	/// entry. Called by the fallback pump; a no-op when nothing is outstanding,
	/// when this side is not a guest, or when the session ended.
	/// </summary>
	internal void ResendDamages()
	{
		if (!HasReportWork(_damages.Count))
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.BlockDamageReport,
			new BlockDamageSnapshotMsg { Entries = [.. _damages.Outstanding], Generation = _generations.Stamp() });
		_log.LogInformation("[BlockSync] re-reported {Count} unaccounted partial-damage cell(s) to the host.",
			_damages.Count);
	}

	/// <summary>Guest, in session, something outstanding — the shape both re-sends need.</summary>
	private bool HasReportWork(int pendingCount) =>
		_session.Role == SessionRole.Guest && _session.SessionActive && pendingCount > 0;

	// ---- W1's drop half: unacknowledged break drops ----

	/// <summary>How many unacknowledged break-drop sets are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBreakDropCount => _breakDrops.Count;

	/// <summary>
	/// Guest only: record the break's locally-created drops BEFORE the live
	/// <c>BlockDamaged</c> report is sent. The host registers a guest's break
	/// drops exclusively from that message, so a swallowed one leaves the
	/// breaker's items unknown to the authoritative table and the item keyframe
	/// has no fact to reconcile them from — this record is the fallback's source.
	/// </summary>
	internal void ReportBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_breakDrops.Report(x, y, drops, buildingDrops);
	}

	/// <summary>
	/// Guest: the host relayed a break for this cell — its payload names the drops
	/// it registered, so every one of them is answered (the accepted relay is the
	/// acknowledgement, exactly as the W1 block echo is for the air write).
	/// </summary>
	internal void AnswerBreakDrops(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_breakDrops.Answer(x, y, drops, buildingDrops);
	}

	/// <summary>Guest: one drop of an outstanding break is answered another way (the host refused it, or the local object is gone) — the break must not be re-reported forever for an item that no longer exists.</summary>
	internal void ForgetBreakDrop(ulong itemId)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_breakDrops.ForgetItem(itemId);
	}

	/// <summary>Guest: true while this side waits for the host to answer a break carrying <paramref name="itemId"/> — the item keyframe's reconcile consults this before killing a locally-created drop (the 5-30 s keyframe is far inside the fallback's window, so an unacknowledged drop must survive it).</summary>
	internal bool IsBreakDropPending(ulong itemId) => _breakDrops.IsPending(itemId);

	/// <summary>The world these reports belonged to is gone — every pending break-drop report dies with it.</summary>
	internal void ResetBreakDrops() => _breakDrops.Reset();

	/// <summary>
	/// Guest only: re-report every unacknowledged break's drops to the host — one
	/// <c>BlockDamaged</c> per outstanding break, in the message shape the live
	/// report uses, so the host's existing arbitration and its idempotent per-item
	/// registration handle it unchanged. The host's relay of that message is what
	/// clears the entry. Called by the fallback pump; a no-op when nothing is
	/// outstanding, when this side is not a guest, or when the session ended.
	/// </summary>
	internal void ResendBreakDrops()
	{
		if (!HasReportWork(_breakDrops.Count))
		{
			return;
		}

		foreach (var entry in _breakDrops.Entries)
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockDamaged, new BlockDamagedMsg
			{
				X = entry.X,
				Y = entry.Y,
				Damage = 0f,
				MetalBonus = false,
				Drops = entry.Drops.Count > 0 ? [.. entry.Drops] : null,
				BuildingDrops = entry.BuildingDrops.Count > 0 ? [.. entry.BuildingDrops] : null,
				Generation = _generations.Stamp(),
			});
		}

		_log.LogInformation("[BlockSync] re-reported {Count} unacknowledged break-drop set(s) to the host.",
			_breakDrops.Count);
	}
}
