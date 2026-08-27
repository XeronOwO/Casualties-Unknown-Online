using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Projections;

/// <summary>
/// The human-readable result of comparing two terminal-fact projections.
/// Empty means the two projections agree.
/// </summary>
public sealed class ItemTerminalDiff(IReadOnlyList<string> differences)
{
	public IReadOnlyList<string> Differences { get; } = differences;

	public bool HasDifferences => Differences.Count > 0;
}
