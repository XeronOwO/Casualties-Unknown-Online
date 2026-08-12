using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>The per-item follow decision for one frame (PURE data — the
/// GameAdapter executes the writes; the decision carries the target values
/// it needs).</summary>
internal readonly struct FollowDecision
{
	/// <summary>Frozen (no stream tick yet — never pumped), Settled (ease the
	/// residual gap away), Moving (local physics runs from the host's velocity).</summary>
	internal FollowMode Mode { get; init; }

	/// <summary>Moving mode and the divergence exceeds the snap threshold — hard-snap the copy
	/// to the host's state (position + rotation + velocity, the local inertia is discarded).</summary>
	internal bool HardSnap { get; init; }

	/// <summary>Settled mode and the residual gap exceeds the settle threshold — ease toward the host's spot.</summary>
	internal bool EaseToTarget { get; init; }

	/// <summary>EaseToTarget: the Lerp coefficient for this frame (clamp01(deltaTime × rate)).</summary>
	internal float EaseK { get; init; }

	/// <summary>Settled mode and the gap exceeds the diagnostic threshold — worth a log line.</summary>
	internal bool LogDivergence { get; init; }

	/// <summary>The divergence this decision was computed from (the adapter's log lines).</summary>
	internal float Dist { get; init; }

	internal float TargetX { get; init; }
	internal float TargetY { get; init; }
	internal float TargetRot { get; init; }
	internal float VelX { get; init; }
	internal float VelY { get; init; }
	internal float AngVel { get; init; }
}

/// <summary>
/// The guest-side world-item follow machine (PURE — no Unity, time and
/// positions are explicit inputs): the id → authoritative-target table the
/// host's 10 Hz stream feeds, and the per-frame decision (frozen / ease-to-rest
/// / velocity-sync / hard-snap) computed from the target and the copy's current
/// state. The GameAdapter's ItemPositionFollow owns the scene writes — this
/// machine is what the tests lock.
/// </summary>
internal sealed class ItemFollowDecision
{
	private readonly Dictionary<ulong, FollowTarget> _targets = [];

	internal int Count => _targets.Count;

	/// <summary>The tracked item ids (a snapshot — the adapter iterates it and may
	/// <see cref="Remove"/> while walking, so it must copy).</summary>
	internal IEnumerable<ulong> Keys => _targets.Keys;

	/// <summary>The stream delivered a target — store it and mark the copy
	/// playable (the first tick after a freeze switches it to local physics).
	/// Returns true when the target is NEW (the adapter's start-parity align
	/// runs once).</summary>
	internal bool UpdateTarget(ulong itemId, float x, float y, float velX, float velY, float rot, float angVel)
	{
		var isNew = !_targets.TryGetValue(itemId, out var t);
		if (isNew)
		{
			t = new FollowTarget();
			_targets[itemId] = t;
		}

		t!.X = x;
		t.Y = y;
		t.VelX = velX;
		t.VelY = velY;
		t.Rot = rot;
		t.AngVel = angVel;
		t.Played = true;
		return isNew;
	}

	/// <summary>The copy left the world domain (picked up / destroyed / no longer a
	/// world item) — stop following it.</summary>
	internal void Remove(ulong itemId) => _targets.Remove(itemId);

	/// <summary>All targets gone (session end, unbind).</summary>
	internal void Clear() => _targets.Clear();

	/// <summary>
	/// The frame's decision for one copy: the current position/rotation are game
	/// inputs (the adapter reads the transform), deltaTime the game's time input.
	/// A copy with no target or not yet streamed is Frozen — never pumped.
	/// </summary>
	internal FollowDecision Decide(ulong itemId, float curX, float curY, float curRot, float deltaTime)
	{
		if (!_targets.TryGetValue(itemId, out var t) || !t!.Played)
		{
			return default;
		}

		var dx = t.X - curX;
		var dy = t.Y - curY;
		var dist = Sqrt(dx * dx + dy * dy);
		var decision = new FollowDecision
		{
			Mode = ItemMotionState.IsSettled(t.VelX * t.VelX + t.VelY * t.VelY, Abs(t.AngVel))
				? FollowMode.Settled
				: FollowMode.Moving,
			Dist = dist,
			TargetX = t.X,
			TargetY = t.Y,
			TargetRot = t.Rot,
			VelX = t.VelX,
			VelY = t.VelY,
			AngVel = t.AngVel,
		};

		if (decision.Mode == FollowMode.Settled)
		{
			var ease = dist > ItemMotionState.SettleSnapDistance;
			var k = ease ? deltaTime * ItemMotionState.SettleEaseRate : 0f;
			decision = decision with
			{
				EaseToTarget = ease,
				LogDivergence = dist > ItemMotionState.SettleLogDistance,
				EaseK = k > 1f ? 1f : (k < 0f ? 0f : k), // clamp01
			};
		}
		else
		{
			decision = decision with { HardSnap = dist > ItemMotionState.SnapDistance };
		}

		return decision;
	}

	private static float Sqrt(float v) => (float)System.Math.Sqrt(v);

	private static float Abs(float v) => v < 0f ? -v : v;

	/// <summary>id → the host's authoritative move target; Played=false = still frozen
	/// (kinematic, no stream yet) — never pumped.</summary>
	private sealed class FollowTarget
	{
		public float X;
		public float Y;
		public float VelX;
		public float VelY;
		public float Rot;
		public float AngVel;
		public bool Played;
	}
}
