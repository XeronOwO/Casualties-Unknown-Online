using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// World-event domain: block placement + the generated-baseline difference
/// table, building-entity damage/open, earthquakes (host timing — guests never
/// trigger, they only receive) and the keypad codes. Block DAMAGE + the block
/// break arbitration live in <see cref="BlockBreakSync"/> (the break's drops
/// are one message with the break — split for the 600-line gate). Local
/// compute → report → host relay/arbitration, position-keyed. Owns the
/// deferred spawn-landing presentation (sound + camera shake) that the start
/// gate must not play into the frozen world.
/// </summary>
internal sealed partial class WorldEventSync(
	ISessionControl session,
	IWorldControl world,
	BlockBreakSync blockBreaks,
	RestoredWorldFactReplay replay,
	OperationTrace trace,
	WorldEntityKernelProjection kernelProjection,
	WorldEntryFanout worldBackfill,
	ILogger<WorldEventSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly BlockBreakSync _blockBreaks = blockBreaks;
	private readonly RestoredWorldFactReplay _replay = replay;
	private readonly OperationTrace _trace = trace;
	private readonly WorldEntityKernelProjection _kernelProjection = kernelProjection;
	private readonly WorldEntryFanout _worldBackfill = worldBackfill;
	private readonly WorldBuildingEntitySync _buildingEntities = new(session, world, trace, log);
	private readonly ILogger<WorldEventSync> _log = log;

	/// <summary>True while a remote world mutation is being applied — the local-report hooks must stay silent (call identity lives in CallContext, not bools).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	/// <summary>The generated world snapshot the difference table diffs against (host/solo only).</summary>
	private ushort[,]? _baseline;

	internal void BindToSession()
	{
		_world.BlockDamagedReceived += _blockBreaks.OnRemoteBlockDamaged;
		_world.BlockDamageSnapshotReceived += _blockBreaks.OnBlockDamageSnapshot;
		_world.BuildingEntityDamagedReceived += _buildingEntities.OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived += OnRemoteBuildingEntityOpenedRelay;
		_world.BlockStateReceived += OnRemoteBlockState;
		_world.BlockPlacedReceived += OnRemoteBlockPlaced;
		_world.EarthquakeStartReceived += OnEarthquakeStartReceived;
		_world.KeypadCodeReceived += OnKeypadCodeReceived;
		_kernelProjection.OpenedEntitiesProjected += OnOpenedEntitiesProjected;
		_kernelProjection.BuildingHealthProjected += OnBuildingHealthProjected;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
		_session.EntryRepairRequested += OnEntryRepairRequested;
	}

	internal void Unbind()
	{
		_world.BlockDamagedReceived -= _blockBreaks.OnRemoteBlockDamaged;
		_world.BlockDamageSnapshotReceived -= _blockBreaks.OnBlockDamageSnapshot;
		_world.BuildingEntityDamagedReceived -= _buildingEntities.OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived -= OnRemoteBuildingEntityOpenedRelay;
		_world.BlockStateReceived -= OnRemoteBlockState;
		_world.BlockPlacedReceived -= OnRemoteBlockPlaced;
		_world.EarthquakeStartReceived -= OnEarthquakeStartReceived;
		_world.KeypadCodeReceived -= OnKeypadCodeReceived;
		_kernelProjection.OpenedEntitiesProjected -= OnOpenedEntitiesProjected;
		_kernelProjection.BuildingHealthProjected -= OnBuildingHealthProjected;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_session.EntryRepairRequested -= OnEntryRepairRequested;
	}

	// The building-entity appliers return what the live world took, because the
	// HOST's restored cut counts them (the restore's live-write account). The live
	// relay and the guest's projection only care that they ran, so these thin
	// wrappers keep the event signatures void — and stay the single subscription
	// identity Bind/Unbind pair up.

	private void OnRemoteBuildingEntityOpenedRelay(NetVector2 pos) =>
		_buildingEntities.OnRemoteBuildingEntityOpened(pos);

	private void OnOpenedEntitiesProjected(IReadOnlyList<NetVector2Msg> positions) =>
		_buildingEntities.OnOpenedEntitiesProjected(positions);

	private void OnBuildingHealthProjected(IReadOnlyList<BuildingEntityHealthEntryMsg> entries) =>
		_buildingEntities.OnBuildingHealthProjected(entries);

	/// <summary>Building-entity patch entry: report a local damage write (delegated to the building-entity sync).</summary>
	internal void OnBuildingEntityDamaged(BuildingEntity entity, float damage, bool playHitSound = true, bool playHitFlash = false) =>
		_buildingEntities.OnBuildingEntityDamaged(entity, damage, playHitSound, playHitFlash);

	/// <summary>Building-entity patch entry: report a local open write (delegated to the building-entity sync).</summary>
	internal void OnBuildingEntityOpened(BuildingEntity entity) =>
		_buildingEntities.OnBuildingEntityOpened(entity);

	/// <summary>A member (re)entered the world — re-broadcast the keypad codes so
	/// a reconnect gets them immediately instead of waiting up to 60 s for the
	/// periodic cycle (idempotent — an already-set code is left alone).</summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (inWorld && IsHostMode && WorldGeneration.world != null) // Unity object — ==
		{
			SendKeypadCodes();
		}
	}

	/// <summary>
	/// The host re-sent a member's entry state because that member is still re-asserting its
	/// entry — its readiness window is open, so a send made on the entry edge may have been
	/// swallowed by the lazy P2P session. The keypad codes are one of the two entry tables the
	/// adapter owns (the geysers' liquid types are the other), so they ride the repair exactly
	/// as they ride the entry edge; the send is idempotent (the receiver leaves a set code
	/// alone). It is a broadcast because the keypad channel has no per-member send today —
	/// third parties receive a duplicate of a table they already hold.
	/// </summary>
	private void OnEntryRepairRequested(ulong steamId)
	{
		if (IsHostMode && WorldGeneration.world != null) // Unity object — ==
		{
			_log.LogInformation("[Keypad] entry repair for {Peer} — re-broadcast the codes (its entry window is still open).", steamId);
			SendKeypadCodes();
		}
	}

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>
	/// Pump: capture the generated baseline once generation completes (host/solo
	/// — the difference table's reference), and periodically re-send the damage
	/// table to in-world members. The lazy Steam P2P session establishes up to
	/// ~30 s after world entry — world-mutation broadcasts sent in that window
	/// are silently dropped (the handshake retries cover the handshake; the
	/// BlockPlaced relay has no retry), so the guest's world keeps the generated
	/// blocks where the host broke them ("the guest's breaks are a subset of the
	/// host's", points appearing right after world entry). The resend is
	/// idempotent (same-value SetBlock) and small (only deviated blocks).
	/// </summary>
	private float _lastSnapshotResend;

	internal void Update()
	{
		if (_session.Role != SessionRole.Guest)
		{
			TryCaptureWorldBaseline();
		}

		if (IsHostMode && _session.SessionActive && Time.unscaledTime - _lastSnapshotResend > 60f)
		{
			_lastSnapshotResend = Time.unscaledTime;
			if (_session.Members.Any(m => m.InWorld))
			{
				// The trap layout is re-derived from the LIVE scene inside the
				// repair group itself (WorldEntryFanout -> ILiveTrapLayoutSource),
				// once per frame however many members are healed: the table is only
				// as fresh as its last scan, so a table sent as-is could resurrect
				// an entity the world has since removed.
				foreach (var member in _session.Members)
				{
					if (member.InWorld)
					{
						_worldBackfill.SendInSessionRepair(member.SteamId); // the absolute in-world tables (block state/damage, trap layout, enemy snapshot, kernel checkpoint, runtime entities) — the swallowed-send recovery for a member that never leaves the world
					}
				}
			}

			SendKeypadCodes(); // re-send the full set (idempotent — set codes are left alone) — covers the lazy-session swallow window and keypads created after the first send (the airdrop/command case, #128 follow-up)
		}
	}

	/// <summary>
	/// Called from the SetBlock patch after any world mutation (mining,
	/// placement, EARTHQUAKES, remote application). Host/solo: diff against the
	/// generated baseline (equal → removed from the difference table, otherwise
	/// upserted) and broadcast the mutation live — air writes included: the
	/// earthquake (WorldGeneration.cs:895) and environment breaks SetBlock(0)
	/// on each side with INDEPENDENT random, so without the air-write relay the
	/// two sides' terrain diverges ("the item keeps being pulled back" — items
	/// fall through holes that exist on one side only). Guest: report local
	/// mutations for arbitration (mining double-reports via BlockDamaged —
	/// idempotent). Remote applications are guarded (they answer their own
	/// way); generation-time SetBlock calls are the baseline itself and excluded.
	/// </summary>
	internal void OnBlockSet(Vector2Int pos, ushort block)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		// The block at this cell was written (a break, a placement): whatever partial
		// damage was accounted for here — this side's outstanding contribution on a
		// guest, every sender's ledger entry on the host — belonged to the block that
		// is gone, and a fresh block at the same cell starts from zero. Remote writes
		// return above and clear on their own path (OnRemoteBlockPlaced).
		_blockBreaks.ForgetBlockDamageAccounting(pos);

		// Trace only the PLAYER-driven writes: mining and placement (the postfix
		// verified the write landed — GetBlock == block). Quake/environment
		// breaks fire inside WorldGeneration.Update at 16/s per side — the
		// [Earthquake] summary lines cover those; a per-break trace would drown
		// the log.
		if (_session.SessionActive && !WorldGenerationUpdatePatch.InUpdate)
		{
			_trace.End(_trace.NextOperationId(), 0, "OnBlockSet", "Committed(1)", "BlockSet");
		}

		// Host OR solo: diff against the generated baseline (equal → removed
		// from the difference table, otherwise upserted). Solo tracking is
		// what lets a solo game that opens a lobby later hand its accumulated
		// world changes to a joining guest (the guest regenerates the seed
		// world and applies the table). Guests do not track — they only apply.
		if (_session.Role != SessionRole.Guest)
		{
			if (_baseline is null)
			{
				TryCaptureWorldBaseline(); // generation may have just completed this frame
				if (_baseline is null)
				{
					return; // still no baseline — nothing to diff against
				}
			}

			if (block == _baseline[pos.x, pos.y])
			{
				_world.RemoveBlockState(pos.x, pos.y); // restored to baseline — no longer a difference
			}
			else
			{
				_world.ReportBlockState(pos.x, pos.y, block);
			}
		}

		if (_session.SessionActive)
		{
			// An air write ends any partial damage at that cell — a broken
			// block is the block-state snapshot's semantic, never the
			// partial-damage snapshot's.
			if (block == 0)
			{
				_blockBreaks.OnBlockAirWrite(pos);
			}

			// Is this write the block-removal half of a damage ROLL? The native
			// DamageBlock opens its own call-identity scope around the roll
			// (WorldGenerationDamageBlockPatch), so a break that ran inside one
			// played the game's break presentation on THIS side: the hit and step
			// sounds of the block that just went, and its break particles. The
			// receiving side must present the same break, because its own copy of
			// the air write arrives BEFORE the drops-carrying break report and the
			// report's native roll therefore never runs there (see
			// RemoteBreakPresentation). An earthquake/environment break — the
			// game's own SetBlock air write inside WorldGeneration.Update — and a
			// placement are NOT this: nothing was played here, so nothing is
			// invented there.
			var playerBreak = block == 0 && CallContext.Current == CallContext.Origin.DamageBlockOrigin;

			// A world mutation in a live session: the source applied it locally
			// (local compute) — host broadcasts it, guest reports it for
			// arbitration. Solo (no session) never sends.
			if (_session.Role == SessionRole.Host)
			{
				_world.BroadcastBlockPlaced(0, pos.x, pos.y, block, playerBreak);
			}
			else if (_session.Role == SessionRole.Guest)
			{
				_world.SendBlockPlacedReport(pos.x, pos.y, block, playerBreak);
			}
		}
	}

	/// <summary>
	/// A mutation arrived: host arbitrates — a PLACEMENT (block != 0) must land
	/// on air (the game's own placement condition, Item.cs), an AIR write
	/// (earthquake/environment break, block == 0) must land on something — then
	/// applies, records the difference and relays to EVERY member (the reporter
	/// included: its echo is the acknowledgement that clears its pending
	/// report); a refused report gets the host's current cell as a targeted
	/// correction, so the reporter converges instead of staying diverged. Guest
	/// applies it directly. An APPLIED air write also records the sender's
	/// break for the drops arbitration: when that sender's BlockDamaged report
	/// (the drops carrier) arrives later, the record proves the break was the
	/// first writer (see _recentBroken).
	/// <para>
	/// The WRITE ITSELF goes through <see cref="RemoteBlockWrite"/>: an air write
	/// the message marks as a player's break is applied with the game's OWN damage
	/// roll, so this side presents the break — the broken block's hit and step
	/// sounds and its break particles — exactly as the side that computed it did.
	/// The air write arrives before the drops-carrying break report, so that report
	/// can no longer present anything on a cell this write already made air. The
	/// claim rides the relay unchanged, so every peer downstream presents the same
	/// one break, and a placement or an environment write stays silent everywhere.
	/// </para>
	/// </summary>
	private void OnRemoteBlockPlaced(ulong sender, int x, int y, ushort block, bool playerBreak, WorldGenerationRelation generation)
	{
		// A report cannot be arbitrated against a half-generated world: while
		// (re)generating, every cell belongs to the previous layer. The guest's
		// own generation boundary clears its pending table; dropping here closes
		// the host-reset → guest-apply skew window (a fallback re-report fired
		// in it must not land in the new layer).
		if (WorldGeneration.world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			return;
		}

		// A report from ANOTHER generation is refused before it can become
		// arbitration evidence (the host's _recentBroken record) or a world write:
		// the cell key is layer-relative, so a stale air write would clear a
		// freshly generated block. The message seam already logged both
		// generations; this line names the consequence.
		if (generation == WorldGenerationRelation.Stale)
		{
			_log.LogWarning("[BlockSync] {Sender}'s block report at ({X},{Y}) belongs to another world generation — neither applied nor recorded as an air write.", sender, x, y);
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var pos = new Vector2Int(x, y);
			if (IsHostMode)
			{
				if ((block == 0) == (WorldGeneration.world.GetBlock(pos) == 0))
				{
					// First-writer-wins: the host's cell stands. Answer the reporter with
					// it — its pending report is acknowledged and it converges.
					_world.SendBlockPlacedCorrection(sender, x, y, WorldGeneration.world.GetBlock(pos));
					return;
				}

				RemoteBlockWrite.Apply(WorldGeneration.world, pos, block, playerBreak, _log);
				_blockBreaks.ForgetBlockDamageAccounting(pos);
				_world.ReportBlockState(x, y, block); // the mutation is a world difference too
				_world.BroadcastBlockPlaced(0, x, y, block, playerBreak); // everyone, the reporter included — the echo is its acknowledgement (same-value SetBlock is idempotent); the break claim rides the relay so peers downstream present it too
				if (block == 0)
				{
					// A player break (its BlockDamaged report follows) — or a
					// quake/environment write (expires unused in BlockBreakSync).
					_blockBreaks.OnBlockAirWrite(pos);
					_blockBreaks.OnRemoteAirWriteApplied(sender, pos);
					_buildingEntities.MarkSupportLossRemote(pos);
				}
			}
			else
			{
				var changed = WorldGeneration.world.GetBlock(pos) != block;
				RemoteBlockWrite.Apply(WorldGeneration.world, pos, block, playerBreak, _log);
				_blockBreaks.ForgetBlockDamageAccounting(pos);
				if (changed && block == 0)
				{
					// A guest receiving the host's air-write relay may have its
					// own stale BlockDamage at this cell (from local partial
					// mining) — SetBlock(0) does not remove it, so clear the
					// game-side crack sprite here too. An UNCHANGED cell is the
					// echo of this side's own accepted report (the host relays to
					// the reporter): the local air transition already ran its
					// cleanup, and re-marking it would suppress this side's own
					// building-drop roll (RemoteEntityDeath).
					_blockBreaks.OnBlockAirWrite(pos);
					_buildingEntities.MarkSupportLossRemote(pos);
				}
			}
		}
	}

	/// <summary>
	/// An earthquake just started (detected in WorldGenerationUpdatePatch). The
	/// HOST broadcasts it (quake timing is synced to the host: guests show the
	/// effect and re-align their timer, so every side shakes together and
	/// breaks its own nearby region; the regions union via the air-write relay,
	/// overlaps count once). A GUEST never starts one: its timer is frozen by
	/// the patch's Prefix (WorldGenerationUpdatePatch), so a start observed
	/// here is either the host's broadcast landing mid-frame (frame order) or
	/// a freeze leak — never canceled (canceling a broadcast-driven quake is
	/// "started then ended"; the freeze is the guard). Solo play (no session)
	/// quakes normally.
	/// </summary>
	internal void OnEarthquakeStarted(float duration, float nextDelay)
	{
		if (IsHostMode && _session.SessionActive)
		{
			_log.LogInformation("[Earthquake] host quake started ({Duration:F1}s, next in {NextDelay:F0}s) — broadcasting.", duration, nextDelay);
			_world.BroadcastEarthquakeStart(duration, nextDelay);
		}
		else if (_session.SessionActive)
		{
			_log.LogInformation("[Earthquake] guest quake start observed ({Duration:F1}s) — timer frozen, host broadcast drives it.", duration);
		}
		else
		{
			_log.LogInformation("[Earthquake] local quake started on this side ({Duration:F1}s, next in {NextDelay:F0}s) — solo, no session.", duration, nextDelay);
		}
	}

	/// <summary>Guest side: an earthquake began (host timing) — show the effect (earthquakeTime drives the Update intensity ramp) and re-align the local quake timer to the host's next delay, so the next quake fires on all sides together.</summary>
	private void OnEarthquakeStartReceived(float duration, float nextDelay)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		WorldGeneration.world.earthquakeTime = duration;
		WorldGeneration.world.earthquakeDelay = nextDelay;
		_log.LogInformation("[Earthquake] guest: host quake ({Duration:F1}s) — showing effect, timer re-aligned ({NextDelay:F0}s).", duration, nextDelay);
	}

	/// <summary>
	/// The host's world-entry seam, once per generation: snapshot worldBlocks the
	/// moment generation completes (the generated baseline the difference table
	/// diffs against), write a RESTORED cut into the fresh world when the save
	/// layer restored one, then broadcast the decided keypads. Any generation
	/// start resets the flag; a completed generation re-captures — per world/layer,
	/// matching the table reset at CaptureWorldParams.
	///
	/// The order inside this method is load-bearing: a restored cut must land
	/// AFTER the baseline snapshot (the generated world is the diff's reference,
	/// not the restored one) and BEFORE the keypad broadcast (the peers must be
	/// told the restored codes, not freshly generated ones).
	/// </summary>
	private void TryCaptureWorldBaseline()
	{
		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			_baseline = null;
			return;
		}

		if (_baseline is not null)
		{
			return; // already captured for this generation
		}

		var blocks = HarmonyTraverse.ReadWorldBlocks(world);
		if (blocks is null)
		{
			return;
		}

		_baseline = (ushort[,])blocks.Clone();
		if (_replay.HasPending)
		{
			// A restored layer is NOT a new layer for the WORLD-FACT tables: the
			// facts about to be written into this world are the ones the cut named,
			// so the world-fact reset (which exists to start a NEW layer from empty
			// tables) must not run — it would erase the restored block diff, the
			// restored partial damage and the kernel-backed world-entity facts the
			// checkpoint restore just put back.
			//
			// The runtime-created ENTITY table is the opposite: it is this
			// generation's registration table (its entries are gone with the old
			// scene), and a creation record left over from the previous layer would
			// be materialized by a late-joining guest as a ghost entity.
			_world.ResetRuntimeEntities();
			_log.LogInformation("Captured world baseline ({Width}x{Height}) — a restored cut is pending; the restored world-fact tables are kept, the runtime-entity table is reset, and the restored facts are written into this world.",
				_baseline.GetLength(0), _baseline.GetLength(1));
		}
		else
		{
			_world.ResetDamagedBlocks();
			_log.LogInformation("Captured world baseline ({Width}x{Height}) — the damage table now diffs against it.",
				_baseline.GetLength(0), _baseline.GetLength(1));
		}

		_replay.ApplyIfPending(); // writes the restored cut into the freshly generated world (a no-op when none is pending)

		if (IsHostMode)
		{
			SendKeypadCodes(); // the world-entry broadcast carries the restored codes — the replay above already wrote them
		}
	}

	/// <summary>
	/// Host only: broadcast every keypad's code (the game lazy-generates on first
	/// use per side, <c>Openable.cs:19</c> — every side would get its own code).
	/// The table read generates a missing code host-side, so the set is complete
	/// by construction. Runs at world entry (after the generation completed — the
	/// Openables exist by then, and after a restored cut wrote its codes) and
	/// re-runs on the 60 s cycle: the re-send covers the lazy Steam P2P session's
	/// swallow window and keypads created after the first send (the
	/// airdrop/command case, #128 follow-up — created keypads are broadcast
	/// immediately by <see cref="OnEntityInstantiated"/>, this is the fallback).
	/// Idempotent: the receiver leaves an already-set code alone.
	/// </summary>
	private void SendKeypadCodes()
	{
		var codes = KeypadCodeTable.Capture();
		if (codes.Count > 0)
		{
			_world.SendKeypadCodes(codes);
		}
	}

	/// <summary>
	/// Guest side: the host's keypad codes arrived — write them onto the local
	/// Openables (position-keyed: deterministic world entities sit at the same
	/// place on both sides). A code already set (a local first use raced the
	/// broadcast) is left alone.
	/// </summary>
	private void OnKeypadCodeReceived(IReadOnlyList<KeypadEntryMsg> codes)
	{
		if (codes.Count == 0)
		{
			return;
		}

		var applied = KeypadCodeTable.ApplyWhereUnset(codes, _log);
		_log.LogInformation("[Keypad] applied {Applied} host keypad code(s).", applied);
	}

	/// <summary>
	/// Guest side: the host's authoritative block-state snapshot — apply the
	/// accumulated mutations to our freshly generated world (the snapshot only
	/// arrives after our InWorld report, i.e. after generation finished).
	///
	/// The writes run under the REMOTE-APPLY scope (see
	/// <see cref="WorldBlockStateTable"/>) so the local report hook stays silent;
	/// without it every snapshot cell was echoed back to the host as a guest
	/// mutation.
	///
	/// An air write settles the building support loss ONLY for a live row: the
	/// host made that transition in ITS world, so the building that stood on the
	/// block must die on this side too (and this side's drops are presentation the
	/// host's item table reconciles). A RESTORED row already carried that
	/// settlement — the host replayed it out of an archive — so re-running it here
	/// would kill a building the authority still holds and re-roll drops that are
	/// already checkpoint items.
	/// </summary>
	private void OnRemoteBlockState(IReadOnlyList<DamagedBlock> blocks)
	{
		if (WorldGeneration.world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			return;
		}

		WorldBlockStateTable.Apply(blocks, (pos, supportLossSettled) =>
		{
			_blockBreaks.OnBlockAirWrite(pos);
			if (!supportLossSettled)
			{
				_buildingEntities.MarkSupportLossRemote(pos);
			}
		}, _log);

		_log.LogInformation("Applied host block-state snapshot ({Count} blocks, {Restored} restored cell(s) whose support loss was settled by the host).",
			blocks.Count, blocks.Count(block => block.SupportLossSettled));
	}
}
