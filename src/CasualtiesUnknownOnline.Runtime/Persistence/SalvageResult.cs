using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The per-entry salvage outcome §6 requires the in-game surface to show: the
/// damage report of the decode pass, the counts each domain file lost and the
/// names of the affected entries. A non-zero count is a warning the player must
/// be able to read, never a log-only footnote.
/// </summary>
public sealed record SalvageResult(DamageReport Report)
{
	/// <summary>Untranslatable entries per domain file, in first-seen order; empty when nothing was skipped.</summary>
	public IReadOnlyDictionary<string, int> SkippedByFile { get; } = Report.Entries
		.Where(entry => entry.Scope == DamageReport.EntryScope.Entry)
		.GroupBy(entry => entry.Path, StringComparer.Ordinal)
		.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

	/// <summary>The skipped entries themselves, path and id included.</summary>
	public IReadOnlyList<DamageReport.Entry> SkippedEntries { get; } =
		[.. Report.Entries.Where(entry => entry.Scope == DamageReport.EntryScope.Entry)];

	/// <summary>True = nothing was skipped by the decode pass.</summary>
	public bool IsClean => SkippedEntries.Count == 0;
}
