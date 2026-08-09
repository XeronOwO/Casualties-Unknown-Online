using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The block-break report state: a local break spans two frames — the
/// DamageBlock postfix sees the block gone and holds the report, the drops'
/// Item.Start (the NEXT frame) folds each drop in, the frame-end flush sends
/// ONE BlockDamagedMsg carrying the break + all drops. Named phases like
/// ItemDropState: every change is a transition, failure paths are explicit
/// (world left → Reset), all pending read/write points live in one class
/// (state belongs to its owner). The migration DECISIONS (when to flush, what
/// to trace) stay with WorldEventSync — this class only answers "which
/// transition happened".
/// </summary>
internal sealed class PendingBlockBreak
{
	/// <summary>The lifecycle of one break report.</summary>
	internal enum Phase
	{
		/// <summary>No break report in flight.</summary>
		Idle,

		/// <summary>A block broke locally; the report waits one frame for the drops' Item.Start to fold in.</summary>
		Broken,
	}

	private (Vector2 Pos, float Dmg, int Frame, long Op, List<BlockDropEntryMsg> Drops)? _pending;

	internal Phase Current => _pending is null ? Phase.Idle : Phase.Broken;

	/// <summary>Idle → Broken: the block broke locally — hold the report until the drops are collected. The op id links the pending state to its operation trace.</summary>
	internal void EnterBreak(Vector2 pos, float dmg, long op) =>
		_pending = (pos, dmg, Time.frameCount, op, []);

	/// <summary>Broken → stays Broken: one drop's Item.Start ran — fold it in. False when no break is pending (the drop then falls back to a standalone spawn report).</summary>
	internal bool TryAddDrop(BlockDropEntryMsg drop)
	{
		if (_pending is not { } pending)
		{
			return false;
		}

		pending.Drops.Add(drop);
		_pending = pending;
		return true;
	}

	/// <summary>
	/// Broken → Idle (frame-end flush) or stays Broken. A same-frame flush is
	/// refused: the drops' Item.Start only runs the frame AFTER the break —
	/// flushing early would send the break with half its drops (the rest would
	/// then report as standalone spawns and split the verdict).
	/// </summary>
	internal bool TryFlush(out (Vector2 Pos, float Dmg, long Op, List<BlockDropEntryMsg> Drops) flushed)
	{
		if (_pending is not { } pending)
		{
			flushed = default;
			return false;
		}

		if (Time.frameCount <= pending.Frame)
		{
			flushed = default;
			return false;
		}

		_pending = null;
		flushed = (pending.Pos, pending.Dmg, pending.Op, pending.Drops);
		return true;
	}

	/// <summary>Broken → Idle: the world was left (scene switch / session end) — the broken block's drops are gone with it. Returns the op id so the trace stays balanced.</summary>
	internal bool TryReset(out long op)
	{
		if (_pending is not { } pending)
		{
			op = 0;
			return false;
		}

		op = pending.Op;
		_pending = null;
		return true;
	}
}
