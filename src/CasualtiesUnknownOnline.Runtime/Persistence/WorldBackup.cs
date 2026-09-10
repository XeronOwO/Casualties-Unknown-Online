using System;
using System.Collections.Generic;
using System.IO;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The name grammar of backups (<c>&lt;kind&gt;-&lt;stamp&gt;[-n].cuoz</c>, §2).
/// A second save inside the same second gets a <c>-n</c> suffix, so two cuts can
/// never collide and overwrite each other; retention (§7) then sees exactly one
/// file per snapshot.
/// </summary>
public sealed record WorldBackup(string FileName, string FullPath, WorldCutKind Kind, string Stem)
{
	/// <summary>The cut's stamp (<c>yyyyMMdd-HHmmss</c>) — the field that orders archives.</summary>
	public string Stamp { get; } = SaveArchiveFormat.StampOfStem(Stem);

	/// <summary>The <c>-n</c> disambiguator (0 when the name has none): the order of cuts inside one second.</summary>
	public int Suffix { get; } = SaveArchiveFormat.SuffixOfStem(Stem);

	/// <summary>The name a cut at <paramref name="utc"/> wants; the first free variant when that name is taken.</summary>
	internal static WorldBackup Create(string backupsDirectory, WorldCutKind kind, DateTime utc)
	{
		var baseStem = SaveArchiveFormat.BuildBackupStem(kind, utc);
		var stem = baseStem;
		for (var suffix = 2; File.Exists(FullPathOf(backupsDirectory, stem)); suffix++)
		{
			stem = $"{baseStem}-{suffix}";
		}

		return new WorldBackup(stem + SaveArchiveFormat.BackupExtension, FullPathOf(backupsDirectory, stem), kind, stem);
	}

	/// <summary>Parses an existing backup file name; null = not a backup of this format.</summary>
	internal static WorldBackup? TryParse(string backupsDirectory, string fileName) =>
		SaveArchiveFormat.TryParseBackupFileName(fileName, out var kind, out var stem)
			? new WorldBackup(fileName, Path.Combine(backupsDirectory, fileName), kind, stem)
			: null;

	/// <summary>
	/// Newest-first by the stamp inside the name — what §6's "newest readable
	/// backup" and §7's retention mean. The stamp is compared, never the whole stem:
	/// kind names sort arbitrarily against each other, so comparing stems would put
	/// an older <c>mid-run</c> cut in front of a newer <c>auto</c> one.
	/// </summary>
	internal static IReadOnlyList<WorldBackup> SortedNewestFirst(IEnumerable<WorldBackup> backups)
	{
		var sorted = new List<WorldBackup>(backups);
		sorted.Sort(CompareNewestFirst);
		return sorted;
	}

	private static int CompareNewestFirst(WorldBackup left, WorldBackup right)
	{
		var byStamp = string.CompareOrdinal(right.Stamp, left.Stamp);
		if (byStamp != 0)
		{
			return byStamp;
		}

		// Same second: the disambiguating suffix is the order ("…-10" is newer than
		// "…-2", which a plain string comparison gets backwards).
		var bySuffix = right.Suffix.CompareTo(left.Suffix);
		return bySuffix != 0 ? bySuffix : string.CompareOrdinal(right.FileName, left.FileName);
	}

	private static string FullPathOf(string backupsDirectory, string stem) =>
		Path.Combine(backupsDirectory, stem + SaveArchiveFormat.BackupExtension);
}
