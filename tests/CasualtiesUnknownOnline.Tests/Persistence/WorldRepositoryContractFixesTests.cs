using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// Defects an independent adversarial review of S1 found in the world repository
/// and this stage fixed: an unvalidated world id could create folders and delete
/// archives outside the repository root, and backups were ordered by file name
/// (kind first) instead of by their stamp, so retention and the fallback picked
/// the wrong archive.
/// </summary>
public class WorldRepositoryContractFixesTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Repository_DoesNotCreateAFolderOutsideItsRootForAnUnsafeWorldId()
	{
		var workspace = SaveTestWorkspace.Create("fix-worldid-write");
		var escapedId = ".." + Path.DirectorySeparatorChar + "escape-probe";
		var escaped = Path.Combine(Path.GetDirectoryName(workspace.Root)!, "escape-probe");
		Assert.False(Directory.Exists(escaped));

		var repository = new WorldRepository(workspace.Root, NullLogger<WorldRepository>.Instance, Writer(), Reader(), () => Cut);
		var result = repository.WriteSnapshot(escapedId, SaveTestData.Request(escapedId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("x")));

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.InvalidRequest, result.Reason);
		Assert.False(Directory.Exists(escaped), "WriteSnapshot created a folder outside the repository root");
	}

	[Fact]
	public void Repository_DoesNotPruneBackupsOutsideItsRoot()
	{
		var workspace = SaveTestWorkspace.Create("fix-worldid-prune");
		var victim = Path.Combine(Path.GetDirectoryName(workspace.Root)!, "victim-probe", "backups");
		Directory.CreateDirectory(victim);
		var older = Path.Combine(victim, "layer-end-20260101-000000.cuoz");
		File.WriteAllBytes(older, [1]);
		File.WriteAllBytes(Path.Combine(victim, "layer-end-20260102-000000.cuoz"), [2]);

		var repository = new WorldRepository(workspace.Root, NullLogger<WorldRepository>.Instance, Writer(), Reader(), () => Cut);
		var pruned = repository.PruneBackups(".." + Path.DirectorySeparatorChar + "victim-probe", 1);

		Assert.False(pruned.Clean);
		Assert.True(File.Exists(older), "PruneBackups deleted a file outside the repository root");
	}

	[Fact]
	public void Repository_RefusesAnUnsafeWorldIdOnEveryEntryPoint()
	{
		var fixture = SaveTestRepository.Create("fix-worldid-everywhere");
		var escapedId = ".." + Path.DirectorySeparatorChar + "elsewhere";

		Assert.False(fixture.Repository.LoadSnapshot(escapedId, new WorldLoadOptions()).Loaded);
		Assert.False(fixture.Repository.RenameWorld(escapedId, "Renamed"));
		Assert.False(fixture.Repository.SetLastOpenedWorld(escapedId));
		Assert.Empty(fixture.Repository.ListBackups(escapedId));
		Assert.Throws<SaveArchivePathException>(() => fixture.Repository.PathOfWorld(escapedId));
	}

	[Fact]
	public void Backups_AreListedNewestFirstAcrossKinds()
	{
		var fixture = SaveTestRepository.Create("fix-backup-order");
		Assert.True(fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.MidRun, fixture.Now.AddHours(-2), SaveTestData.RunPayload("older"))).Success);
		Assert.True(fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.Auto, fixture.Now, SaveTestData.RunPayload("newer"))).Success);

		var listed = fixture.Repository.ListBackups(fixture.WorldId);

		Assert.Equal(2, listed.Count);
		Assert.StartsWith("auto-", listed[0].FileName, StringComparison.Ordinal);
	}

	[Fact]
	public void Retention_KeepsTheChronologicallyNewestArchive()
	{
		var fixture = SaveTestRepository.Create("fix-retention-order");
		Assert.True(fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.MidRun, fixture.Now.AddHours(-2), SaveTestData.RunPayload("older"))).Success);
		var newest = fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.Auto, fixture.Now, SaveTestData.RunPayload("newer")));
		Assert.True(newest.Success, newest.Detail);

		var pruned = fixture.Repository.PruneBackups(fixture.WorldId, 1);

		Assert.True(File.Exists(newest.BackupArchivePath!), "retention deleted the newest archive of the world");
		Assert.Contains(pruned.Kept, backup => string.Equals(backup.FullPath, newest.BackupArchivePath, StringComparison.Ordinal));
	}

	[Fact]
	public void BackupFallback_OpensTheChronologicallyNewestBackup()
	{
		var fixture = SaveTestRepository.Create("fix-fallback-order");
		Assert.True(fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.MidRun, fixture.Now.AddHours(-2), SaveTestData.RunPayload("older"))).Success);
		var newestPayload = SaveTestData.RunPayload("newer");
		Assert.True(fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.Auto, fixture.Now, newestPayload)).Success);
		File.WriteAllBytes(fixture.ManifestPath, SaveTestData.Bytes("{ broken"));

		var load = fixture.Repository.LoadSnapshot(fixture.WorldId, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.BackupFallback, load.State);
		var loaded = load.Content!.Files.Single(file => string.Equals(file.Path, SaveTestData.RunFileName, StringComparison.Ordinal));
		Assert.Equal(newestPayload.Content, loaded.Bytes);
	}

	[Fact]
	public void SameSecondArchives_AreOrderedByTheirDisambiguatingSuffix()
	{
		var fixture = SaveTestRepository.Create("fix-suffix-order");
		var first = fixture.Repository.WriteSnapshot(fixture.WorldId, fixture.Request(WorldCutKind.LayerEnd, fixture.Now, SaveTestData.RunPayload("one")));
		var second = fixture.Repository.WriteSnapshot(fixture.WorldId, fixture.Request(WorldCutKind.LayerEnd, fixture.Now, SaveTestData.RunPayload("two")));
		Assert.True(first.Success && second.Success);

		var listed = fixture.Repository.ListBackups(fixture.WorldId);

		Assert.Equal(Path.GetFileName(second.BackupArchivePath!), listed[0].FileName);
		Assert.Equal(10, WorldBackup.TryParse(fixture.Workspace.BackupsDirectory(fixture.WorldId), "mid-run-20260910-120000-10.cuoz")!.Suffix);
		Assert.Equal(2, listed[0].Suffix);
	}

	private static SaveArchiveWriter Writer() => new(NullLogger<SaveArchiveWriter>.Instance);

	private static SaveArchiveReader Reader() => new(NullLogger<SaveArchiveReader>.Instance);
}
