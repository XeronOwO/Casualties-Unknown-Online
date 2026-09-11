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
	/// Everything this cut will NOT carry, named so the report can say it: the
	/// policy's `drop-with-log` rows that a CUO owner counted, any waiting row the
	/// caller could not resolve before its deadline, the classes the policy marks
	/// <see cref="WorldTransientDetection.Standing"/> (no CUO counter exists, so no
	/// count is claimed — the class itself is named), and the decided native values
	/// when this build has no reader for them.
	/// </summary>
	internal static List<string> Dropped(
		IReadOnlyList<WorldTransientCount> observation,
		IReadOnlyList<WorldTransientCount> deadlineExceeded,
		bool nativeReaderAvailable)
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

		var standing = WorldTransientPolicy.Rows
			.Where(row => row.Verdict == WorldTransientVerdict.DropWithLog && row.Detection == WorldTransientDetection.Standing)
			.Select(row => row.Unit)
			.ToList();
		if (standing.Count > 0)
		{
			dropped.Add($"no mid-run cut carries {string.Join(", ", standing)} (the game owns the state and CUO has no counter for it)");
		}

		if (!nativeReaderAvailable)
		{
			dropped.Add("this build has no native world-fact reader, so the decided keypad/geyser values and the game's own partial damage are not carried");
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
