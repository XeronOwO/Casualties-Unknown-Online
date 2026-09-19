using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The GUEST's unacknowledged break-DROP bookkeeping (sync-coverage audit W1's
/// drop half), the third sibling of <see cref="GuestBlockReportBookkeeping"/>
/// (the block STATE) and <see cref="GuestBlockDamageReportBookkeeping"/> (the
/// partial DAMAGE): the three ride different messages and are answered by
/// different host facts, so they keep different tables. This type owns the
/// pending table and the one-per-overflow-episode log latch; the message surface
/// (<see cref="WorldStateMessageService"/>) only sends and answers.
///
/// The rules it must keep exactly (they are what the drop recovery is):
/// the break's drops are recorded BEFORE the live report is sent; a second break
/// at the same cell appends to the outstanding set; an entry is dropped when the
/// host relays the break carrying its drops (the accepted relay's echo — the W1
/// answer shape) or when a single drop was answered another way (an
/// <c>ItemReject</c> refusal, a local object that is gone); the table survives a
/// reconnect-while-in-world and is cleared only when a new world/layer baseline
/// is applied or the session ends.
/// </summary>
internal sealed class GuestBreakDropReportBookkeeping(ILogger<WorldService> log)
{
	private readonly PendingBreakDropTable _table = new();
	private bool _overflowLogged;

	/// <summary>How many unacknowledged drop sets are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int Count => _table.Count;

	/// <summary>The unacknowledged drop sets to re-report, in table order — every entry is an independent idempotent report.</summary>
	internal IReadOnlyList<PendingBreakDrops> Entries => _table.Entries;

	/// <summary>
	/// Record the break's drops BEFORE the live report is sent: a send that never
	/// lands is exactly what the fallback exists for. A cell the cap refuses is
	/// logged once per overflow episode, never dropped silently.
	/// </summary>
	internal void Report(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_table.Report(x, y, drops, buildingDrops))
		{
			return;
		}

		if (_overflowLogged)
		{
			return;
		}

		_overflowLogged = true;
		log.LogWarning(
			"[BlockSync] pending break-drop table is full ({Cap} cells) — new cells are not re-reported until the host answers (cell ({X},{Y}) dropped).",
			_table.Cap, x, y);
	}

	/// <summary>
	/// Guest: the host answered for this cell — its relay carries the break's
	/// drops, which is the acknowledgement of exactly those items. An entry is
	/// done once every one of its items has been named (a partial relay leaves the
	/// rest outstanding).
	/// </summary>
	internal void Answer(int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (!_table.Answer(x, y, drops, buildingDrops))
		{
			return;
		}

		EpisodeEnded();
		log.LogDebug("[BlockSync] host relayed the break at ({X},{Y}) with its drops — dropped the pending drop report ({Remaining} left).",
			x, y, _table.Count);
	}

	/// <summary>A single drop left the outstanding set (the host refused it, or the local object is gone) — the break must not be re-reported forever for an item that no longer exists.</summary>
	internal void ForgetItem(ulong itemId)
	{
		if (!_table.ForgetItem(itemId))
		{
			return;
		}

		EpisodeEnded();
		log.LogDebug("[BlockSync] drop {ItemId} left the pending break-drop set ({Remaining} left).", itemId, _table.Count);
	}

	/// <summary>True while <paramref name="itemId"/> is one of the drops an unacknowledged break is still waiting to have answered (the item keyframe's reconcile asks this before killing a locally-created drop).</summary>
	internal bool IsPending(ulong itemId) => _table.IsPending(itemId);

	/// <summary>The world these reports belonged to is gone (a new world/layer baseline, or the session ended) — every pending report dies with it.</summary>
	internal void Reset()
	{
		if (_table.Count > 0)
		{
			log.LogInformation(
				"[BlockSync] cleared {Count} pending break-drop report(s): they belonged to the previous world.",
				_table.Count);
			_table.Clear();
		}

		_overflowLogged = false;
	}

	private void EpisodeEnded()
	{
		if (_table.Count == 0)
		{
			_overflowLogged = false; // the overflow episode ended — a later fill must log again
		}
	}
}
