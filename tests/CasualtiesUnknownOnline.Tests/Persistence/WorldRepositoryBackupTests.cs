using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 case 13: retention keeps the newest N, prunes oldest-first and never
/// deletes the newest archive — including the degenerate retentions (0, 1, more
/// than exist), a negative retention and the all-unreadable backup case.
/// </summary>
public class WorldRepositoryBackupTests
{
	[Fact]
	public void Retention_KeepsTheNewestNArchivesAndPrunesOldestFirst()
	{
		var fixture = SaveTestRepository.Create("retention");
		WriteBackups(fixture, 5);

		var result = fixture.Repository.PruneBackups(fixture.WorldId, 2);

		Assert.True(result.Clean, string.Join(", ", result.Failures));
		Assert.Equal(2, result.Kept.Count);
		Assert.Equal(3, result.Deleted.Count);
		var remaining = fixture.Repository.ListBackups(fixture.WorldId);
		Assert.Equal(result.Kept.Select(backup => backup.FileName), remaining.Select(backup => backup.FileName));
		Assert.All(remaining, backup => Assert.True(File.Exists(backup.FullPath)));
		Assert.All(result.Deleted, backup => Assert.False(File.Exists(backup.FullPath)));
	}

	[Fact]
	public void RetentionZero_StillKeepsTheNewestArchive()
	{
		var fixture = SaveTestRepository.Create("retention-zero");
		WriteBackups(fixture, 3);

		var result = fixture.Repository.PruneBackups(fixture.WorldId, 0);

		Assert.True(result.Clean, string.Join(", ", result.Failures));
		var remaining = fixture.Repository.ListBackups(fixture.WorldId);
		Assert.Single(remaining);
		Assert.Equal(result.Kept.Single().FileName, remaining[0].FileName);
		Assert.Equal(2, result.Deleted.Count);
	}

	[Fact]
	public void RetentionOneAndAboveTheArchiveCount_AreBothSafe()
	{
		var fixture = SaveTestRepository.Create("retention-bounds");
		WriteBackups(fixture, 2);

		var keepMore = fixture.Repository.PruneBackups(fixture.WorldId, 10);
		Assert.True(keepMore.Clean, string.Join(", ", keepMore.Failures));
		Assert.Equal(2, keepMore.Kept.Count);
		Assert.Empty(keepMore.Deleted);

		var keepOne = fixture.Repository.PruneBackups(fixture.WorldId, 1);
		Assert.True(keepOne.Clean, string.Join(", ", keepOne.Failures));
		Assert.Single(keepOne.Kept);
		Assert.Single(keepOne.Deleted);
	}

	[Fact]
	public void RetentionOnAWorldWithoutBackups_IsANoOp()
	{
		var fixture = SaveTestRepository.Create("retention-empty");

		var result = fixture.Repository.PruneBackups(fixture.WorldId, 3);

		Assert.True(result.Clean);
		Assert.Empty(result.Kept);
		Assert.Empty(result.Deleted);
	}

	[Fact]
	public void NegativeRetention_IsReportedAndPrunesNothing()
	{
		var fixture = SaveTestRepository.Create("retention-negative");
		WriteBackups(fixture, 2);

		var result = fixture.Repository.PruneBackups(fixture.WorldId, -1);

		Assert.False(result.Clean);
		Assert.Equal(2, result.Kept.Count);
		Assert.Empty(result.Deleted);
		Assert.Single(result.Failures);
	}

	[Fact]
	public void ListedBackups_AreNewestFirstAndIgnoreForeignFiles()
	{
		var fixture = SaveTestRepository.Create("retention-listing");
		var backups = WriteBackups(fixture, 3);
		File.WriteAllText(Path.Combine(fixture.Workspace.BackupsDirectory(fixture.WorldId), "notes.txt"), "not an archive");
		File.WriteAllText(Path.Combine(fixture.Workspace.BackupsDirectory(fixture.WorldId), "backup.cuoz"), "not a name we produce");

		var listed = fixture.Repository.ListBackups(fixture.WorldId);

		Assert.Equal(3, listed.Count);
		Assert.Equal(backups[2].FileName, listed[0].FileName);
		Assert.Equal(backups[0].FileName, listed[2].FileName);
		Assert.All(listed, backup => Assert.True(SaveArchiveFormat.TryParseBackupFileName(backup.FileName, out _, out _)));
	}

