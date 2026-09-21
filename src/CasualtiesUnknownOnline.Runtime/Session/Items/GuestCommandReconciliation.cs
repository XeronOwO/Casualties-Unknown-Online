using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Guest-side item-command convergence (sync-coverage audit row I5). An item
/// command is a REPORT of a native action that already happened on this client:
/// the guest picked the item up, dropped it or destroyed it locally first, and
/// the command exists so the host's kernel learns the fact. The transport is
/// reliable, but only while the peer is reachable — the documented lazy-P2P
/// swallow window drops frames the sender never learns about, and an item
/// command had no re-report at all. A swallowed one therefore left the two sides
/// disagreeing about where the item is for the rest of the run: the guest held
/// it while the host's table still had it in the world (or the reverse), and
/// I3's world-item keyframe converges the HOST's table, never the guest's local
/// result, so nothing healed it short of a reconnect.
///
/// <para>
/// The fix is the absolute-report pattern this codebase already uses for the
/// world tables, applied to the command channel: the guest keeps each
/// unacknowledged command as a whole FRAME and re-sends that exact frame in a
/// bounded window (<see cref="IntervalMs"/> cadence, at most
/// <see cref="MaxReports"/> repeats per report, so a report lives about 60 s
/// after its own edge — the same order of magnitude as the host's 60 s repair
/// cycle, which re-baselines the world anyway), so the operation's identity is
/// preserved and the host's kernel answers a repeat from its own operation
/// window instead of committing twice (<c>GameStateKernel</c> returns the
/// original decision for a known <c>OperationId</c>, and the host re-broadcasts
/// that same committed batch). A report leaves the window on either verdict the
/// wire already carries: its own id in a committed batch
/// (<see cref="ClearCommitted"/>) or a refusal naming the item
/// (<see cref="ClearRejected"/>).
/// </para>
///
/// <para>
/// The outstanding reports are kept PER ITEM and IN SEND ORDER, and that
/// ordering is the mechanism, not a detail. One production frame reports two
/// commands on one item — the adapter's generation-time item report sends the
/// creation and the pickup that follows it back to back — and the creation is
/// the family's prerequisite: a lost creation makes every later report about
/// that item a refused protocol violation ("creation this host has never
/// judged") which no operation report can repair. A newest-wins slot would
/// therefore evict the very report the item cannot be recovered without. The
/// same holds one level down, for an item whose state a newer report does not
/// carry: a pickup behind a LOST drop is refused with <c>Conflict</c> ("item is
/// already carried") because the host still holds the older location. So every
/// unacknowledged report stays queued, an item's reports are re-sent in their
/// original order (creating before operating, dropping before re-picking), and
/// each report carries its own budget.
/// </para>
///
/// <para>
/// A refusal drops the queue's NEWEST report: the host answers in the order it
/// receives, so the newest outstanding report is the one a refusal most likely
/// judged, and the older reports stay outstanding so the host still converges to
/// the state the refusal left behind — the refusal of a pickup behind a lost
/// drop is followed by the drop's own re-report, which is what makes the two
/// sides agree again. A refusal of a creation (the host's tombstone then answers
/// every later report about that item) drains the queue one report per answer
/// until it is empty. The queue is capped at
/// <see cref="MaxPendingPerItem"/>; the oldest non-creation report is the one
/// dropped at the cap, because the newest reports are the ones carrying the
/// item's current state.
/// </para>
///
/// <para>
/// The window is BOUNDED rather than a permanent trickle: a report the host
/// never answers is named in a warning and stops being re-sent, which is the
/// accepted-loss discipline the session-control windows use. The budget is spent
/// only by reports that actually left this client
/// (<see cref="PacketSender.TrySend"/> reports the transport's verdict), and the
/// queue is dropped wholesale when the world baseline is replaced (a restored
/// checkpoint or a new session), because the baseline supersedes every in-flight
/// local operation. A session that ends and re-forms re-enters through the
/// host's checkpoint and its world-entry item snapshot, so a report swallowed
/// across that boundary converges through the re-baseline instead of through
/// this queue: it is deliberately NOT re-sent after a rejoin, and a diverging
/// local result the rejoin's item snapshot does not carry is the same declared
/// loss any uncommitted local action has at a session boundary.
/// </para>
///
/// <para>
/// One host branch deliberately IGNORES a report instead of refusing it: the
/// destroy of an item whose judged location is another player's carried item —
/// the admission seam's rule (<c>KernelCommandGateway.MayReportDestroyed</c>, the
/// handler's former <c>CanDestroy</c>) guarding against a remote display proxy
/// reporting its owner's real instance ids (an unknown item is refused before
/// that guard, and a world item may be destroyed by any peer, so the early
/// return fires only for that carried-by-another case). Such a report
/// has no verdict to wait for, so it spends its budget and ends in the
/// "not answered" warning. Answering it with a refusal was rejected: the guest's
/// rejection path rolls a pickup back, and that rollback would land on the
/// remote display clone the guard exists to reject.
/// </para>
/// </summary>
public sealed class GuestCommandReconciliation : ICuoService, IDisposable
{
	/// <summary>The re-report cadence: the first repeat goes out one interval after the report's own edge.</summary>
	internal const long IntervalMs = 5_000;

