using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// Defects an independent adversarial review of S1 found and this stage fixed:
/// backup ordering, the manifest gate's structural validation, world-id
/// validation, the on-disk dialect, and reader robustness on hostile archives.
/// Each case failed against the reviewed revision.
/// </summary>
public class SaveArchiveContractFixesTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Manifest_PropertiesUseTheDocumentedCamelCaseNames()
	{
		var workspace = SaveTestWorkspace.Create("fix-manifest-casing");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);

		var text = File.ReadAllText(workspace.ManifestPath(worldId));

		// docs/architecture/save-archive-format.md §3.2 names the fields:
		// schemaVersion, format, worldId, files: [{ path, sha256, bytes }], checksumPolicy.
		Assert.Contains("\"schemaVersion\"", text, StringComparison.Ordinal);
		Assert.Contains("\"files\"", text, StringComparison.Ordinal);
		Assert.Contains("\"sha256\"", text, StringComparison.Ordinal);
		Assert.Contains("\"checksumPolicy\"", text, StringComparison.Ordinal);
		Assert.DoesNotContain("\"SchemaVersion\"", text, StringComparison.Ordinal);
	}

	[Fact]
	public void Manifest_KindUsesTheDocumentedKebabCaseSpelling()
	{
		var workspace = SaveTestWorkspace.Create("fix-manifest-kind");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);

		var text = File.ReadAllText(workspace.ManifestPath(worldId));

		Assert.Contains("\"kind\": \"layer-end\"", text, StringComparison.Ordinal);
	}

	[Fact]
	public void ManifestInTheDocumentedDialect_IsReadNotSilentlyDefaulted()
	{
		var workspace = SaveTestWorkspace.Create("fix-manifest-dialect");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut,
				SaveTestData.RunPayload("fresh"), SaveTestData.ItemsPayload(SaveTestData.ItemId))).Success);

		var path = workspace.ManifestPath(worldId);
		var conforming = File.ReadAllText(path)
			.Replace("\"Kind\"", "\"kind\"")
			.Replace("\"LayerEnd\"", "\"layer-end\"")
			.Replace("\"SchemaVersion\"", "\"schemaVersion\"")
			.Replace("\"Format\"", "\"format\"")
			.Replace("\"Files\"", "\"files\"")
			.Replace("\"Path\"", "\"path\"")
			.Replace("\"Sha256\"", "\"sha256\"")
			.Replace("\"Bytes\"", "\"bytes\"")
			.Replace("\"WorldId\"", "\"worldId\"");
		File.WriteAllText(path, conforming);

		var load = Reader().LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.Current, load.State);
		Assert.Equal(2, load.Content!.Files.Count);
		Assert.Equal(2, load.Content.Manifest.Files.Count);
	}

	[Fact]
	public void ManifestWithoutAnyOfItsRequiredFields_IsNotAcceptedAsAnEmptySnapshot()
	{
		var workspace = SaveTestWorkspace.Create("fix-empty-manifest");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);
		File.WriteAllText(workspace.ManifestPath(worldId), "{}");

		var load = Reader().LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.BackupFallback, load.State);
		Assert.NotEmpty(load.Content!.Files);
	}

	[Fact]
	public void ManifestListingTheSameFileTwice_IsNotAppliedTwice()
	{
		var workspace = SaveTestWorkspace.Create("fix-duplicate-manifest-entry");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut,
				SaveTestData.RunPayload("fresh"), SaveTestData.ItemsPayload(SaveTestData.ItemId))).Success);

		var path = workspace.ManifestPath(worldId);
		var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
		var files = node["files"]!.AsArray();
		files.Add(files[0]!.DeepClone());
		File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

		var load = Reader().LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());

		Assert.Equal(WorldLoadState.BackupFallback, load.State);
		Assert.DoesNotContain(load.Content!.Files, file => SaveArchiveReader.IsDomainFile(file, SaveTestData.ItemsFileName) && load.Content.Files.Count(item => item.Path == file.Path) > 1);
	}

	[Fact]
	public void LiveManifestWithAnUnsafeFilePath_IsReportedAsDamageInsteadOfThrown()
	{
		var workspace = SaveTestWorkspace.Create("fix-manifest-traversal");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);

		var path = workspace.ManifestPath(worldId);
		var text = File.ReadAllText(path);
		var tampered = text.Replace("\"run.json\"", "\"../../outside.json\"");
		Assert.NotEqual(text, tampered);
		File.WriteAllText(path, tampered);
		foreach (var backup in Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz"))
		{
			File.WriteAllBytes(backup, []);
		}

		var load = Reader().LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());

		Assert.False(load.Loaded);
		Assert.Equal(WorldLoadState.Failed, load.State);
		Assert.Contains(load.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.ManifestUnreadable);
		Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(workspace.Root)!, "outside.json")));
	}

	[Fact]
	public void BackupWithADuplicateEntryName_IsReportedNotThrown()
	{
		var workspace = SaveTestWorkspace.Create("fix-duplicate-zip-entry");
		var worldId = workspace.NewWorldId();
		Assert.True(Writer().WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);
		var backup = Directory.GetFiles(workspace.BackupsDirectory(worldId), "*" + SaveArchiveFormat.BackupExtension).Single();
		DuplicateEntry(backup, SaveArchiveFormat.ManifestFileName);
		File.WriteAllBytes(workspace.ManifestPath(worldId), SaveTestData.Bytes("{ broken"));

		var load = Reader().LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());

		Assert.False(load.Loaded);
		Assert.Contains(load.Report.Entries, entry =>
			entry.Reason == DamageReport.EntryReason.NoReadableBackup
			&& entry.Detail.Contains("more than once", StringComparison.Ordinal));
	}

	[Fact]
	public void FailedBackupStep_IsReportedAsBackupFailedAndLeavesNoStagingFolder()
	{
		var workspace = SaveTestWorkspace.Create("fix-backup-failure");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		Directory.CreateDirectory(workspace.LiveDirectory(worldId));
		var previous = SaveTestData.Bytes("{\"marker\":\"previous\"}\n");
		File.WriteAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName), previous);
		File.WriteAllBytes(Path.Combine(directory, SaveArchiveFormat.BackupsFolderName), SaveTestData.Bytes("not a folder"));

		var result = Writer().WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh")));

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.BackupFailed, result.Reason);
		Assert.Equal(previous, File.ReadAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName)));
		Assert.False(Directory.Exists(workspace.StagingDirectory(worldId)), "a failed transaction left its staging folder behind");
	}

	[Fact]
	public void CrashedArchiveRename_LeavesNoAbandonedTemporaryFile()
	{
		var fixture = SaveTestRepository.Create("fix-abandoned-tmp");
		var backups = fixture.Workspace.BackupsDirectory(fixture.WorldId);
		Directory.CreateDirectory(backups);
		var abandoned = Path.Combine(backups, "layer-end-20200101-000000.cuoz.tmp");
		File.WriteAllBytes(abandoned, [1, 2, 3]);

		Assert.True(fixture.Repository.WriteSnapshot(fixture.WorldId,
			fixture.Request(WorldCutKind.LayerEnd, fixture.Now, SaveTestData.RunPayload("fresh"))).Success);

		Assert.False(File.Exists(abandoned), "an abandoned .cuoz.tmp must not survive a write");
	}

	private static SaveArchiveWriter Writer() => new(NullLogger<SaveArchiveWriter>.Instance);

	private static SaveArchiveReader Reader() => new(NullLogger<SaveArchiveReader>.Instance);

	private static void DuplicateEntry(string archivePath, string entryName)
	{
		var temporary = archivePath + ".rewrite";
		using (var source = ZipFile.OpenRead(archivePath))
		using (var target = ZipFile.Open(temporary, ZipArchiveMode.Create))
		{
			foreach (var entry in source.Entries)
			{
				Copy(source, target, entry.FullName);
				if (string.Equals(entry.FullName, entryName, StringComparison.Ordinal))
				{
					Copy(source, target, entryName);
				}
			}
		}

		File.Delete(archivePath);
		File.Move(temporary, archivePath);
	}

	private static void Copy(ZipArchive source, ZipArchive target, string name)
	{
		var entry = target.CreateEntry(name);
		using var targetStream = entry.Open();
		using var sourceStream = source.GetEntry(name)!.Open();
		sourceStream.CopyTo(targetStream);
	}
}
