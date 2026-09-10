using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Everything that was lost, skipped or recovered while opening a snapshot —
/// the in-game surface §6 demands ("every skipped entry, every fallback and
/// every mismatch is surfaced, silent loss is forbidden"). The report is the
/// only channel for that information: the format layer logs the same facts, but
/// never instead of returning them.
/// </summary>
public sealed record DamageReport(IReadOnlyList<DamageReport.Entry> Entries)
{
	/// <summary>An empty report: the snapshot opened exactly as written.</summary>
	public static DamageReport Empty { get; } = new([]);

	/// <summary>True = a reader that cannot tolerate loss must refuse this snapshot.</summary>
	public bool LosesContent => Entries.Any(entry => entry.IsLoss);

	/// <summary>
	/// A one-line account for the in-game surface and the load log: fallbacks and
	/// damaged files first, then salvage counts grouped by the domain file they
	/// came from.
	/// </summary>
	public string Describe()
	{
		if (Entries.Count == 0)
		{
			return "opened without damage";
		}

		var parts = new List<string>(Entries.Count + 1)
		{
			Entries.Any(entry => entry.IsLoss)
				? $"{Entries.Count} item(s) reported, content was lost"
				: $"{Entries.Count} item(s) reported, no content lost",
		};

		parts.AddRange(Entries
			.Where(entry => entry.Scope == EntryScope.Repository)
			.Select(entry => $"{entry.Reason}: {entry.Detail}"));

		parts.AddRange(Entries
			.Where(entry => entry.Scope == EntryScope.File)
			.Select(entry => $"{entry.Reason} in {entry.Path}: {entry.Detail}"));

		parts.AddRange(Entries
			.Where(entry => entry.Scope == EntryScope.Entry)
			.GroupBy(entry => entry.Path, StringComparer.Ordinal)
			.Select(group =>
			{
				var reasons = string.Join(", ", group.Select(entry => entry.Reason).Distinct());
				return $"{group.Count()} untranslatable entry(ies) skipped in {group.Key} ({reasons})";
			}));

		return string.Join("; ", parts);
	}

	/// <summary>How far the damaged thing reaches: the repository, one file, or one entry inside a file.</summary>
	public enum EntryScope
	{
		/// <summary>Not tied to a file: a fallback, a crash leftover, a version mismatch.</summary>
		Repository,

		/// <summary>A whole file could not be used.</summary>
		File,

		/// <summary>One entry inside a readable file could not be materialized.</summary>
		Entry,
	}

	/// <summary>
	/// Why something was skipped. The set is closed on purpose: a repair path that
	/// cannot name its cause cannot be shown in-game.
	/// </summary>
	public enum EntryReason
	{
		/// <summary>An interrupted write left a staging folder; it was discarded.</summary>
		CrashLeftoverStaging,

		/// <summary>An interrupted write left a previous snapshot; it was restored.</summary>
		CrashLeftoverPrevious,

		/// <summary>The live manifest could not be read or parsed; the newest readable backup was opened instead.</summary>
		ManifestUnreadable,

		/// <summary>No backup archive could be opened either.</summary>
		NoReadableBackup,

		/// <summary>The per-file <c>schemaVersion</c> inside a domain file is newer than this reader understands (one entry).</summary>
		EntrySchemaNewer,

		/// <summary>The file's bytes do not match the manifest's digest.</summary>
		ChecksumMismatch,

		/// <summary>The file listed in the manifest could not be read.</summary>
		FileUnreadable,

		/// <summary>The file's JSON is not valid for its schema.</summary>
		FileInvalidJson,

		/// <summary>A domain decoder rejected the file as a whole.</summary>
		FileDecodeFailed,

		/// <summary>The entry's content id is absent from the current content set (a mod update removed it).</summary>
		ContentMissing,

		/// <summary>A domain decoder rejected one entry.</summary>
		DecoderRejected,

		/// <summary>A decoder callback itself threw while handling the entry.</summary>
		DecoderThrew,

		/// <summary>Repair mode is off and a file is damaged, so the whole snapshot is refused by policy.</summary>
		DisabledByPolicy,

		/// <summary>The snapshot was written by a different protocol version; it opens, but entities may not restore (§6.1).</summary>
		ProtocolMismatch,
	}

	/// <summary>One reported item.</summary>
	public sealed record Entry(EntryScope Scope, EntryReason Reason, string Path, string Id, string Detail)
	{
		/// <summary>True = skipping this item drops content (a crash leftover that was restored does not).</summary>
		public bool IsLoss => Scope != EntryScope.Repository || Reason == EntryReason.NoReadableBackup;
	}
}