	/// <summary>Repeats per report (12 x 5 s ≈ 60 s), the same order as the host's own absolute resend cycle.</summary>
	internal const int MaxReports = 12;

	/// <summary>Outstanding reports kept per item — beyond it the oldest non-creation report is dropped.</summary>
	internal const int MaxPendingPerItem = 8;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ItemKernelAuthority _authority;
	private readonly ITimeSource _time;
	private readonly ILogger<GuestCommandReconciliation> _log;

	/// <summary>The unacknowledged reports, keyed by item instance id, each item's queue in SEND ORDER (the item's creation first — it is the report no later one can replace).</summary>
	private readonly Dictionary<ulong, List<PendingCommand>> _pending = [];

	/// <summary>One pump's view of the queue. A send can deliver a verdict INSIDE the send call (a transport that dispatches inline, as the simulation harness does), and that answer closes a report while the pump is walking the table — so the pump walks a snapshot, never the live collections.</summary>
	private readonly List<PendingCommand> _scan = [];

	/// <summary>Reports whose budget is spent, collected during the pump's iteration (a collection may not be mutated while it is enumerated).</summary>
	private readonly List<PendingCommand> _spent = [];

	public GuestCommandReconciliation(
		ISessionControl session,
		PacketSender sender,
		ItemKernelAuthority authority,
		ITimeSource time,
		ILogger<GuestCommandReconciliation> log)
	{
		_session = session;
		_sender = sender;
		_authority = authority;
		_time = time;
		_log = log;
		_session.SessionEnded += OnSessionEnded;
		_authority.CheckpointRestored += OnCheckpointRestored;
	}

	/// <summary>The reports still awaiting the host's verdict, across every item (the window's live size).</summary>
	internal int PendingCount
	{
		get
		{
			var total = 0;
			foreach (var queue in _pending.Values)
			{
				total += queue.Count;
			}

			return total;
		}
	}

	/// <summary>One item's outstanding reports, oldest first (the test surface for the queue's ordering).</summary>
	internal IReadOnlyList<WireCommandKind> PendingKindsFor(ulong itemId) =>
		_pending.TryGetValue(itemId, out var queue)
			? [.. queue.ConvertAll(pending => pending.Kind)]
			: [];

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Stop()
	{
	}

	void ICuoService.Update() => Pump(_time.NowMs);

	void IDisposable.Dispose()
	{
		_session.SessionEnded -= OnSessionEnded;
		_authority.CheckpointRestored -= OnCheckpointRestored;
	}

	/// <summary>
	/// A command frame is about to leave this client: queue it (with the exact frame,
	/// so the repeat carries the same <c>OperationId</c> and the same payload) until
	/// the host's verdict arrives. Registered BEFORE the send — a transport that
	/// dispatches inline can deliver a verdict inside the send call, and a verdict
	/// that arrived before the registration would be lost, leaving the window open on
	/// a report the host has already judged. A send the transport refuses is queued
	/// too, because the window's first repeat is what heals it.
	/// </summary>
	internal void Track(WireCommand command, ulong operationId, ProtocolFrame frame, WirePayloadType payloadType)
	{
		if (!IsReReported(payloadType) || command.Identity.InstanceId == 0)
		{
			return;
		}

		var itemId = command.Identity.InstanceId;
		if (!_pending.TryGetValue(itemId, out var queue))
		{
			queue = [];
			_pending[itemId] = queue;
		}

		if (queue.Count >= MaxPendingPerItem)
		{
			var dropped = DropOldestNonCreation(queue);
			_log.LogInformation("[ItemCommand] dropped the oldest queued report for item {ItemId} ({Kind}, operation {Operation}) — at most {Cap} reports are kept per item, and the newest ones carry its state.",
				itemId, dropped.Kind, dropped.OperationId, MaxPendingPerItem);
		}

		queue.Add(new PendingCommand(itemId, operationId, command.Kind, frame, _time.NowMs));
		_log.LogDebug("[ItemCommand] {Kind} on item {ItemId} (operation {Operation}) awaits the host's verdict — re-reported every {Interval} ms for at most {Max} report(s); {Queued} report(s) now outstanding for this item.",
			command.Kind, itemId, operationId, IntervalMs, MaxReports, queue.Count);
	}