	[Fact]
	public void BackupNames_UseTheKindStampGrammarAndDisambiguateWithinASecond()
	{
		var fixture = SaveTestRepository.Create("retention-names");

		var backups = WriteBackups(fixture, 2);

		Assert.Matches("^layer-end-[0-9]{8}-[0-9]{6}\\.cuoz$", backups[0].FileName);
		Assert.Equal(WorldCutKind.LayerEnd, backups[0].Kind);
		Assert.Equal("layer-end-20260910-120000-2", backups[1].Stem);
		Assert.True(SaveArchiveFormat.TryParseBackupFileName(backups[1].FileName, out var kind, out var stem));
		Assert.Equal(WorldCutKind.LayerEnd, kind);
		Assert.Equal(backups[1].Stem, stem);
		Assert.True(File.Exists(backups[1].FullPath));

		Assert.False(SaveArchiveFormat.TryParseBackupFileName("layer-end-2026091-120000.cuoz", out _, out _));
		Assert.False(SaveArchiveFormat.TryParseBackupFileName("mystery-20260910-120000.cuoz", out _, out _));
	}

	[Fact]
	public void AllUnreadableBackups_ProduceAFailedLoadThatNamesThem()
	{
		var fixture = SaveTestRepository.Create("retention-unreadable");
		WriteBackups(fixture, 2);
		foreach (var backup in fixture.Repository.ListBackups(fixture.WorldId))
		{
			File.WriteAllBytes(backup.FullPath, SaveTestData.Bytes("this is not a zip archive"));
		}

		// The live snapshot is unusable too, so the fallback runs and finds only
		// unreadable archives — the load must fail and name every one of them.
		File.WriteAllBytes(fixture.ManifestPath, SaveTestData.Bytes("{ broken"));

		var load = fixture.Repository.LoadSnapshot(fixture.WorldId, new WorldLoadOptions());

		Assert.False(load.Loaded);
		Assert.Equal(WorldLoadState.Failed, load.State);
		Assert.Equal(3, load.Report.Entries.Count(entry => entry.Reason == DamageReport.EntryReason.NoReadableBackup));
		Assert.Single(load.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.ManifestUnreadable);
		Assert.All(fixture.Repository.ListBackups(fixture.WorldId), backup =>
			Assert.Contains(load.Report.Entries, entry => string.Equals(entry.Id, backup.FileName, StringComparison.Ordinal)));
	}

	[Fact]
	public void BackupWriteDoesNotLeaveATemporaryFile()
	{
		var fixture = SaveTestRepository.Create("retention-temp");

		WriteBackups(fixture, 2);

		Assert.Empty(Directory.GetFiles(fixture.Workspace.BackupsDirectory(fixture.WorldId), "*.tmp"));
		Assert.All(fixture.Repository.ListBackups(fixture.WorldId), backup => Assert.True(new FileInfo(backup.FullPath).Length > 0));
	}

	/// <summary>Writes <paramref name="count"/> cuts inside the same second, so every name after the first needs a suffix.</summary>
	private static List<WorldBackup> WriteBackups(SaveTestRepository fixture, int count)
	{
		var backups = new List<WorldBackup>(count);
		for (var index = 0; index < count; index++)
		{
			var result = fixture.Repository.WriteSnapshot(fixture.WorldId,
				fixture.Request(WorldCutKind.LayerEnd, fixture.Now, SaveTestData.RunPayload("cut-" + index)));
			Assert.True(result.Success, result.Detail);
			backups.Add(WorldBackup.TryParse(fixture.Workspace.BackupsDirectory(fixture.WorldId), Path.GetFileName(result.BackupArchivePath!))!);
		}

		return backups;
	}
}
