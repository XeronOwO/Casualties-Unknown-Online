using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The enemy state stream's delta rule. The broadcast used to carry EVERY enemy on every tick — a flat 4,005 B per
/// frame against a world of ~50 mostly idle crawlers, which the host's own traffic line showed as the largest single
/// carrier of a ~200 KB/s send rate while nothing moved (measured 2026-11-02).
///
/// <para>
/// The stream is update-only on the receiving side (an id absent from a batch keeps its last values, never removed), so
/// a subset is legal. What a subset cannot survive is a LOST packet: with `reliable: false` a dropped delta leaves that
/// enemy stale until it next moves. Hence the keyframe: every <see cref="KeyframeTicks"/>-th tick sends everything, which
/// bounds staleness to a second instead of to "until it moves again".
/// </para>
///
/// <para>
/// Unity-free on purpose: the comparison is the whole rule and it is unit-tested.
/// </para>
/// </summary>
internal static class EnemyStateDelta
{
	/// <summary>How far a field may move before it counts as a change. Position and velocity are world units per
	/// second; rotation is degrees; health is a 0-100 bar. The values are the visible resolution, not the simulation's:
	/// a crawler that shifted a hundredth of a unit is not a frame anybody can see.</summary>
	internal const float PositionEpsilon = 0.05f;

	internal const float VelocityEpsilon = 0.05f;

	internal const float RotationEpsilon = 0.5f;

	internal const float HealthEpsilon = 0.5f;

	/// <summary>Every twentieth tick is a full snapshot — at the stream's 20 Hz that is one per second.</summary>
	internal const int KeyframeTicks = 20;

	/// <summary>True when this tick must carry every enemy: the keyframe cadence, or the first tick of a session.</summary>
	internal static bool SendAll(long tick) => tick % KeyframeTicks == 0;

	/// <summary>
	/// True when <paramref name="next"/> differs from <paramref name="previous"/> in a field the stream carries, or when
	/// there is no previous value at all (a new enemy always goes out).
	/// </summary>
	internal static bool Changed(WireEnemyStreamState? previous, WireEnemyStreamState next)
	{
		if (previous is null)
		{
			return true;
		}

		if (next is null)
		{
			throw new ArgumentNullException(nameof(next));
		}

		return !Near(previous.Position, next.Position, PositionEpsilon)
			|| !Near(previous.Velocity, next.Velocity, VelocityEpsilon)
			|| Math.Abs(previous.Rotation - next.Rotation) > RotationEpsilon
			|| Math.Abs(previous.Health - next.Health) > HealthEpsilon
			|| previous.PresentationFlags != next.PresentationFlags
			|| !SameLine(previous.CrystalLineEnd, next.CrystalLineEnd);
		// SpiderLegTargets and CrystalWindupAmount are deliberately NOT in this test: they are per-frame animation, and
		// a crawler's legs are re-solved every tick, so including them made every enemy "changed" every frame — the
		// stream kept its flat 4,005 B payload after the delta landed (measured 2026-11-02). They still travel whenever
		// the enemy is sent for a real reason, and on every keyframe.
	}

	/// <summary>
	/// The entries to put on the wire this tick: everything on a keyframe, otherwise only the changed ones. The caller
	/// keeps <paramref name="previous"/> up to date with what it actually sent.
	/// </summary>
	internal static List<WireEnemyStreamState> Select(
		IEnumerable<WireEnemyStreamState> all,
		IReadOnlyDictionary<(ulong Epoch, uint Counter, byte Generation), WireEnemyStreamState> previous,
		long tick)
	{
		var sendAll = SendAll(tick);
		var selected = new List<WireEnemyStreamState>();
		foreach (var state in all)
		{
			var key = (state.EntityId.Epoch, state.EntityId.Counter, state.EntityId.Generation);
			if (sendAll || Changed(Lookup(previous, key), state))
			{
				selected.Add(state);
			}
		}

		return selected;
	}

	private static WireEnemyStreamState? Lookup(
		IReadOnlyDictionary<(ulong Epoch, uint Counter, byte Generation), WireEnemyStreamState> previous,
		(ulong Epoch, uint Counter, byte Generation) key) =>
		previous.TryGetValue(key, out var found) ? found : null;

	private static bool Near(WireVector2 a, WireVector2 b, float epsilon) =>
		Math.Abs(a.X - b.X) <= epsilon && Math.Abs(a.Y - b.Y) <= epsilon;

	private static bool SameLine(WireVector2? a, WireVector2? b)
	{
		if (a is null || b is null)
		{
			return a is null && b is null;
		}

		return Near(a, b, PositionEpsilon);
	}

	private static bool SameLegs(List<WireVector2>? a, List<WireVector2>? b)
	{
		if (a is null || b is null)
		{
			return a is null && b is null;
		}

		if (a.Count != b.Count)
		{
			return false;
		}

		for (var i = 0; i < a.Count; i++)
		{
			if (!Near(a[i], b[i], PositionEpsilon))
			{
				return false;
			}
		}

		return true;
	}
}
