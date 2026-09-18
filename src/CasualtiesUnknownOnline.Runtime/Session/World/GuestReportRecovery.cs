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
	INativeWorldFacts? nativeWorldFacts,
	ILogger<WorldService> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly INativeWorldFacts? _nativeWorldFacts = nativeWorldFacts;
	private readonly ILogger<WorldService> _log = log;

	/// <summary>The W1 state: block cell → the block this side wrote there, unacknowledged by the host.</summary>
	private readonly GuestBlockReportBookkeeping _blocks = new(log);

	/// <summary>The W2 state: block cell → the absolute partial damage this side holds, unacknowledged by the host.</summary>
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
				new BlockPlacedMsg { X = entry.X, Y = entry.Y, Block = entry.Block });
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

		_sender.Send(targetSteamId, NetMsg.BlockPlaced, new BlockPlacedMsg { X = x, Y = y, Block = block });
		_log.LogDebug("[BlockSync] answered {Peer}'s report at ({X},{Y}) with the authoritative block {Block}.",
			targetSteamId, x, y, block);
	}

	// ---- W2: unacknowledged partial-damage reports ----

	/// <summary>How many unacknowledged guest partial-damage reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingDamageCount => _damages.Count;

	/// <summary>
	/// Guest only: record the cell's current ABSOLUTE damage BEFORE the live delta
	/// report is sent. The live report stays a delta (the receiver accumulates it);
	/// this record is what the fallback re-sends as an absolute value, so a
	/// swallowed report is healed without double-applying. The value is the cell's
	/// accumulated damage on THIS side (the game's own <c>BlockDamage.damage</c>),
	/// which already includes every relay this side received.
	/// </summary>
	internal void ReportDamage(int x, int y, float damage)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_damages.Report(x, y, damage);
	}

	/// <summary>
	/// Either role: an air write landed on the cell (local break, remote break,
	/// earthquake/environment) — a broken block's partial damage is carried by the
	/// block-state channel, so the pending damage report dies with the block. A
	/// no-op outside a guest's own pending set.
	/// </summary>
	internal void ForgetDamage(int x, int y) => _damages.Forget(x, y);

	/// <summary>
	/// Guest: the host's partial-damage snapshot arrived — it is also the ANSWER
	/// to every outstanding absolute re-report whose cell it names (the
	/// world-entry / 60 s snapshot and the per-report answer share this message),
	/// so those pending entries are done.
	/// </summary>
	internal void AnswerDamage(IReadOnlyList<BlockDamageEntryMsg> entries) => _damages.Answer(entries);

	/// <summary>The world these reports belonged to is gone — every pending partial-damage report dies with it.</summary>
	internal void ResetDamages() => _damages.Reset();

	/// <summary>
	/// Guest only: re-report every unacknowledged cell's ABSOLUTE damage to the
	/// host — one <see cref="NetMsg.BlockDamageReport"/> carrying the whole
	/// outstanding set (one operation, one message). The host merges per cell and
	/// answers with its authoritative value for every reported cell (the existing
	/// <see cref="NetMsg.BlockDamageSnapshot"/>), which is what clears the pending
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
			new BlockDamageSnapshotMsg { Entries = [.. _damages.Entries] });
		_log.LogInformation("[BlockSync] re-reported {Count} unacknowledged partial-damage cell(s) to the host.",
			_damages.Count);
	}

	/// <summary>
	/// Host only: a guest's ABSOLUTE partial-damage report arrived. Merge it into
	/// the GAME's own damage list through the native port — per cell, never below
	/// what this host already holds, so two players' contributions and this host's
	/// own all count — and broadcast this host's authoritative value for EVERY
	/// reported cell. A zero answer means "no damage here" (the cell is air, the
	/// row is out of range, or the game's own list is full): the reporter clears
	/// its local crack, so a refused report converges instead of re-reporting
	/// forever. The broadcast includes the reporter — that is its acknowledgement.
	/// </summary>
	internal void HandleDamageReport(ulong sender, IReadOnlyList<BlockDamageEntryMsg> entries)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || entries.Count == 0)
		{
			return;
		}

		if (_nativeWorldFacts is null)
		{
			_log.LogDebug("[BlockSync] no native world-fact port is registered — {Peer}'s partial-damage report is neither merged nor answered.",
				sender);
			return;
		}

		var authoritative = _nativeWorldFacts.MergeBlockDamages(entries);
		if (authoritative is null)
		{
			// No live world to merge into: answering with an invented set would
			// clear the reporter's pending entry against a table that does not
			// exist, so the report stays outstanding for the next cycle.
			_log.LogWarning("[BlockSync] no live world to merge {Peer}'s partial-damage report into — the report is not answered.",
				sender);
			return;
		}

		_session.Broadcast(NetMsg.BlockDamageSnapshot, new BlockDamageSnapshotMsg { Entries = [.. authoritative] });
		_log.LogInformation("[BlockSync] merged {Peer}'s partial-damage report ({Count} cell(s)) and answered with {Answered} authoritative value(s).",
			sender, entries.Count, authoritative.Count);
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
	internal void ReportBreakDrops(int x, int y, float posX, float posY, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_breakDrops.Report(x, y, posX, posY, drops, buildingDrops);
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
				Position = new NetVector2(entry.PosX, entry.PosY).ToNetVector2Msg(),
				Damage = 0f,
				MetalBonus = false,
				Drops = entry.Drops.Count > 0 ? [.. entry.Drops] : null,
				BuildingDrops = entry.BuildingDrops.Count > 0 ? [.. entry.BuildingDrops] : null,
			});
		}

		_log.LogInformation("[BlockSync] re-reported {Count} unacknowledged break-drop set(s) to the host.",
			_breakDrops.Count);
	}
}
