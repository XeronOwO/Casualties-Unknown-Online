using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The receive half of the block-break chain (split from <see cref="BlockBreakSync"/> — the 600-line gate):
/// one relay is judged and applied here. The host arbitrates first-writer-wins through
/// <see cref="BlockBreakArbitration"/>, bounds the breaker's drops against the destroyed block's own table
/// (<see cref="BlockDropBoundJudge"/>) and relays the break to every member; a guest applies the host's
/// broadcast and materializes its drops. The arrival side (the deferred queue for a relay that lands while the
/// world generates), the local report hold and the flush stay in <see cref="BlockBreakSync"/>, which delegates
/// here once the world is whole.
/// <para>
/// The logger is the sync's category on purpose: the lines this class writes are the same lines the class they
/// moved out of wrote, and a changed category would change every one of them on a reader's screen.
/// </para>
/// </summary>
internal sealed class BlockBreakRemoteApply(
	ISessionControl session,
	IWorldControl world,
	IItemControl items,
	WorldBuildingEntitySync buildingEntities,
	BlockDropBoundJudge dropJudge,
	BlockBreakArbitration arbitration,
	Action<Vector2Int> onBlockAirWrite,
	ILogger<BlockBreakSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly WorldBuildingEntitySync _buildingEntities = buildingEntities;
	private readonly BlockDropBoundJudge _dropJudge = dropJudge;
	private readonly BlockBreakArbitration _arbitration = arbitration;
	private readonly Action<Vector2Int> _onBlockAirWrite = onBlockAirWrite;
	private readonly ILogger<BlockBreakSync> _log = log;

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>The apply path for one relay (the host arbitrates and relays; a guest applies): runs only against a whole world.</summary>
	internal void Apply(ulong sender, int x, int y, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, WorldGenerationRelation generation)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity objects — ==
		{
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
				_log.LogWarning("[BlockBreak] {Sender}'s report at ({X},{Y}) belongs to another world generation — not applied; its drops are refused with it.", sender, x, y);
				_dropJudge.RefuseAll(sender, new Vector2Int(x, y), "the report belongs to another world generation", drops, buildingDrops);
			}
			else
			{
				_log.LogWarning("[BlockBreak] the host's relay at ({X},{Y}) belongs to another world generation — not applied and its drops not materialized.", x, y);
			}

			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			// The message carries the CELL itself (protocol 32): both sides name the
			// same block, and the partial-damage accounting is keyed by it. The block
			// is read ONCE: it is the host's own statement of what this cell held
			// before the break, which is both the air test and the input the reported
			// drops are bounded against.
			var cell = new Vector2Int(x, y);
			var standingBlock = world.GetBlock(cell);
			var blockIsAir = standingBlock == 0;
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
					var verdict = _arbitration.TryAccept(
						sender, cell.x, cell.y, blockIsAir, generation == WorldGenerationRelation.Current, standingBlock, out var destroyedBlock, out var recordedBuildingRows);
					if (verdict == Verdict.Refused || (verdict == Verdict.Fresh && !blockIsAir))
					{
						_dropJudge.RefuseAll(sender, cell, "the cell's break is not this sender's first-writer claim", drops, buildingDrops);
						return;
					}

					// The support loss is marked BEFORE the drops are judged, because
					// the entities that lose a support block here are destroyed by
					// their own next Update: this call is the host's only chance to
					// read their drop tables. On the recorded shapes it finds them
					// already marked and captures nothing (the air write took their
					// rows); on the standing shape no air write ever landed here, so
					// this mark is what captures them.
					var liveBuildingRows = _buildingEntities.MarkSupportLossRemote(cell);
					var buildingRows = recordedBuildingRows.Count > 0 ? recordedBuildingRows : liveBuildingRows;
					_arbitration.RecordAccepted(sender, cell.x, cell.y, Time.unscaledTime, destroyedBlock, buildingRows);
					AcceptBreak(
						sender, cell, dmg, metalBonus,
						_dropJudge.Bound(sender, cell, destroyedBlock, drops),
						_dropJudge.BoundBuildingDrops(sender, cell, buildingRows, buildingDrops),
						verdict);
					OnRemoteDamageBrokeBlock(sender, cell, verdict);
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
					_world.BroadcastBlockDamaged(sender, cell.x, cell.y, dmg, metalBonus, null, null);
				}
				else
				{
					OnRemoteDamageBrokeBlock(sender, cell, null);
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
				_world.BlockReports.AnswerBreakDrops(cell.x, cell.y, drops, buildingDrops);
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
	/// Both drop sets arrive already bounded against the tables their own roll
	/// answers to. The support loss was marked by the caller BEFORE these drops
	/// were judged, because the mark is also the capture point for the dying
	/// buildings' tables (<see cref="WorldBuildingEntitySync.MarkSupportLossRemote"/>).
	/// </summary>
	private void AcceptBreak(ulong sender, Vector2Int cell, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, Verdict verdict)
	{
		_items.FireBlockDropsReceived(sender, drops ?? []);
		_items.FireBuildingDropsReceived(sender, buildingDrops ?? []);
		_world.BroadcastBlockDamaged(0, cell.x, cell.y, dmg, metalBonus, drops, buildingDrops);
		_log.LogInformation("[BlockBreak] {Sender}'s break at ({X},{Y}) {Verdict} — {BlockCount} block drop(s) + {BuildingCount} building drop(s) registered + relayed.",
			sender, cell.x, cell.y,
			verdict switch
			{
				Verdict.Fresh => "accepted",
				Verdict.LostAirWrite => "accepted as this generation's lost air write",
				Verdict.Repeat => "re-accepted (repeat report)",
				_ => "judged " + verdict,
			},
			drops?.Count ?? 0, buildingDrops?.Count ?? 0);
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
	/// them registered and relayed by <see cref="AcceptBreak"/> — on every accepting verdict, the
	/// lost-air-write one included — so this only converges the STATE.
	/// <para>
	/// The block itself goes through <see cref="RemoteBlockWrite"/>: when it still
	/// stands here — the lost-air-write shape, where the breaker's air write never
	/// arrived — this side would otherwise learn the cell was air without ever
	/// hearing the break, so the write runs the game's own damage roll (the native
	/// break presentation) and the relay carries the break claim for every peer
	/// downstream. A cell already air is only written.
	/// </para>
	/// </summary>
	private void OnRemoteDamageBrokeBlock(ulong sender, Vector2Int cell, Verdict? verdict)
	{
		// The break is real, so this side's block must go — and when it still
		// STANDS here (the accepted report arrived without its air write: the
		// lost-air-write shape) it goes through the game's own damage roll, which
		// is the native break presentation the air write normally carried. A cell
		// already air (the air write landed first, or the apply above just broke
		// it) has nothing left to present and is only written.
		var world = WorldGeneration.world;
		if (world != null) // Unity object — ==
		{
			RemoteBlockWrite.Apply(world, cell, 0, playerBreak: true, _log);
		}

		_world.ReportBlockState(cell.x, cell.y, 0);
		_world.BroadcastBlockPlaced(0, cell.x, cell.y, 0, playerBreak: true); // everyone, the reporter included — its echo acknowledges the pending air-write report (same-value SetBlock(0) is a no-op locally), and the break claim lets every side downstream present it
		_onBlockAirWrite(cell);
		_buildingEntities.MarkSupportLossRemote(cell);
		// The headline names the shape the JUDGEMENT reached instead of guessing it from the cell's own state
		// afterwards. The old sentence asserted "without an air-write report" on EVERY accepted break, and on
		// a Fresh verdict IS the record of that sender's applied air write, and a Repeat is the cell's earlier accepted claim — so the one line a reader opens
		// to learn which shape ran answered "no air write" on the path nearly every member break takes.
		// `null` is the damage-only edge below, which no verdict judged.
		var shape = verdict switch
		{
			Verdict.LostAirWrite => "the accepted report arrived without its air write (the lost-air-write shape), so the block went through the game's own roll here",
			Verdict.Fresh => "its air write had already landed here (the fresh first-writer claim)",
			Verdict.Repeat => "this was a repeat of its own earlier claim for the cell",
			null => "the report carried no drop payload, so this side cannot tell a damage-only hit from a break whose roll was empty — the applied damage broke the block here",
			_ => "the judgement reached " + verdict,
		};
		_log.LogInformation("[BlockBreak] {Sender}'s remote damage broke the block at ({X},{Y}) — {Shape}; the block-state difference was recorded and the break relayed with its claim.",
			sender, cell.x, cell.y, shape);
	}
}
