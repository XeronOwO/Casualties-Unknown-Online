using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
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
/// materializes the drops on the other sides. One deep module: the report hold,
/// the arbitration record, the flush, the drops' fate (register/refuse) and the
/// partial block-damage snapshot (host record + guest absolute apply) live here
/// — the DamageBlock/SetBlock patches and the world domain are thin adapters.
/// </summary>
internal sealed class BlockBreakSync(
	ISessionControl session,
	IWorldControl world,
	IItemControl items,
	BlockBreakPendingState breakState,
	WorldBuildingEntitySync buildingEntities,
	OperationTrace trace,
	ILogger<BlockBreakSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly WorldBuildingEntitySync _buildingEntities = buildingEntities;
	private readonly BlockBreakPendingState _breakState = breakState;
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
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>Pump: expire break records without a consuming BlockDamaged (quake/environment air writes, a breaker that disconnected mid-operation) and accepted-break records past the re-report window. The 1 s throttle is this side's cost guard — the expiry decisions live in the machine.</summary>
	internal void Update()
	{
		if ((_arbitration.Count == 0 && _arbitration.AcceptedCount == 0) || Time.unscaledTime - _lastBrokenCleanup <= 1f)
		{
			return;
		}

		_lastBrokenCleanup = Time.unscaledTime;
		_arbitration.PurgeStale(Time.unscaledTime, RecentBrokenTtl, AcceptedBreakTtl);
	}

	/// <summary>
	/// Called from the DamageBlock patch after a LOCAL block damage was applied:
	/// report it so the peer applies the same damage at the same world position
	/// (raw damage + MetalBonus — the receiver's own DamageBlock applies the
	/// same metallic multiplier to the same generated block). CUO records nothing
	/// here on the HOST: the late-joiner absolute value is the GAME's own list,
	/// read at snapshot time (<see cref="BlockDamageSnapshotSender"/>). A GUEST
	/// records the cell's current absolute damage BEFORE its delta report goes
	/// out — the fallback's re-report source (audit gap W2). A BREAK is not
	/// reported immediately — it waits one frame so the drops' Item.Start folds
	/// into the pending break (one message, one verdict), and the frame-end flush
	/// sends it.
	/// </summary>
	internal void OnBlockDamaged(Vector2 pos, float dmg, bool bonusMetal)
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

		var cell = world.WorldToBlockPos(pos);
		var op = _trace.NextOperationId();
		if (world.GetBlock(cell) != 0)
		{
			// Damage only (the block survived) — report it immediately. The
			// absolute damage a late joiner receives is read from the game's own
			// list at snapshot time, so the HOST records nothing here; a GUEST
			// records the cell's current absolute value FIRST, because its live
			// report is a delta: a swallowed one would leave the host short by
			// exactly this hit and its absolute snapshot could never heal it
			// (audit gap W2).
			var accumulated = world.GetBlockDamage(cell)?.damage ?? 0f;
			if (accumulated > 0f)
			{
				_world.ReportBlockDamage(cell.x, cell.y, accumulated);
			}

			_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg, bonusMetal, null, null);
			_trace.End(op, 0, "OnBlockDamaged", "Committed(1)", "Damage");
			return;
		}

		// The block broke (SetBlock(0) ran inside the roll, WorldGeneration.cs:839)
		// — its partial damage is gone with it: the game's own list drops the
		// entry inside DamageBlock (WorldGeneration.cs:841). The report holds one
		// frame so the drops' Item.Start folds in (break + drops = ONE message,
		// one verdict).
		_trace.Begin(op, 0, "OnBlockDamaged", "Break");
		_breakState.EnterBreak(pos.x, pos.y, dmg, bonusMetal, op, Time.frameCount);
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
			// and eventually starve the cap. The world guard matters for the same
			// reason: the cell is the game's own world→cell conversion, which
			// cannot run without a world — `default` would key a real corner cell.
			var cell = world.WorldToBlockPos(new Vector2(flushed.PosX, flushed.PosY));
			_world.ReportBreakDrops(cell.x, cell.y, flushed.PosX, flushed.PosY, flushed.Drops, flushed.BuildingDrops);
		}

		_world.SendBlockDamaged(
			new NetVector2(flushed.PosX, flushed.PosY),
			flushed.Dmg,
			flushed.MetalBonus,
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
	/// report). Refused → every drop gets an ItemReject and the breaker destroys
	/// its local copy. A damage-only report applies the damage and relays while
	/// the block still stands; the host then records its post-apply absolute
	/// damage for the snapshot.
	/// Guest: the host's broadcast — apply the damage; a break's drops
	/// materialize. No side ever rolls: the drops are the breaker's local
	/// compute, carried by the message. MetalBonus rides raw on both sides so
	/// the game's own metallic multiplier (WorldGeneration.cs:715) is applied
	/// identically everywhere.
	/// </summary>
	internal void OnRemoteBlockDamaged(ulong sender, NetVector2 pos, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, WorldGenerationRelation generation)
	{
		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity objects/traverse — ==
		{
			// While (re)generating, every cell belongs to the previous layer — the
			// same reason the air-write half drops its reports here. The reporter's
			// pending entry survives, so its fallback re-reports once the world is
			// whole again.
			return;
		}

		if (generation == WorldGenerationRelation.Stale)
		{
			// The report/relay belongs to another world generation: its position is a
			// layer-relative key, so it must touch neither this world nor the
			// arbitration table. On the host a break's drops are the BREAKER's local
			// copies, so they are rolled back exactly as a first-writer loss does
			// (the world-service line already names both generations; this one names
			// the consequence). On a guest the relay's drops must simply never
			// materialize — answering the host with an ItemReject would be wrong.
			if (IsHostMode)
			{
				_log.LogWarning("[BlockBreak] {Sender}'s report at ({X},{Y}) belongs to another world generation — not applied; {BlockCount} block drop(s) + {BuildingCount} building drop(s) rejected.",
					sender, (int)pos.X, (int)pos.Y, drops?.Count ?? 0, buildingDrops?.Count ?? 0);
				RejectBreakDrops(sender, drops, buildingDrops);
			}
			else
			{
				_log.LogWarning("[BlockBreak] the host's relay at ({X},{Y}) belongs to another world generation — not applied and its drops not materialized.", (int)pos.X, (int)pos.Y);
			}

			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var cell = world.WorldToBlockPos(new Vector2(pos.X, pos.Y));
			var blockIsAir = world.GetBlock(cell) == 0;
			var hasDropPayload = (drops is { Count: > 0 }) || (buildingDrops is { Count: > 0 });
			if (IsHostMode)
			{
				if (hasDropPayload)
				{
					// A BREAK with drops. The verdict decides its fate; the drops
					// themselves are the breaker's local compute and their
					// registration/materialization is idempotent per item id, so a
					// REPEAT (the breaker's fallback re-sending a break the host
					// already accepted — its relay, the acknowledgement, was the lost
					// message) re-relays instead of being refused. Re-reporting is
					// what makes a swallowed report recoverable at all.
					// A FRESH verdict means this sender's air write already landed,
					// so the cell must be air here; a standing cell contradicts the
					// record and is refused. The clause is scoped to Fresh on purpose:
					// a REPEAT must survive a block placed back on the cell
					// (rebuilding a hole), or a legitimate, already-registered drop
					// would be destroyed on the breaker while the host's table still
					// holds it.
					// A report whose generation stamp proves it is THIS world's and
					// that names a cell which still stands is the LOST AIR WRITE: the
					// guest broke the block, the air-write report was lost together
					// with the drops-carrying one, so no record exists and this side's
					// cell is untouched. Accepting that shape used to be impossible —
					// a stale previous-layer report looked exactly like it and would
					// have broken a freshly generated block — and the stamp is what
					// tells the two apart (see BlockBreakArbitration.TryAccept).
					var verdict = _arbitration.TryAccept(sender, cell.x, cell.y, blockIsAir, generation == WorldGenerationRelation.Current);
					if (verdict == Verdict.Refused || (verdict == Verdict.Fresh && !blockIsAir))
					{
						RejectBreakDrops(sender, drops, buildingDrops);
						return;
					}

					_arbitration.RecordAccepted(sender, cell.x, cell.y, Time.unscaledTime);
					AcceptBreak(sender, cell, pos, dmg, metalBonus, drops, buildingDrops, verdict);
					OnRemoteDamageBrokeBlock(sender, cell);
					return;
				}

				// Damage only against an already-air cell: there is no block to
				// damage — ignore it. DamageBlock on air would create a
				// transient BlockDamage for an air cell and play the hit
				// sounds/particles (air health is 0, WorldGeneration.cs:
				// 315-322).
				if (blockIsAir)
				{
					return;
				}

				// Damage only (or a break whose roll was empty): apply the
				// damage. The relay only goes out while the block still
				// stands: a broken block's damage is meaningless elsewhere.
				world.DamageBlock(cell, dmg, true, metalBonus, true);
				if (world.GetBlock(cell) != 0)
				{
					_world.BroadcastBlockDamaged(sender, pos, dmg, metalBonus, null, null);
				}
				else
				{
					OnRemoteDamageBrokeBlock(sender, cell);
				}

				return;
			}

			// Guest: the host's broadcast — apply. An already-air cell has no
			// block to damage; its drops (an accepted break relay whose
			// BlockPlaced already made the cell air here) still materialize. When
			// the relay carries a break's drops it is ALSO the answer to this
			// side's own unacknowledged break report for the cell (the reporter is
			// included in the relay for exactly this echo), so the pending drop
			// report is done — the cell comes from the game's own conversion, which
			// only this side can run.
			if (_session.Role == SessionRole.Guest && sender == _session.HostSteamId && hasDropPayload)
			{
				_world.AnswerBreakDrops(cell.x, cell.y, drops, buildingDrops);
			}

			_buildingEntities.MarkSupportLossRemote(cell);
			if (blockIsAir)
			{
				if (drops is { Count: > 0 })
				{
					_items.FireBlockDropsReceived(sender, drops);
				}

				if (buildingDrops is { Count: > 0 })
				{
					_items.FireBuildingDropsReceived(sender, buildingDrops);
				}

				return;
			}

			world.DamageBlock(cell, dmg, true, metalBonus, true);
			if (drops is { Count: > 0 })
			{
				_items.FireBlockDropsReceived(sender, drops);
			}

			if (buildingDrops is { Count: > 0 })
			{
				_items.FireBuildingDropsReceived(sender, buildingDrops);
			}
		}
	}

	/// <summary>
	/// Host: an accepted (or repeated) break — register the drops and relay the
	/// break to EVERY member, the reporter included (its relay echo is the
	/// acknowledgement that clears its pending drop report; the materialization
	/// and registration are idempotent per item id, so a repeat is harmless).
	/// </summary>
	private void AcceptBreak(ulong sender, Vector2Int cell, NetVector2 pos, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, Verdict verdict)
	{
		_buildingEntities.MarkSupportLossRemote(cell);
		_items.FireBlockDropsReceived(sender, drops ?? []);
		_items.FireBuildingDropsReceived(sender, buildingDrops ?? []);
		_world.BroadcastBlockDamaged(0, pos, dmg, metalBonus, drops, buildingDrops);
		_log.LogInformation("[BlockBreak] {Sender}'s break at ({X},{Y}) {Verdict} — {BlockCount} block drop(s) + {BuildingCount} building drop(s) registered + relayed.",
			sender, cell.x, cell.y,
			verdict switch
			{
				Verdict.Fresh => "accepted",
				Verdict.LostAirWrite => "accepted as this generation's lost air write",
				_ => "re-accepted (repeat report)",
			},
			drops?.Count ?? 0, buildingDrops?.Count ?? 0);
	}

	/// <summary>Host: the break lost first-writer-wins — every drop goes back to the breaker, which destroys its local copy (the drops were never picked up, there is no ground position to roll back to).</summary>
	private void RejectBreakDrops(ulong sender, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (drops is not null)
		{
			foreach (var drop in drops)
			{
				_items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
			}
		}

		if (buildingDrops is not null)
		{
			foreach (var drop in buildingDrops)
			{
				_items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
			}
		}

		_log.LogInformation("[BlockBreak] {Sender}'s break report refused (the cell's break belongs to another writer) — {BlockCount} block drop(s) + {BuildingCount} building drop(s) rejected.",
			sender, drops?.Count ?? 0, buildingDrops?.Count ?? 0);
	}

	/// <summary>
	/// Host: the world domain applied a guest's air-write (its SetBlock(0)
	/// report) — record the sender's break for the drops arbitration: when that
	/// sender's BlockDamaged report (the drops carrier) arrives later, the
	/// record proves the break was the first writer.
	/// </summary>
	internal void OnRemoteAirWriteApplied(ulong sender, Vector2Int cell) =>
		_arbitration.RecordAppliedAirWrite(sender, cell.x, cell.y, Time.unscaledTime);

	/// <summary>
	/// Any applied air write (local or remote) invalidates the cell's partial
	/// damage — a broken block is carried by the block-state snapshot, never by
	/// the partial-damage snapshot.
	/// </summary>
	internal void OnBlockAirWrite(Vector2Int cell)
	{
		// The cell is air now: a pending partial-damage report for it would be
		// refused (air) on every future report cycle, so it dies with the block.
		_world.ForgetPendingBlockDamage(cell.x, cell.y);

		var world = WorldGeneration.world;
		if (world != null && BlockDamageCleaner.ClearForAirWrite(world, cell))
		{
			_log.LogDebug("[BlockBreak] cleared stale game BlockDamage at ({X},{Y}) after air write.",
				cell.x, cell.y);
		}
	}

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

	/// <summary>
	/// The host's applied remote damage broke the block. The SetBlock(0) inside
	/// <see cref="WorldGeneration.DamageBlock"/> ran under
	/// <see cref="CallContext.Origin.RemoteApply"/>, so the local-report hook
	/// stayed silent — without this the host's block-state difference table
	/// omits the cell and the host's absolute snapshot can never heal the peers
	/// that missed the breaker's own air-write report (sync-coverage audit W1).
	/// Record the air transition and relay it exactly like an applied air-write
	/// report; the relay INCLUDES the reporter — it already has the cell air
	/// locally, so the same-value echo is a no-op that acknowledges its pending
	/// air-write report. A report that carried the break's drops has already had
	/// them registered and relayed by <see cref="AcceptBreak"/> (the case where
	/// the air-write message was lost with it), so this only converges the STATE.
	/// </summary>
	private void OnRemoteDamageBrokeBlock(ulong sender, Vector2Int cell)
	{
		_world.ReportBlockState(cell.x, cell.y, 0);
		_world.BroadcastBlockPlaced(0, cell.x, cell.y, 0); // everyone, the reporter included — its echo acknowledges the pending air-write report (same-value SetBlock(0) is a no-op locally)
		OnBlockAirWrite(cell);
		_buildingEntities.MarkSupportLossRemote(cell);
		_log.LogInformation("[BlockBreak] {Sender}'s remote damage broke the block at ({X},{Y}) without an air-write report — recorded the block-state difference and relayed it.",
			sender, cell.x, cell.y);
	}
}
