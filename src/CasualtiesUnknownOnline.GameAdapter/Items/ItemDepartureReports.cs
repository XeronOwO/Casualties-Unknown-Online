using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using CommitStatus = CasualtiesUnknownOnline.GameAdapter.Items.ItemReportCommitter.CommitStatus;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The departure side of the world-item report: a body drop, a throw, and the one-frame flush that
/// turns either into the report the host judges. Split out of <see cref="ItemWorldSync"/> (gate-driven —
/// the type's aggregate line budget), so the creation claim below can live next to the operation it
/// protects.
///
/// <para>
/// The claim is the point of this class. A departure may name an object that had no instance id yet —
/// a generation-time item picked up inside the dense window, then dropped a moment later. The report
/// sites used to mint that id and send only the departure, so the host was asked to judge an operation
/// on an identity it had never judged the creation of, and refused it as a protocol violation
/// (measured 2026-11-25, batch <c>20261125</c>: <c>ItemDestroy … whose creation this host has never
/// judged</c>, minted by the watermark line one log line earlier, while the carried statement that
/// owns a carried item's creation arrived afterwards and named nothing). The pickup path already makes
/// the same statement (<c>PickupSync</c>'s "spawn-then-pickup": the spawn binds the peer's same-spot
/// object, then the operation moves it). A departure is that shape with the operation being the drop.
/// </para>
/// </summary>
internal sealed class ItemDepartureReports(
	ISessionControl session,
	IItemControl items,
	DropProtectionGuard guard,
	ItemDropState dropState,
	OperationTrace trace,
	ItemReportCommitter reports,
	ItemIdAllocator ids,
	ILogger<ItemDepartureReports> log)
{
	private readonly ISessionControl _session = session;
	private readonly IItemControl _items = items;
	private readonly DropProtectionGuard _guard = guard;
	private readonly ItemDropState _dropState = dropState;
	private readonly OperationTrace _trace = trace;
	private readonly ItemReportCommitter _reports = reports;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger<ItemDepartureReports> _log = log;

	/// <summary>True while a REPLAY of a peer's fact is being applied — the local departure hooks must stay silent (call identity lives in CallContext, not a bool).</summary>
	private static bool IsReplayedFact => CallContext.IsReplayedRemoteFact;

	/// <summary>True while a departure report is REGISTERED, NOT yet sent, and still OWED — the query the two re-report
	/// entry points ask through <see cref="ItemWorldSync"/>: a body drop's carrier holds it for one frame so the game's
	/// own <c>DropItem</c> → <c>ThrowItem</c> pair can set the final velocity, and a container unload's waits for the rest
	/// of its bracket (a container load re-homes the item and reports that move itself). An inventory snapshot that reaches
	/// the peers first makes them read the change as a move without an event ("left the inventory without an event sync",
	/// batch `20261005-e`).</summary>
	internal bool HasPendingDropReport => _dropState.HasReportOwed;

	/// <summary>The world was left (scene switch / session end) — a pending departure cannot resolve anymore; cancel it so the operation trace stays balanced. Its own per-call set: the reset can run from a report's own call stack, the shape the flush's comment argues against for shared buffers.</summary>
	internal void ResetPending()
	{
		var cancelled = new List<DropPendingState.Pending>();
		_dropState.ResetAll(cancelled);
		foreach (var departure in cancelled)
		{
			_trace.End(departure.Op, departure.ItemId, "ResetPending", "Cancelled", "WorldLeft");
		}
	}

	internal void OnItemDropped(Item item)
	{
		if (IsReplayedFact || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var claimed = HasAnId(item);
		var itemId = _ids.EnsureId(item);
		if (itemId == 0)
		{
			return;
		}

		if (!claimed)
		{
			AnnounceTheCreation(itemId, item);
		}

		var op = _trace.NextOperationId();

		if (_session.Role == SessionRole.Guest)
		{
			_guard.Mark(itemId); // the frozen copy must not be killed by the reconcile before the host's stream takes over

			// Freeze the item at the drop spot — NO local simulation until the
			// host's stream arrives (ItemPositionFollow switches it to local
			// physics on its first tick). Without the freeze the copy diverges
			// from the host's trajectory: the report travels for the latency,
			// the host starts simulating from the drop spot, and a local copy
			// that already rolled gets yanked back THROUGH the wall ("thrown
			// item through the wall"). The frozen item plays the host's
			// simulation from the same spot — same phase, no rewind. (The
			// landmine argument from the earlier kinematic design is gone: the
			// layer isolation + MineScriptPatches shield items from tripping
			// mines, so local physics in the playback phase is safe.) The throw
			// report reads rb.velocity AFTER ThrowItem ran; the kinematic body
			// keeps the assigned property values. DropItem does not move the
			// item (Body.cs:1441-1451), so the drop spot IS the reported
			// position.
			item.rb.bodyType = RigidbodyType2D.Kinematic;
			item.rb.velocity = Vector2.zero;
			item.rb.angularVelocity = 0f;
		}

		// The departure waits for the frame-end flush (the throw velocity lands a
		// moment later and merges into ONE report). No flush of another item is
		// forced here any more: the machine holds one entry PER ITEM, so a second
		// drop in the same frame — which a same-frame flush cannot settle — no
		// longer overwrites the first one's report (ticket
		// `drop-pending-single-slot-overwrite`).
		_trace.Begin(op, itemId, "OnItemDropped", "Drop");
		_dropState.EnterDrop(itemId, item, item.transform.position, op, DropPendingState.Source.CarriedInventory);
	}

	internal void OnItemThrown(Item item)
	{
		if (IsReplayedFact || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var claimed = HasAnId(item);
		var itemId = _ids.EnsureId(item);
		if (itemId == 0)
		{
			return;
		}

		if (!claimed)
		{
			AnnounceTheCreation(itemId, item);
		}

		if (_session.Role == SessionRole.Guest)
		{
			_guard.Mark(itemId);
		}

		if (_dropState.TryConsumeByThrow(item, out var thrown))
		{
			// Landed check: a throw with the item NOT a standalone world item
			// (re-attached mid-flight — an inventory-internal sequence) never
			// left the world; reporting it would materialize a phantom drop.
			var status = ItemWorldSync.IsStandaloneWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
			_reports.CommitReport(itemId, thrown.Op, "OnItemThrown", status,
				() =>
				{
					_reports.SendDropReport(itemId, item, thrown.Pos);
					return 1;
				},
				"Drop", "Throw");
			return;
		}

		// No matching pending drop — the pump already flushed it on a previous
		// frame (a cross-frame DropItem → ThrowItem, rare) or a hook-order
		// anomaly. Report alone anyway, or the item never enters the domain;
		// the host's re-place covers the rare double report.
		var op = _trace.NextOperationId();
		var throwStatus = ItemWorldSync.IsStandaloneWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
		_reports.CommitReport(itemId, op, "OnItemThrown", throwStatus,
			() =>
			{
				_reports.SendDropReport(itemId, item, (Vector2)item.transform.position);
				return 1;
			},
			"Throw");
	}

	/// <summary>Next frame: every departure whose frame has passed reports now — the
	/// one-frame wait lets the game's DropItem → ThrowItem sequence set the FINAL
	/// velocity (a zero-velocity report materialized a ghost on the host, "dropped —
	/// immediately desynced"), and it is what lets a container load re-home a
	/// container's child first. One entry per item: two departures in one frame are
	/// two reports. An item that meanwhile left the world as a whole (or that a
	/// load re-homed) does not re-report.</summary>
	internal void FlushPendingDrop()
	{
		if (!_dropState.HasPending)
		{
			return; // the common frame: nothing left a home, so nothing is due
		}

		// The due set is taken OUT of the machine before the first report goes out:
		// a report RE-ENTERS this method (the item domain settles the deferred
		// creation reports before an operation goes out, and this flush is one of
		// those owners), and it then finds the machine short of exactly the entries
		// this call is reporting — never a half-iterated shared buffer.
		var due = new List<ItemDropState.Departure>();
		_dropState.Settle(due);
		foreach (var departure in due)
		{
			// Settle already verified the item is a standalone world item — the
			// commit is Committed by construction. The id is normally the one the
			// departure minted (and claimed) when it was registered; a departure
			// registered by another seam without an id states its creation here, on
			// the same rule.
			var claimed = HasAnId(departure.Item);
			var itemId = _ids.EnsureId(departure.Item);
			if (itemId == 0)
			{
				continue;
			}

			if (!claimed)
			{
				AnnounceTheCreation(itemId, departure.Item);
			}

			_reports.CommitReport(itemId, departure.Op, "FlushPendingDrop", CommitStatus.Committed,
				() =>
				{
					_reports.SendDropReport(itemId, departure.Item, departure.Pos);
					return 1;
				},
				"Drop", "Flush");
		}
	}

	/// <summary>Whether the object already carries an instance id — the question the claim turns on: an id minted below is an identity nobody else has been told about.</summary>
	private static bool HasAnId(Item item)
	{
		var instanceId = item.GetComponent<ItemInstanceId>();
		return instanceId != null && instanceId.Id != 0; // Unity object — ==
	}

	/// <summary>
	/// The id below was invented here, so the creation has to be stated before the operation that uses
	/// it — the host judges creation-before-operation (its kernel refuses the id it never judged). Same
	/// payload the pickup path sends for the same reason ("spawn-then-pickup": the spawn binds the
	/// peer's same-spot object by position, and the operation then moves it).
	/// </summary>
	private void AnnounceTheCreation(ulong itemId, Item item)
	{
		var position = item.transform.position;
		_items.SendItemSpawned(itemId, ItemStateCodec.CaptureItem(item, -1),
			new NetVector2(position.x, position.y), new NetVector2(0f, 0f),
			item.transform.eulerAngles.z, false, 0f);
		_log.LogInformation(
			"[ItemSpawn] {Type} (id {ItemId}) had no id when it left the inventory — its creation is stated before the departure.",
			item.id,
			itemId);
	}
}
