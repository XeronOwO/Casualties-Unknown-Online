using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The outcome of a backup retention pass (§7). Retention never deletes the
/// newest archive, and a prune failure is reported instead of being allowed to
/// break the world — the list is what is actually on disk afterwards.
/// </summary>
public sealed record BackupPruneResult(
	IReadOnlyList<WorldBackup> Kept,
	IReadOnlyList<WorldBackup> Deleted,
	IReadOnlyList<string> Failures)
{
	/// <summary>True = every requested deletion succeeded (or none was requested).</summary>
	public bool Clean => Failures.Count == 0;
}