	/// <summary>
	/// A committed batch arrived — if it is one of this guest's own reports, the host
	/// has judged it and it leaves the window. Observed at RECEIPT (before the batch is
	/// applied), so a batch the replay kernel already holds still closes the report.
	/// </summary>
	internal void ClearCommitted(ulong operationId)
	{
		var closedItem = 0ul;
		PendingCommand? closed = null;
		var emptied = false;
		foreach (var entry in _pending)
		{
			var index = entry.Value.FindIndex(pending => pending.OperationId == operationId);
			if (index < 0)
			{
				continue;
			}

			closedItem = entry.Key;
			closed = entry.Value[index];
			entry.Value.RemoveAt(index);
			emptied = entry.Value.Count == 0;
			break;
		}

		if (closed is null)
		{
			return;
		}

		if (emptied)
		{
			_pending.Remove(closedItem);
		}

		if (closed.Reports > 0)
		{
			_log.LogInformation("[ItemCommand] {Kind} on item {ItemId} converged after {Reports} re-report(s) — the host committed operation {Operation}.",
				closed.Kind, closedItem, closed.Reports, operationId);
		}
		else
		{
			_log.LogDebug("[ItemCommand] {Kind} on item {ItemId} converged on its first report (operation {Operation}).",
				closed.Kind, closedItem, operationId);
		}
	}

	/// <summary>
	/// The host refused a report about this item: the refusal is item-scoped on the
	/// wire (it carries the item id and the reason, never the operation id), so the
	/// NEWEST outstanding report for the item leaves the window — that is the one the
	/// host most likely judged — while the older reports keep their place, so the host
	/// still converges to the state the refusal left behind. This side's local result
	/// is the adapter's own to settle (the rejection reaches the item domain's
	/// rollback path as it always did).
	/// </summary>
	internal void ClearRejected(ulong itemId)
	{
		if (!_pending.TryGetValue(itemId, out var queue) || queue.Count == 0)
		{
			return;
		}

		var refused = queue[queue.Count - 1];
		queue.RemoveAt(queue.Count - 1);
		if (queue.Count == 0)
		{
			_pending.Remove(itemId);
		}

		_log.LogInformation("[ItemCommand] {Kind} on item {ItemId} (operation {Operation}) was refused by the host — it leaves the window after {Reports} re-report(s); {Remaining} older report(s) stay outstanding.",
			refused.Kind, itemId, refused.OperationId, refused.Reports, queue.Count);
	}

	/// <summary>Drop every unacknowledged report: the world baseline was replaced, so no in-flight local operation is still meaningful.</summary>
	private void ResetPending(string reason)
	{
		var count = PendingCount;
		if (count == 0)
		{
			return;
		}

		_log.LogInformation("[ItemCommand] dropped {Count} unacknowledged item report(s): {Reason}.", count, reason);
		_pending.Clear();
	}

