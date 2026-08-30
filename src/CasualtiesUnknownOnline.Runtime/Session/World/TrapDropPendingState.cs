using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Defers a destructive trap trigger on the local triggering side until the
/// BuildingEntity.Update death branch's items have run their Item.Start and
/// can be folded into the same trap event. Unlike the block-break state, the
/// trap event itself is held too, so the trigger side sends ONE EntityEventMsg
/// whose Drops list lets the host commit the trap facts and the item spawns as
/// one atomic kernel composite.
/// </summary>
internal sealed class TrapDropPendingState
{
	/// <summary>Wait long enough for the death-branch Update and the next-frame Item.Start to run before flushing.</summary>
	private const int HoldFrames = 2;

	/// <summary>Building-death drops spawn at the trap entity's position; this radius matches them to the correct pending trap.</summary>
	private const float MatchRadius = 3f;

	private sealed class PendingTrap
	{
		internal PendingTrap(EntityEventKind kind, float x, float y, byte extra, int startFrame)
		{
			Kind = kind;
			X = x;
			Y = y;
			Extra = extra;
			StartFrame = startFrame;
		}

		internal EntityEventKind Kind { get; }

		internal float X { get; }

		internal float Y { get; }

		internal byte Extra { get; }

		internal int StartFrame { get; }

		internal List<TrapDropEntryMsg> Drops { get; } = [];
	}

	private readonly List<PendingTrap> _pending = [];

	internal int Count => _pending.Count;

	internal void Enter(EntityEventKind kind, float x, float y, byte extra, int startFrame)
	{
		// A duplicate trigger report for the same trap position/kind must not
		// create two pending events (the trap patches already suppress most
		// duplicates; this is the last-resort guard).
		if (_pending.Any(p => p.Kind == kind && DistanceSq(p.X, p.Y, x, y) < MatchRadius * MatchRadius))
		{
			return;
		}

		_pending.Add(new PendingTrap(kind, x, y, extra, startFrame));
	}

	/// <summary>Folds one building-death drop into the nearest pending destructive trap; false when no pending trap matches, so the caller keeps the standalone spawn path.</summary>
	internal bool TryAddDrop(TrapDropEntryMsg drop)
	{
		PendingTrap? nearest = null;
		var bestDistance = MatchRadius * MatchRadius;
		foreach (var pending in _pending)
		{
			var distance = DistanceSq(pending.X, pending.Y, drop.Position.X, drop.Position.Y);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				nearest = pending;
			}
		}

		if (nearest is null)
		{
			return false;
		}

		nearest.Drops.Add(drop);
		return true;
	}

	/// <summary>Pulls every pending trap that has passed the hold window into ready EntityEventMsg payloads (oldest first).</summary>
	internal bool TryFlush(int currentFrame, out List<EntityEventMsg> flushed)
	{
		var ready = _pending
			.Where(p => currentFrame - p.StartFrame >= HoldFrames)
			.OrderBy(p => p.StartFrame)
			.ToList();

		if (ready.Count == 0)
		{
			flushed = [];
			return false;
		}

		_pending.RemoveAll(ready.Contains);
		flushed = [];
		foreach (var p in ready)
		{
			flushed.Add(new EntityEventMsg
			{
				Kind = p.Kind,
				Position = new NetVector2Msg(p.X, p.Y),
				Extra = p.Extra,
				Drops = p.Drops,
			});
		}

		return true;
	}

	internal void Reset() => _pending.Clear();

	private static float DistanceSq(float ax, float ay, float bx, float by)
	{
		var dx = ax - bx;
		var dy = ay - by;
		return dx * dx + dy * dy;
	}
}
