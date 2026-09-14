using System;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The itemized form of a damage report (§6's in-game surface): one line per
/// reported item, carrying the affected content id. The grouped one-line
/// <see cref="DamageReport.Describe"/> says "2 untranslatable entry(ies) skipped
/// in items.json" — enough for a log, not enough for the player who has to know
/// WHICH item is gone, which is why the surface prints these lines under it.
/// </summary>
public class DamageReportLineTests
{
	[Fact]
	public void EmptyReport_HasNoDetailLines() => Assert.Empty(DamageReport.Empty.DescribeLines());

	[Fact]
	public void SkippedEntry_NamesItsContentIdDomainFileAndReason()
	{
		var report = new DamageReport([
			new DamageReport.Entry(
				DamageReport.EntryScope.Entry,
				DamageReport.EntryReason.ContentMissing,
				SaveArchiveFormat.ItemsFileName,
				"bandage",
				"the item definition is absent from the current content set"),
		]);

		var line = Assert.Single(report.DescribeLines());

		Assert.Contains("bandage", line, StringComparison.Ordinal);
		Assert.Contains(SaveArchiveFormat.ItemsFileName, line, StringComparison.Ordinal);
		Assert.Contains(nameof(DamageReport.EntryReason.ContentMissing), line, StringComparison.Ordinal);
		Assert.Contains("the item definition is absent", line, StringComparison.Ordinal);
	}

	[Fact]
	public void UnusableFile_NamesTheFileAndTheReason()
	{
		var report = new DamageReport([
			new DamageReport.Entry(
				DamageReport.EntryScope.File,
				DamageReport.EntryReason.ChecksumMismatch,
				SaveArchiveFormat.WorldBlocksFileName,
				string.Empty,
				"sha256 does not match the manifest"),
		]);

		var line = Assert.Single(report.DescribeLines());

		Assert.Contains(SaveArchiveFormat.WorldBlocksFileName, line, StringComparison.Ordinal);
		Assert.Contains(nameof(DamageReport.EntryReason.ChecksumMismatch), line, StringComparison.Ordinal);
	}

	[Fact]
	public void BackupFallback_NamesTheArchiveTheLoadOpened()
	{
		// §6's "fell back to backup X" has to be readable as a message: the entry's id IS
		// the backup file's name, and the detail says what happened.
		var report = new DamageReport([
			new DamageReport.Entry(
				DamageReport.EntryScope.Repository,
				DamageReport.EntryReason.ManifestUnreadable,
				string.Empty,
				"2026-09-10T12-00-00-menu-return-0" + SaveArchiveFormat.BackupExtension,
				"the live snapshot's manifest is unreadable, so that backup was opened instead"),
		]);

		var line = Assert.Single(report.DescribeLines());

		Assert.Contains(SaveArchiveFormat.BackupExtension, line, StringComparison.Ordinal);
		Assert.Contains("was opened instead", line, StringComparison.Ordinal);
	}

	[Fact]
	public void RepositoryEntryWithoutAnId_StaysReadable()
	{
		var report = new DamageReport([
			new DamageReport.Entry(
				DamageReport.EntryScope.Repository,
				DamageReport.EntryReason.CrashLeftoverStaging,
				string.Empty,
				string.Empty,
				"an interrupted write left a staging folder; it was discarded"),
		]);

		var line = Assert.Single(report.DescribeLines());

		Assert.Contains(nameof(DamageReport.EntryReason.CrashLeftoverStaging), line, StringComparison.Ordinal);
		Assert.DoesNotContain("()", line, StringComparison.Ordinal);
	}
}
