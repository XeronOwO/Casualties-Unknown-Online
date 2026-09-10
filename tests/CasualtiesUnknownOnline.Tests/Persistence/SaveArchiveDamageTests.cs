using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProtocolVersionInfo = CasualtiesUnknownOnline.Runtime.Protocol.ProtocolVersion;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 cases 5, 6 and 9: the manifest is the only hard gate, a damaged domain file
/// is repaired around instead of aborting the load, a protocol mismatch opens in
/// repair mode with a warning, and a newer schema is refused instead of guessed.
/// </summary>
public class SaveArchiveDamageTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void CorruptLiveManifest_FallsBackToTheNewestReadableBackupLoudly()
	{
		var workspace = SaveTestWorkspace.Create("damaged-manifest");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var run = SaveTestData.RunPayload("fresh");
		var items = SaveTestData.ItemsPayload(SaveTestData.ItemId);
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, run, items)).Success);
		File.WriteAllBytes(workspace.ManifestPath(worldId), SaveTestData.Bytes("{ this is not json"));

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.BackupFallback, load.State);
		Assert.NotNull(load.Content!.SourcePath);
		Assert.EndsWith(SaveArchiveFormat.BackupExtension, load.Content.SourcePath!, StringComparison.Ordinal);
		Assert.Contains(load.Report.Entries, entry =>
			entry.Reason == DamageReport.EntryReason.ManifestUnreadable
			&& entry.Scope == DamageReport.EntryScope.Repository);
		Assert.Contains(SaveArchiveFormat.BackupExtension, load.Summary, StringComparison.Ordinal);
		Assert.Equal(run.Content, BytesOf(load.Content, SaveTestData.RunFileName));
		Assert.Equal(items.Content, BytesOf(load.Content, SaveTestData.ItemsFileName));
	}

	[Fact]
	public void CorruptDomainFile_LoadsTheOtherFilesAndNamesTheDamagedPath()
	{
		var workspace = SaveTestWorkspace.Create("damaged-file");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var run = SaveTestData.RunPayload("fresh");
		var items = SaveTestData.ItemsPayload(SaveTestData.ItemId);
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, run, items)).Success);

		// An empty items.json stands in for a file damaged after the manifest was written.
		File.WriteAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.ItemsFileName), []);

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions { VerifyChecksums = true });

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.Current, load.State);
		Assert.Equal(run.Content, BytesOf(load.Content!, SaveTestData.RunFileName));
		Assert.Contains(load.Report.Entries, entry =>
			entry.Scope == DamageReport.EntryScope.File
			&& entry.Reason == DamageReport.EntryReason.ChecksumMismatch
			&& entry.Path == SaveTestData.ItemsFileName);
		Assert.Contains(SaveTestData.ItemsFileName, load.Summary, StringComparison.Ordinal);

		var (_, salvage) = reader.ReadSalvage(load.Content!, (_, session) =>
		{
			Assert.NotEqual(SaveTestData.ItemsFileName, session.CurrentPath);
		}, new WorldLoadOptions());

		Assert.Contains(salvage.Report.Entries, entry =>
			entry.Scope == DamageReport.EntryScope.File
			&& entry.Reason == DamageReport.EntryReason.FileInvalidJson
			&& entry.Path == SaveTestData.ItemsFileName);
	}

	[Fact]
	public void MissingFileListedByTheManifest_IsReportedAndTheRestLoads()
	{
		var workspace = SaveTestWorkspace.Create("missing-file");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var run = SaveTestData.RunPayload("fresh");
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, run, SaveTestData.ItemsPayload(SaveTestData.ItemId))).Success);
		File.Delete(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.ItemsFileName));

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(run.Content, BytesOf(load.Content!, SaveTestData.RunFileName));
		Assert.Contains(load.Report.Entries, entry =>
			entry.Scope == DamageReport.EntryScope.File
			&& entry.Reason == DamageReport.EntryReason.FileUnreadable
			&& entry.Path == SaveTestData.ItemsFileName);
	}

	[Fact]
	public void StrictMode_RefusesADamagedSnapshotInsteadOfRepairingIt()
	{
		var workspace = SaveTestWorkspace.Create("strict-mode");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);
		File.Delete(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName));

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions { RepairMode = false });

		Assert.False(load.Loaded);
		Assert.Equal(WorldLoadState.Failed, load.State);
		Assert.Contains(load.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.DisabledByPolicy);
	}

	[Fact]
	public void ProtocolMismatch_OpensInRepairModeWithAWarning()
	{
		var workspace = SaveTestWorkspace.Create("protocol-mismatch");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var meta = new SaveManifestMeta
		{
			DisplayName = "Future World",
			ProtocolVersion = ProtocolVersionInfo.Current + 1,
		};
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, meta, SaveTestData.RunPayload("future"))).Success);

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.Current, load.State);
		Assert.Equal(meta.ProtocolVersion, load.Content!.Manifest.ProtocolVersion);
		Assert.Contains(load.Report.Entries, entry =>
			entry.Reason == DamageReport.EntryReason.ProtocolMismatch
			&& entry.Detail.Contains("protocol", StringComparison.Ordinal));
		Assert.Contains("Protocol", load.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void NewerManifestSchema_IsRefusedInsteadOfGuessed()
	{
		var workspace = SaveTestWorkspace.Create("newer-schema");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh"))).Success);

		// A newer writer bumped the manifest schema; this build must not guess the
		// payload, and the only backup is broken too, so hard refusal is observable.
		var original = File.ReadAllText(workspace.ManifestPath(worldId));
		var bumped = original.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2");
		Assert.NotEqual(original, bumped);
		File.WriteAllBytes(workspace.ManifestPath(worldId), SaveTestData.Bytes(bumped));
		foreach (var backup in Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz"))
		{
			File.WriteAllBytes(backup, []);
		}

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions { RepairMode = false });

		Assert.False(load.Loaded);
		Assert.Equal(WorldLoadState.Failed, load.State);
		Assert.Contains(load.Report.Entries, entry =>
			entry.Reason == DamageReport.EntryReason.ManifestUnreadable
			&& entry.Detail.Contains("newer", StringComparison.Ordinal));
		Assert.Contains(load.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.NoReadableBackup);
	}

	private static byte[] BytesOf(WorldSnapshotContent content, string path) =>
		content.Files.Single(file => string.Equals(file.Path, path, StringComparison.Ordinal)).Bytes;
}
