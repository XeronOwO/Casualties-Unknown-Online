using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The block-damage sync chain (split from WorldEventSync — the 600-line gate):
/// local damage reports immediately while the block survives, local breaks hold
/// their report one frame for the drops (PendingBlockBreak), then go out as ONE
/// BlockDamagedMsg carrying the break + drops; the host arbitrates
/// first-writer-wins (the record of the sender's APPLIED air-write — a GetBlock
/// check is useless, the block is air for the loser too) and the accepted relay
/// materializes the drops on the other sides. This class is the ARRIVAL and local
/// side: the report hold, the flush, the deferred queue for a relay that lands
/// while the world generates, and the partial block-damage snapshot (host record +
/// guest absolute apply) live here; the relay's own judgement and application is
/// <see cref="BlockBreakRemoteApply"/>. The DamageBlock/SetBlock patches and the
/// world domain are thin adapters, and the drops' own fate (bounded against the
/// destroyed block's table, or refused whole) is <see cref="BlockDropBoundJudge"/>'s.
/// </summary>
internal sealed class BlockBreakSync(
	ISessionControl session,
	IWorldControl world,
	IItemControl items,
	BlockBreakPendingState breakState,
	WorldBuildingEntitySync buildingEntities,
	BlockDropBoundJudge dropJudge,
	OperationTrace trace,
	ILogger<BlockBreakSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly WorldBuildingEntitySync _buildingEntities = buildingEntities;
	private readonly BlockBreakPendingState _breakState = breakState;
	private readonly BlockDropBoundJudge _dropJudge = dropJudge;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<BlockBreakSync> _log = log;

	/// <summary>
	/// Host: block-break arbitration — a guest's air-write (BlockPlaced,
	/// SetBlock(0)) that the host APPLIED proves that guest's break is the
	/// first writer for that cell; the record is consumed when that guest's
	/// BlockDamaged report (the drops carrier) arrives. The BlockPlaced
	/// necessarily precedes the BlockDamaged (both reliable, same source — the
	/// break report waits a frame for the drops), so in the NORMAL path the block
	/// is already air when the drops arrive and a GetBlock check cannot tell
	/// first-writer from second-writer there ("the block is gone" is true for
	/// both) — the record does. The degraded path inverts that: a report reaching
	/// this side while the block still stands is the lost air write, and it is
	/// accepted when the report's world/layer stamp proves it belongs to THIS
	/// generation (a stale previous-layer report is refused at the message seam
	/// before it can claim a freshly generated cell). The table and the verdict
	/// live in the pure BlockBreakArbitration machine (Runtime); this side feeds
	/// the game inputs (cell coordinates, Time.unscaledTime).
	/// </summary>
	private readonly BlockBreakArbitration _arbitration = new();

	/// <summary>
	/// Relays that reached this side while its world was (re)generating. Every cell belongs to the
	/// previous layer until generation finishes, so the report cannot be applied on arrival — and a
	/// guest has no pending entry to re-report it (only a breaker does), which made dropping it a
	/// permanent divergence. Held in arrival order and applied once the world is whole; cleared with
	/// the world on <see cref="ResetPending"/>.
	/// </summary>
	private readonly List<DeferredRemoteReport> _deferredReports = [];

	/// <summary>The deferred queue's cost guard: a session whose world never finishes generating must not grow it without bound.</summary>
	private const int DeferredReportCap = 256;



	private const float RecentBrokenTtl = 3f;

	/// <summary>
	/// How long an accepted break stays attributable to its breaker: the report can
	/// arrive again from the guest's fallback (the acknowledgement relay was the
	/// lost message), so the record must outlive one re-report window — the 60 s
	/// cadence plus the round trip. Past it a repeat is refused like any
	/// unattributed break, which is the same bound every other recovery in this
	/// family has.
	/// </summary>
	private const float AcceptedBreakTtl = 90f;

	private float _lastBrokenCleanup;

	/// <summary>True while a remote world mutation is being applied — the local-report hooks must stay silent (call identity lives in CallContext, not bools).</summary>
	private bool IsRemoteApply => CallContext.IsWithin(CallContext.Origin.RemoteApply);

	private BlockBreakRemoteApply? _remoteApplyLazy;

	/// <summary>The receive half (split out — the 600-line gate): one relay judged and applied. Built lazily because it takes this side's <see cref="OnBlockAirWrite"/> as its post-break hook.</summary>
	private BlockBreakRemoteApply RemoteApply => _remoteApplyLazy ??= new BlockBreakRemoteApply(_session, _world, _items, _buildingEntities, _dropJudge, _arbitration, OnBlockAirWrite, _log);

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>Pump: expire break records without a consuming BlockDamaged (quake/environment air writes, a breaker that disconnected mid-operation) and accepted-break records past the re-report window. The 1 s throttle is this side's cost guard — the expiry decisions live in the machine.</summary>
	internal void Update()
	{
		DrainDeferredReports();

		if ((_arbitration.Count == 0 && _arbitration.AcceptedCount == 0) || Time.unscaledTime - _lastBrokenCleanup <= 1f)
		{
			return;
		}

		_lastBrokenCleanup = Time.unscaledTime;
		_arbitration.PurgeStale(Time.unscaledTime, RecentBrokenTtl, AcceptedBreakTtl);
	}

	/// <summary>
	/// Applies the relays held while the world was generating, in arrival order, once the world is
	/// whole again. A report that still finds a generating world stays queued (the drain stops there,
	/// keeping the order); the world's own scene change clears the queue through
	/// <see cref="ResetPending"/>.
	/// </summary>
	private void DrainDeferredReports()
	{
		while (_deferredReports.Count > 0)
		{
			if (WorldGeneration.world == null || HarmonyTraverse.IsGenerating())
			{
				return;
			}

			var report = _deferredReports[0];
			_deferredReports.RemoveAt(0);
			_log.LogInformation(
				"[BlockBreak] applying the relay held while the world generated: ({X},{Y}) from {Sender}, {Remaining} still queued.",
				report.X, report.Y, report.Sender, _deferredReports.Count);
			RemoteApply.Apply(report.Sender, report.X, report.Y, report.Dmg, report.MetalBonus, report.Drops, report.BuildingDrops, WorldGenerationRelation.Current);
		}
	}

	/// <summary>
	/// Called from the DamageBlock patch after a LOCAL block damage was applied:
	/// report it so the peer applies the same damage at the same cell (raw damage +
	/// MetalBonus — the receiver's own DamageBlock applies the same metallic
	/// multiplier to the same generated block). The patch hands the CELL over: it
	/// binds the game's body overload, which already holds it, so this path never
	/// converts a world position. CUO records nothing here on the
	/// HOST: the host's row IS the authority and it never re-reports its own
	/// damage. A GUEST records the hit's own contribution — the damage this call
	/// added to the cell, in the game's accumulated units — BEFORE its delta report
	/// goes out, and that cumulative value rides the report: it is what lets the
	/// host account per sender (audit gap W2, per sender since protocol 32) instead
	/// of merging whole-cell totals. A BREAK is not reported immediately — it waits
	/// one frame so the drops' Item.Start folds into the pending break (one
	/// message, one verdict), and the frame-end flush sends it.
	/// </summary>
	internal void OnBlockDamaged(Vector2Int cell, float dmg, bool bonusMetal, float applied)
	{
		if (IsRemoteApply || !_session.SessionActive)
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var op = _trace.NextOperationId();
		if (world.GetBlock(cell) != 0)
		{
			// Damage only (the block survived) — report it immediately. The
			// contribution is THIS hit's own damage (the guest's cumulative value for
			// the cell, 0 when the cell could not be tracked — the host then applies
			// the raw damage, which is the pre-ledger behaviour).
			var contribution = _world.BlockReports.AddLocalBlockDamage(cell.x, cell.y, applied);
			_world.SendBlockDamaged(cell.x, cell.y, dmg, bonusMetal, contribution, null, null);
			_trace.End(op, 0, "OnBlockDamaged", "Committed(1)", "Damage");
			return;
		}

		// The block broke (SetBlock(0) ran inside the roll, WorldGeneration.cs:839)
		// — its partial damage is gone with it: the game's own list drops the
		// entry inside DamageBlock (WorldGeneration.cs:841). The report holds one
		// frame so the drops' Item.Start folds in (break + drops = ONE message,
		// one verdict).
		_trace.Begin(op, 0, "OnBlockDamaged", "Break");
		_breakState.EnterBreak(cell.x, cell.y, dmg, bonusMetal, op, Time.frameCount);
	}

	/// <summary>
	/// Frame-end flush of a pending break: register the drops (host/solo — the
	/// authoritative table must know them before the periodic keyframe) and
	/// send ONE BlockDamagedMsg carrying the break + all block drops and
	/// building-death drops + MetalBonus. The local drop objects are the
	/// original (never materialized again); the peers materialize from the
	/// message. A GUEST records the drops as unacknowledged BEFORE the send
	/// (audit gap W1's drop half): the host registers a guest's break drops
	/// exclusively from this message, so a swallowed one leaves items neither
	/// the authoritative table nor the item keyframe can heal — the fallback
	/// re-reports the set until the host relays it back.
	/// </summary>
	internal void FlushPendingBlockBreak()
	{
		if (!_breakState.TryFlush(Time.frameCount, out var flushed))
		{
			return;
		}

		var hasDrops = flushed.Drops.Count > 0 || flushed.BuildingDrops.Count > 0;
		var world = WorldGeneration.world;
		if (_session.Role != SessionRole.Guest)
		{
			_items.RegisterBlockDrops(flushed.Drops);
			_items.RegisterBuildingDrops(flushed.BuildingDrops);
		}
		else if (hasDrops && world != null) // Unity object — ==
		{
			// Only a report that HAS a drop payload is recorded: an empty set has
			// nothing to recover, and nothing on the host could ever answer it (a
			// drops-free report takes the damage-only path, which relays no
			// payload), so the entry would occupy the table for the whole session
			// and eventually starve the cap.
			_world.BlockReports.ReportBreakDrops(flushed.CellX, flushed.CellY, flushed.Drops, flushed.BuildingDrops);
		}

		_world.SendBlockDamaged(
			flushed.CellX,
			flushed.CellY,
			flushed.Dmg,
			flushed.MetalBonus,
			0f,
			flushed.Drops,
			flushed.BuildingDrops);
		_trace.End(flushed.Op, 0, "FlushPendingBlockBreak",
			$"Committed({flushed.Drops.Count}+{flushed.BuildingDrops.Count})", "Break", "Drop", "BuildingDrop");
	}

	/// <summary>
	/// The world was left (a new world/layer generating, or the session ending) —
	/// a pending break cannot resolve anymore, so cancel it (the operation trace
	/// stays balanced) and drop the arbitration records with it. The cell keys are
	/// LAYER-RELATIVE: an accepted break from the old layer kept across a descent
	/// would let a pending re-report of that break match a freshly generated block
	/// in the new layer and be acknowledged against it. The records' meaning dies
	/// with the layer, so they are dropped with it — belt and braces beside the
	/// refusal of any unattributable report.
	/// </summary>
	internal void ResetPending()
	{
		_arbitration.Reset();
		if (_deferredReports.Count > 0)
		{
			_log.LogInformation("[BlockBreak] the world was left — dropping {Count} held relay(s) with it.", _deferredReports.Count);
			_deferredReports.Clear();
		}

		if (_breakState.TryReset(out var op))
		{
			_trace.End(op, 0, "ResetPending", "Cancelled", "WorldLeft");
		}
	}

	/// <summary>
	/// The peer damaged a block — apply it locally (remote verify/sync).
	/// Host (arbitration): a BREAK report (drops attached) is first-writer-wins —
	/// the sender's own BlockPlaced applied the air-write earlier (the
	/// _recentBroken record, taken when that write landed) is what proves it was
	/// the first writer, never a GetBlock check (the block is air for the loser
	/// too). The one case where the cell's own state IS part of the proof is a
	/// report that arrives while the block still stands: no earlier break can have
	/// taken a cell this side still holds, so a report the generation stamp
	/// attributes to THIS world settles it as the lost air write.
	/// Accepted → the drops register + materialize + relay to EVERY member, the
	/// reporter included (that echo is the acknowledgement of its pending
	/// report), after the destroyed block's own table has bounded them: a drop no
	/// roll for that block could produce is refused on its own and the rest of the
	/// set still goes out (<see cref="BlockDropBoundJudge.Bound"/>). Refused → every drop gets an
	/// ItemReject and the breaker destroys its local copy. A damage-only report
	/// applies the damage and relays while the block still stands; the host then
	/// records its post-apply absolute damage for the snapshot.
	/// Guest: the host's broadcast — apply the damage; a break's drops
	/// materialize. No side ever rolls: the drops are the breaker's local
	/// compute, carried by the message. MetalBonus rides raw on both sides so
	/// the game's own metallic multiplier (WorldGeneration.cs:715) is applied
	/// identically everywhere.
	/// </summary>
	internal void OnRemoteBlockDamaged(ulong sender, int x, int y, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, WorldGenerationRelation generation)
	{
		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity objects/traverse — ==
		{
			// Every cell belongs to the previous layer until generation finishes. A report stamped
			// STALE belongs to another generation and is refused exactly as before; anything else
			// (the current generation's relay, or one arriving while this side is still generating
			// the same world) is HELD: a guest has no pending entry to re-report it, so dropping it
			// left the cell stale on this side for the whole session (measured 2026-11-13).
			if (generation == WorldGenerationRelation.Stale)
			{
				RefuseStaleReport(sender, x, y, drops, buildingDrops);
				return;
			}

			DeferReport(sender, x, y, dmg, metalBonus, drops, buildingDrops);
			return;
		}

		RemoteApply.Apply(sender, x, y, dmg, metalBonus, drops, buildingDrops, generation);
	}

	/// <summary>Refuses a report that belongs to another world generation, naming the side's consequence (the host rolls its drops back; a guest never materializes them).</summary>
	private void RefuseStaleReport(ulong sender, int x, int y, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (IsHostMode)
		{
			_log.LogWarning("[BlockBreak] {Sender}'s report at ({X},{Y}) belongs to another world generation — not applied; its drops are refused with it.", sender, x, y);
			_dropJudge.RefuseAll(sender, new Vector2Int(x, y), "the report belongs to another world generation", drops, buildingDrops);
		}
		else
		{
			_log.LogWarning("[BlockBreak] the host's relay at ({X},{Y}) belongs to another world generation — not applied and its drops not materialized.", x, y);
		}
	}

	/// <summary>Holds a relay that arrived while this side's world was generating; the oldest is dropped with a warning when the cost guard is reached.</summary>
	private void DeferReport(ulong sender, int x, int y, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_deferredReports.Count >= DeferredReportCap)
		{
			var dropped = _deferredReports[0];
			_deferredReports.RemoveAt(0);
			_log.LogWarning("[BlockBreak] the held-report queue is full — dropping the oldest relay at ({X},{Y}) from {Sender}.", dropped.X, dropped.Y, dropped.Sender);
		}

		_deferredReports.Add(new DeferredRemoteReport(sender, x, y, dmg, metalBonus, drops, buildingDrops));
		_log.LogInformation("[BlockBreak] a relay at ({X},{Y}) from {Sender} arrived while the world generates — held until it is whole ({Count} queued).", x, y, sender, _deferredReports.Count);
	}

	/// <summary>
	/// Host: the world domain applied a guest's air-write (its SetBlock(0)
	/// report) — record the sender's break for the drops arbitration: when that
	/// sender's BlockDamaged report (the drops carrier) arrives later, the
	/// record proves the break was the first writer AND carries what it
	/// destroyed (<paramref name="destroyedBlock"/> is the block this side's world
	/// held before the write was applied, read by the applier), which is what the
	/// reported drops are bounded against.
	/// <para>
	/// <paramref name="buildingRows"/> is the second table the same report is
	/// judged against: the drops of the buildings that lost their support block in
	/// this write. The caller reads them off the live entities at this moment (the
	/// only moment they exist) and hands them over here so they live exactly as long
	/// as the break claim they belong to.
	/// </para>
	/// </summary>
	internal void OnRemoteAirWriteApplied(ulong sender, Vector2Int cell, ushort destroyedBlock, IReadOnlyList<DropBound> buildingRows) =>
		_arbitration.RecordAppliedAirWrite(sender, cell.x, cell.y, Time.unscaledTime, destroyedBlock, buildingRows);

	/// <summary>
	/// Any applied air write (local or remote) invalidates the cell's partial
	/// damage — a broken block is carried by the block-state snapshot, never by
	/// the partial-damage snapshot.
	/// </summary>
	internal void OnBlockAirWrite(Vector2Int cell)
	{
		// The cell is air now: a partial-damage contribution for it (this side's own,
		// or any sender's ledger entry on the host) described a block that is gone,
		// and a future block at the same cell starts from zero.
		ForgetBlockDamageAccounting(cell);

		var world = WorldGeneration.world;
		if (world != null && BlockDamageCleaner.ClearForAirWrite(world, cell))
		{
			_log.LogDebug("[BlockBreak] cleared stale game BlockDamage at ({X},{Y}) after air write.",
				cell.x, cell.y);
		}
	}

	/// <summary>
	/// The block at this cell was WRITTEN (air, a fresh placement, a restored
	/// block): whatever partial damage was accounted for there belonged to the
	/// block that is gone, so this side's outstanding contribution and every
	/// sender's ledger entry for the cell are dropped. Called from every write path
	/// (the local SetBlock hook and the remote placement/air relays) — without it a
	/// fresh block at a cell would inherit the previous block's accounting, and a
	/// contribution below the stale value would resolve to "already accounted for"
	/// and never be applied.
	/// </summary>
	internal void ForgetBlockDamageAccounting(Vector2Int cell) =>
		_world.BlockReports.ForgetBlockDamageAccounting(cell.x, cell.y);

	/// <summary>
	/// The host's partial block-damage snapshot arrived (world entry / the
	/// 60 s resend) — apply every entry as an ABSOLUTE set per cell: find or
	/// create the cell's <c>BlockDamage</c>, write the host's accumulated damage
	/// and refresh the crack sprite. The row write and its validation live in
	/// <see cref="GameBlockDamageTable"/> — the same table the save restore writes,
	/// with the same rules — and it deliberately never rides <c>DamageBlock</c>:
	/// an additive delta could go negative when this side already mined further,
	/// and a damage ≥ health must not break the block here (a break is the
	/// block-state snapshot's semantic, not this backfill's).
	///
	/// This is a MERGE, not a replace: cells the snapshot does not name keep this
	/// side's own local damage. Only the save restore replaces the whole list —
	/// there the restored cut is the whole truth for the table.
	///
	/// A ZERO row is the host's authoritative "this cell holds no damage" (the
	/// answer to an absolute re-report the host's own cap/range rules refused, or
	/// a cell whose block is gone): the local crack is cleared through the same
	/// seam an air write uses, never written as a row.
	/// </summary>
	internal void OnBlockDamageSnapshot(IReadOnlyList<BlockDamageEntryMsg> entries)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var rows = new List<BlockDamageEntryMsg>(entries.Count);
		var cleared = 0;
		foreach (var entry in entries)
		{
			if (entry.Damage > 0f)
			{
				rows.Add(entry);
				continue;
			}

			if (BlockDamageCleaner.ClearForAirWrite(world, new Vector2Int(entry.X, entry.Y)))
			{
				cleared++;
			}
		}

		var apply = GameBlockDamageTable.Apply(world, rows, "Block-damage snapshot", _log);
		foreach (var damage in apply.Written)
		{
			// Presentation of a damage the table already holds: a sprite that
			// cannot be refreshed must not undo the applied row.
			damage.UpdateSprite();
		}

		_log.LogInformation("Block-damage snapshot applied ({Applied}/{Count} cells, {Refused} not applicable, {Cleared} cleared).",
			apply.Applied, entries.Count, apply.Refused, cleared);
	}

}