	/// <summary>
	/// One frame of the window: re-send every queued report whose cadence elapsed, each
	/// item's queue in its original order. A report whose budget is spent is named and
	/// dropped (acceptance by design, never a silent trickle), and a cancelled send
	/// does not spend a repeat.
	/// </summary>
	private void Pump(long nowMs)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			ResetPending("this node is not an active guest session");
			return;
		}

		if (_pending.Count == 0)
		{
			return;
		}

		_spent.Clear();
		_scan.Clear();
		foreach (var queue in _pending.Values)
		{
			_scan.AddRange(queue);
		}

		foreach (var pending in _scan)
		{
			if (nowMs < pending.WindowMs)
			{
				// The clock went backwards (Environment.TickCount wraps every ~24.9
				// days) — re-arm at the new reading instead of stalling on the old one.
				pending.WindowMs = nowMs;
				continue;
			}

			if (nowMs - pending.WindowMs < IntervalMs)
			{
				continue;
			}

			if (pending.Reports >= MaxReports)
			{
				_spent.Add(pending);
				continue;
			}

			pending.WindowMs = nowMs;
			if (!_sender.TrySend(_session.HostSteamId, NetMsg.KernelEnvelope, pending.Frame))
			{
				continue; // nothing left this client — a report that never went out must not spend the window
			}

			pending.Reports++;
			_log.LogInformation("[ItemCommand] re-reported {Kind} on item {ItemId} to the host ({Reports}/{Max}) — still awaiting the verdict.",
				pending.Kind, pending.ItemId, pending.Reports, MaxReports);
		}

		foreach (var pending in _spent)
		{
			if (!_pending.TryGetValue(pending.ItemId, out var queue) || !queue.Remove(pending))
			{
				// The report already left the window inside a send (its verdict arrived
				// inline) — it is not ours to drop.
				continue;
			}

			if (queue.Count == 0)
			{
				_pending.Remove(pending.ItemId);
			}

			_log.LogWarning("[ItemCommand] {Kind} on item {ItemId} (operation {Operation}) was not answered by the host after {Reports} re-report(s): this side keeps its local result while the host's copy keeps its own, until the next report about this item or a world re-entry.",
				pending.Kind, pending.ItemId, pending.OperationId, pending.Reports);
		}
	}

	private void OnSessionEnded() => ResetPending("the session ended");

	private void OnCheckpointRestored(GameCheckpoint checkpoint) =>
		ResetPending($"the world baseline was restored at revision {checkpoint.GlobalRevision}");

	/// <summary>
	/// Drops the queue's oldest report, preferring a non-creation one: the item's
	/// creation is the prerequisite no later report can replace, so it is only dropped
	/// when it is the only report left to drop (a queue of creations cannot happen —
	/// one item is created once).
	/// </summary>
	private static PendingCommand DropOldestNonCreation(List<PendingCommand> queue)
	{
		var index = queue.FindIndex(pending => !IsCreation(pending.Kind));
		if (index < 0)
		{
			index = 0;
		}

		var dropped = queue[index];
		queue.RemoveAt(index);
		return dropped;
	}

	/// <summary>
	/// The reports this window owns: a guest item report whose native local action has
	/// ALREADY happened, so a swallowed frame leaves the two copies of the game
	/// disagreeing about a fact the host is the world authority for. The creation
	/// report is included because it is the family's prerequisite — a lost creation
	/// makes every later report about that item a refused protocol violation, so
	/// re-reporting the operations alone could not heal the run.
	/// <c>ItemUpdateState</c> and <c>ItemContainerSync</c> are deliberately absent:
	/// their facts converge through the absolute fallbacks the audit matrix records for
	/// rows I7/I4 (the 1 Hz character snapshot, the 5 s world-item keyframe) — and
	/// <c>ItemUpdateState</c> must stay out for a second reason, because its own host
	/// heal adopts an unknown carried id as the reporter's own
	/// (<c>KernelProtocolCommandHandler.HandleMissingCarriedUpdate</c>), so queueing it
	/// would evict the item's creation report exactly the way the single-slot window
	/// this class replaced used to. <c>ItemTransfer</c> needs no entry either — the kind
	/// has no sender-side emission in this tree (the validator, the wire mapper and the
	/// host's guard name it, no guest path sends it), so there is no report of it to
	/// re-send.
	/// </summary>
	private static bool IsReReported(WirePayloadType payloadType) => payloadType switch
	{
		WirePayloadType.ItemSpawnCommand => true,
		WirePayloadType.ItemPickupCommand => true,
		WirePayloadType.ItemDropCommand => true,
		WirePayloadType.ItemDestroyCommand => true,
		_ => false,
	};

	/// <summary>The item's creation report — the one report no later report about the item can stand in for.</summary>
	private static bool IsCreation(WireCommandKind kind) => kind == WireCommandKind.ItemSpawn;

	/// <summary>One unacknowledged report: the exact frame that was sent (so the repeat is the same operation), its cadence state and its spent budget.</summary>
	private sealed class PendingCommand(ulong itemId, ulong operationId, WireCommandKind kind, ProtocolFrame frame, long windowMs)
	{
		internal ulong ItemId { get; } = itemId;

		internal ulong OperationId { get; } = operationId;

		internal WireCommandKind Kind { get; } = kind;

		internal ProtocolFrame Frame { get; } = frame;

		/// <summary>When the current cadence window started (or the last re-report went out).</summary>
		internal long WindowMs { get; set; } = windowMs;

		/// <summary>Re-reports that actually left this client.</summary>
		internal int Reports { get; set; }
	}
}
