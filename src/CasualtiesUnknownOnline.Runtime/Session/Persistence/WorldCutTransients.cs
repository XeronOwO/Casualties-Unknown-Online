using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The pure steps of the transient policy: assembling ONE observation of an
/// instant, and naming what a cut must wait for or will not carry. The verdicts
/// themselves live in <see cref="WorldTransientPolicy"/> and the wait's deadline
/// state belongs to the caller that owns the armed request, so this class answers
/// questions about an observation instead of holding state.
/// </summary>
internal static class WorldCutTransients
{
	/// <summary>
	/// The observation of one instant: the Runtime probe's half (the CUO-owned
	/// in-flight state) and the adapter's half (the game-side windows the seam hands
	/// in). Both are needed — a class missing from either side would be a class the
	/// cut drops without naming.
	/// </summary>
	internal static List<WorldTransientCount> Observe(IWorldCutTransientProbe? probe, IReadOnlyList<WorldTransientCount>? live)
	{
		var observation = new List<WorldTransientCount>();
		if (probe is not null)
		{
			observation.AddRange(probe.Capture());
		}

		if (live is not null)
		{
			observation.AddRange(live);
		}

		return observation;
	}

	/// <summary>The first class an owner reported that the policy does not declare, or null. An undeclared class REFUSES the cut.</summary>
	internal static string? UndeclaredClass(IReadOnlyList<WorldTransientCount> observation) =>
		observation.Select(row => row.Key).FirstOrDefault(key => !WorldTransientPolicy.IsKnown(key));

	/// <summary>The pending classes the policy resolves before a cut is taken, in the policy table's order.</summary>
	internal static List<WorldTransientCount> WaitingFor(IReadOnlyList<WorldTransientCount> observation) =>
		[.. WorldTransientPolicy.Rows
			.Where(row => row.Verdict == WorldTransientVerdict.ResolveBeforeSave)
			.Select(row => new WorldTransientCount(row.Key, PendingOf(observation, row.Key)))
			.Where(row => row.Pending > 0)];

	/// <summary>
	/// Everything this cut will NOT carry, named with its count and unit: the
	/// policy's `drop-with-log` rows that are pending, plus any waiting row the
	/// caller could not resolve before its deadline.
	/// </summary>
	internal static List<string> Dropped(
		IReadOnlyList<WorldTransientCount> observation,
		IReadOnlyList<WorldTransientCount> deadlineExceeded)
	{
		var dropped = new List<string>();
		foreach (var row in WorldTransientPolicy.Rows.Where(row => row.Verdict == WorldTransientVerdict.DropWithLog))
		{
			var pending = PendingOf(observation, row.Key);
			if (pending > 0)
			{
				dropped.Add(WorldTransientPolicy.Describe(new WorldTransientCount(row.Key, pending)));
			}
		}

		foreach (var row in deadlineExceeded)
		{
			dropped.Add(WorldTransientPolicy.Describe(row));
		}

		return dropped;
	}

	/// <summary>How many units of one class the observation reports (two halves reporting the same class are summed).</summary>
	private static int PendingOf(IReadOnlyList<WorldTransientCount> observation, string key)
	{
		var pending = 0;
		foreach (var row in observation)
		{
			if (string.Equals(row.Key, key, StringComparison.Ordinal))
			{
				pending += row.Pending;
			}
		}

		return pending;
	}
}
