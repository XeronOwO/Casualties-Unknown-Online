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
/// The block-break sync chain (split from WorldEventSync — the 600-line gate):
/// local breaks hold their report one frame for the drops (PendingBlockBreak),
/// then go out as ONE BlockDamagedMsg carrying the break + drops; the host
/// arbitrates first-writer-wins (the record of the sender's APPLIED air-write —
/// a GetBlock check is useless, the block is air for the loser too) and the
/// accepted relay materializes the drops on the other sides. One deep module:
/// the report hold, the arbitration record, the flush and the drops' fate
/// (register/refuse) live here — the DamageBlock/SetBlock patches and the
/// world domain are thin adapters to it.
/// </summary>
internal sealed class BlockBreakSync(
	SessionService session,
	WorldService world,
	ItemService items,
	BlockBreakPendingState breakState,
	OperationTrace trace,
	ILogger<BlockBreakSync> log)
{
	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
	private readonly ItemService _items = items;
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
	/// report it so the peer applies the same damage at the same world position.
	/// A BREAK is not reported immediately — it waits one frame so the drops'
	/// Item.Start folds into the pending break (one message, one verdict), and
	/// the frame-end flush sends it.
	/// </summary>
	internal void OnBlockDamaged(Vector2 pos, float dmg)
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
		if (world.GetBlock(world.WorldToBlockPos(pos)) != 0)
		{
			// Damage only (the block survived) — report it immediately.
			_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg, null);
			_trace.End(op, 0, "OnBlockDamaged", "Committed(1)", "Damage");
			return;
		}

		// The block broke (SetBlock(0) ran inside the roll, WorldGeneration.cs:839)
		// — hold the report: the drops' Item.Start folds in NEXT frame, the
		// frame-end flush then sends the break + drops as ONE message.
		_trace.Begin(op, 0, "OnBlockDamaged", "Break");
		_breakState.EnterBreak(pos.x, pos.y, dmg, op, Time.frameCount);
	}

	/// <summary>
	/// Frame-end flush of a pending break: register the drops (host/solo — the
	/// authoritative table must know them before the periodic keyframe) and
	/// send ONE BlockDamagedMsg carrying the break + all drops. The local drop
	/// objects are the original (never materialized again); the peers
	/// materialize from the message.
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

		_world.SendBlockDamaged(new NetVector2(flushed.PosX, flushed.PosY), flushed.Dmg, flushed.Drops);
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
	/// report applies the damage and relays while the block still stands.
	/// Guest: the host's broadcast — apply the damage; a break's drops
	/// materialize. No side ever rolls: the drops are the breaker's local
	/// compute, carried by the message.
	/// </summary>
	internal void OnRemoteBlockDamaged(ulong sender, NetVector2 pos, float dmg, IReadOnlyList<BlockDropEntryMsg>? drops)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var cell = world.WorldToBlockPos(new Vector2(pos.X, pos.Y));
			if (IsHostMode)
			{
				if (drops is { Count: > 0 } && world.GetBlock(cell) == 0)
				{
					// A BREAK with drops: first-writer-wins on the sender's
					// applied air-write record.
					if (_arbitration.TryAccept(sender, cell.x, cell.y))
					{
						_items.FireBlockDropsReceived(sender, drops);
						_world.BroadcastBlockDamaged(sender, pos, dmg, drops);
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

				// Damage only (or a break whose roll was empty): apply the
				// damage — a no-op on an already-broken block. The relay only
				// goes out while the block still stands: a broken block's
				// damage is meaningless elsewhere.
				world.DamageBlock(cell, dmg, true, false, true);
				if (world.GetBlock(cell) != 0)
				{
					_world.BroadcastBlockDamaged(sender, pos, dmg, null);
				}

				return;
			}

			// Guest: the host's broadcast — apply.
			world.DamageBlock(cell, dmg, true, false, true);
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
}
