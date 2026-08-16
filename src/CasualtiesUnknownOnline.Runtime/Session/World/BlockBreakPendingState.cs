using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The block-break report state machine (PURE — no Unity, the frame and
/// coordinates are explicit inputs): a local break spans two frames — the
/// DamageBlock postfix sees the block gone and holds the report, the drops'
/// Item.Start (the NEXT frame) folds each drop in, the frame-end flush sends
/// ONE BlockDamagedMsg carrying the break + all drops. Every change is a
/// transition, failure paths are explicit (world left → Reset). The GameAdapter
/// feeds the game inputs (world coordinates, Time.frameCount) and reads the
/// flushed payload — this machine is what the tests lock.
/// </summary>
internal sealed class BlockBreakPendingState
{
	/// <summary>The lifecycle of one break report.</summary>
	internal enum Phase
	{
		/// <summary>No break report in flight.</summary>
		Idle,

		/// <summary>A block broke locally; the report waits one frame for the drops' Item.Start to fold in.</summary>
		Broken,
	}

	private (float PosX, float PosY, float Dmg, bool MetalBonus, int Frame, long Op, List<BlockDropEntryMsg> Drops)? _pending;

	internal Phase Current => _pending is null ? Phase.Idle : Phase.Broken;

	/// <summary>Idle → Broken: the block broke locally — hold the report until the drops are collected. The op id links the pending state to its operation trace; MetalBonus preserves the source's metallic-block multiplier.</summary>
	internal void EnterBreak(float posX, float posY, float dmg, bool metalBonus, long op, int currentFrame) =>
		_pending = (posX, posY, dmg, metalBonus, currentFrame, op, []);

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
	internal bool TryFlush(int currentFrame, out (float PosX, float PosY, float Dmg, bool MetalBonus, long Op, List<BlockDropEntryMsg> Drops) flushed)
	{
		if (_pending is not { } pending)
		{
			flushed = default;
			return false;
		}

		if (currentFrame <= pending.Frame)
		{
			flushed = default;
			return false;
		}

		_pending = null;
		flushed = (pending.PosX, pending.PosY, pending.Dmg, pending.MetalBonus, pending.Op, pending.Drops);
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
