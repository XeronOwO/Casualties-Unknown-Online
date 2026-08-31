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
	OperationTrace trace,
	ILogger<BlockBreakSync> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly BlockBreakPendingState _breakState = breakState;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<BlockBreakSync> _log = log;

	/// <summary>
	/// Host: block-break arbitration — a guest's air-write (BlockPlaced,
	/// SetBlock(0)) that the host APPLIED proves that guest's break is the
	/// first writer for that cell; the record is consumed when that guest's
	/// BlockDamaged report (the drops carrier) arrives. The BlockPlaced
	/// necessarily precedes the BlockDamaged (both reliable, same source — the
	/// break report waits a frame for the drops), so the block is ALREADY air
	/// when the drops arrive and a GetBlock check can never tell first-writer
	/// from second-writer ("the block is gone" is true for both) — the record
	/// does. The table and the one-shot accept decision live in the pure
	/// BlockBreakArbitration machine (Runtime); this side feeds the game
	/// inputs (cell coordinates, Time.unscaledTime).
	/// </summary>
	private readonly BlockBreakArbitration _arbitration = new();

	private const float RecentBrokenTtl = 3f;
	private float _lastBrokenCleanup;

	/// <summary>The game's own blockDamages cap — a snapshot must never push the guest's list past it (WorldGeneration.cs:732-737).</summary>
	private const int GameBlockDamageCap = 128;

	/// <summary>True while a remote world mutation is being applied — the local-report hooks must stay silent (call identity lives in CallContext, not bools).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>Pump: expire break records without a consuming BlockDamaged (quake/environment air writes, a breaker that disconnected mid-operation). The 1 s throttle is this side's cost guard — the expiry decision lives in the machine.</summary>
	internal void Update()
	{
		if (_arbitration.Count == 0 || Time.unscaledTime - _lastBrokenCleanup <= 1f)
		{
			return;
		}

		_lastBrokenCleanup = Time.unscaledTime;
		_arbitration.PurgeStale(Time.unscaledTime, RecentBrokenTtl);
	}

	/// <summary>
	/// Called from the DamageBlock patch after a LOCAL block damage was applied:
	/// report it so the peer applies the same damage at the same world position
	/// (raw damage + MetalBonus — the receiver's own DamageBlock applies the
	/// same metallic multiplier to the same generated block). The host also
	/// records the post-write accumulated BlockDamage.damage for the
	/// late-joiner snapshot. A BREAK is not reported immediately — it waits one
	/// frame so the drops' Item.Start folds into the pending break (one
	/// message, one verdict), and the frame-end flush sends it.
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
			// Damage only (the block survived) — report it immediately and
			// record the post-write absolute damage (the snapshot's fact).
			_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg, bonusMetal, null);
			ReportBlockDamageFromGame(world, cell);
			_trace.End(op, 0, "OnBlockDamaged", "Committed(1)", "Damage");
			return;
		}

		// The block broke (SetBlock(0) ran inside the roll, WorldGeneration.cs:839)
		// — its partial damage is gone, and the report holds one frame so the
		// drops' Item.Start folds in (break + drops = ONE message, one verdict).
		_world.RemoveBlockDamage(cell.x, cell.y);
		_trace.Begin(op, 0, "OnBlockDamaged", "Break");
		_breakState.EnterBreak(pos.x, pos.y, dmg, bonusMetal, op, Time.frameCount);
	}

	/// <summary>
	/// Frame-end flush of a pending break: register the drops (host/solo — the
	/// authoritative table must know them before the periodic keyframe) and
	/// send ONE BlockDamagedMsg carrying the break + all drops + MetalBonus.
	/// The local drop objects are the original (never materialized again); the
	/// peers materialize from the message.
	/// </summary>
	internal void FlushPendingBlockBreak()
	{
		if (!_breakState.TryFlush(Time.frameCount, out var flushed))
		{
			return;
		}

		if (_session.Role != SessionRole.Guest)
		{
			_items.RegisterBlockDrops(flushed.Drops);
		}

		_world.SendBlockDamaged(new NetVector2(flushed.PosX, flushed.PosY), flushed.Dmg, flushed.MetalBonus, flushed.Drops);
		_trace.End(flushed.Op, 0, "FlushPendingBlockBreak", $"Committed({flushed.Drops.Count})", "Break", "Drop");
	}

	/// <summary>The world was left (scene switch / session end) — a pending break cannot resolve anymore; cancel it so the operation trace stays balanced.</summary>
	internal void ResetPending()
	{
		if (_breakState.TryReset(out var op))
		{
			_trace.End(op, 0, "ResetPending", "Cancelled", "WorldLeft");
		}
	}

	/// <summary>
	/// The peer damaged a block — apply it locally (remote verify/sync).
	/// Host (arbitration): a BREAK report (drops attached) is first-writer-wins —
	/// the sender's own BlockPlaced applied the air-write earlier (the
	/// _recentBroken record, taken when that write landed) is what proves it
	/// was the first writer, never a GetBlock check (the block is air for the
	/// loser too). Accepted → the drops register + materialize + relay (source
	/// excluded — the breaker already has the originals). Refused → every drop
	/// gets an ItemReject and the breaker destroys its local copy. A damage-only
	/// report applies the damage and relays while the block still stands; the
	/// host then records its post-apply absolute damage for the snapshot.
	/// Guest: the host's broadcast — apply the damage; a break's drops
	/// materialize. No side ever rolls: the drops are the breaker's local
	/// compute, carried by the message. MetalBonus rides raw on both sides so
	/// the game's own metallic multiplier (WorldGeneration.cs:715) is applied
	/// identically everywhere.
	/// </summary>
	internal void OnRemoteBlockDamaged(ulong sender, NetVector2 pos, float dmg, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var cell = world.WorldToBlockPos(new Vector2(pos.X, pos.Y));
			var blockIsAir = world.GetBlock(cell) == 0;
			if (IsHostMode)
			{
				if (drops is { Count: > 0 } && blockIsAir)
				{
					// A BREAK with drops: first-writer-wins on the sender's
					// applied air-write record.
					if (_arbitration.TryAccept(sender, cell.x, cell.y))
					{
						_items.FireBlockDropsReceived(sender, drops);
						_world.BroadcastBlockDamaged(sender, pos, dmg, metalBonus, drops);
						_log.LogInformation("[BlockBreak] {Sender}'s break at ({X},{Y}) accepted — {Count} drop(s) registered + relayed.",
							sender, cell.x, cell.y, drops.Count);
					}
					else
					{
						foreach (var drop in drops)
						{
							_items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
						}

						_log.LogInformation("[BlockBreak] {Sender}'s break at ({X},{Y}) refused (already broken) — {Count} drop(s) rejected.",
							sender, cell.x, cell.y, drops.Count);
					}

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
					ReportBlockDamageFromGame(world, cell);
					_world.BroadcastBlockDamaged(sender, pos, dmg, metalBonus, null);
				}
				else
				{
					_world.RemoveBlockDamage(cell.x, cell.y);
				}

				return;
			}

			// Guest: the host's broadcast — apply. An already-air cell has no
			// block to damage; its drops (an accepted break relay whose
			// BlockPlaced already made the cell air here) still materialize.
			if (blockIsAir)
			{
				if (drops is { Count: > 0 })
				{
					_items.FireBlockDropsReceived(sender, drops);
				}

				return;
			}

			world.DamageBlock(cell, dmg, true, metalBonus, true);
			if (drops is { Count: > 0 })
			{
				_items.FireBlockDropsReceived(sender, drops);
			}
		}
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
		_world.RemoveBlockDamage(cell.x, cell.y);
		var world = WorldGeneration.world;
		if (world != null && BlockDamageCleaner.ClearForAirWrite(world, cell))
		{
			_log.LogDebug("[BlockBreak] cleared stale game BlockDamage at ({X},{Y}) after air write.",
				cell.x, cell.y);
		}
	}

	/// <summary>
	/// The host's partial block-damage snapshot arrived (world entry / the
	/// 60 s resend) — apply every entry as an ABSOLUTE set: find or create the
	/// cell's BlockDamage, write the host's accumulated damage and refresh the
	/// crack sprite. Idempotent by construction (writing the same damage again
	/// is a no-op), and it never rides DamageBlock: an additive delta could go
	/// negative when this side already mined further, and a damage ≥ health
	/// must not break the block here (a break is the block-state snapshot's
	/// semantic, not this backfill's).
	/// </summary>
	internal void OnBlockDamageSnapshot(IReadOnlyList<BlockDamageEntryMsg> entries)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		var applied = 0;
		foreach (var entry in entries)
		{
			var cell = new Vector2Int(entry.X, entry.Y);
			var block = world.GetBlock(cell);
			if (block == 0)
			{
				continue; // already broken — the block-state snapshot owns it
			}

			var blockHealth = world.GetBlockInfo(block).health;
			if (entry.Damage <= 0f || entry.Damage >= blockHealth)
			{
				_log.LogWarning("Block-damage snapshot at ({X},{Y}): damage {Damage} outside a surviving block's range ({Health} hp) — skipped.",
					cell.x, cell.y, entry.Damage, blockHealth);
				continue;
			}

			var blockDamage = world.GetBlockDamage(cell);
			if (blockDamage == null)
			{
				if (world.blockDamages.Count >= GameBlockDamageCap)
				{
					_log.LogWarning("Block-damage snapshot at ({X},{Y}): the game's {Cap}-entry blockDamages list is full — skipped.",
						cell.x, cell.y, GameBlockDamageCap);
					continue;
				}

				blockDamage = new BlockDamage { pos = cell, damage = entry.Damage };
				world.blockDamages.Add(blockDamage);
			}
			else
			{
				blockDamage.damage = entry.Damage;
			}

			blockDamage.UpdateSprite();
			applied++;
		}

		_log.LogInformation("Block-damage snapshot applied ({Applied}/{Count} cells).", applied, entries.Count);
	}

	/// <summary>
	/// Host record: read the game's own post-write BlockDamage state and store
	/// it as the snapshot fact. A survived block always has an entry (the
	/// game creates it before the cap-eviction branch, WorldGeneration.cs:
	/// 720-737); if it is gone, the cell's partial damage is gone with it.
	/// </summary>
	private void ReportBlockDamageFromGame(WorldGeneration world, Vector2Int cell)
	{
		var blockDamage = world.GetBlockDamage(cell);
		if (blockDamage != null && world.GetBlock(cell) != 0)
		{
			_world.ReportBlockDamage(cell.x, cell.y, blockDamage.damage);
		}
		else
		{
			_world.RemoveBlockDamage(cell.x, cell.y);
		}
	}
}
