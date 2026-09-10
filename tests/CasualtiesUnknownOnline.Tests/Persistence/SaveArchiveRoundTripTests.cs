using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 case 1: a write produces a snapshot that reads back byte for byte, with a
/// manifest whose checksums verify and no staging or previous folder left behind.
/// Also case 14: the backup archive holds the same file set, directory entries
/// included, so a restore can unpack exactly what was cut.
/// </summary>
public class SaveArchiveRoundTripTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void WriteThenRead_RoundTripsEveryPayloadAndLeavesNoTransactionFolders()
	{
		var workspace = SaveTestWorkspace.Create("round-trip");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var run = SaveTestData.RunPayload("fresh");
		var items = SaveTestData.ItemsPayload(SaveTestData.ItemId);
		var character = SaveTestData.CharacterPayload("steam-76561198000000001", "host");

		var result = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, run, items, character));

		Assert.True(result.Success, result.Detail);
		Assert.NotNull(result.Manifest);
		Assert.NotNull(result.BackupArchivePath);
		Assert.True(File.Exists(result.BackupArchivePath), "the transaction always archives the committed cut (§5)");
		Assert.False(Directory.Exists(workspace.StagingDirectory(worldId)), "a committed write leaves no .staging folder");
		Assert.False(Directory.Exists(workspace.PreviousDirectory(worldId)), "a committed write leaves no .previous folder");

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions { VerifyChecksums = true });
		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.Current, load.State);
		Assert.False(load.Report.LosesContent, load.Summary);

		var content = load.Content!;
		Assert.Equal(3, content.Files.Count);
		var manifest = content.Manifest;
		Assert.Equal(SaveManifest.FormatMarker, manifest.Format);
		Assert.Equal(SaveManifest.CurrentSchemaVersion, manifest.SchemaVersion);
		Assert.Equal(worldId, manifest.WorldId);
		Assert.Equal(SaveTestData.Meta().DisplayName, manifest.DisplayName);
		Assert.Equal(WorldCutKind.LayerEnd, manifest.Kind);
		Assert.Equal(SaveArchiveFormat.FormatUtc(Cut), manifest.SavedAtUtc);
		Assert.Equal(3, manifest.Files.Count);
		Assert.Equal(manifest.Files.Select(file => file.Path), manifest.Files.Select(file => file.Path).OrderBy(path => path, StringComparer.Ordinal));

		foreach (var file in content.Files)
		{
			Assert.Equal(SaveArchiveChecksum.OfBytes(file.Bytes), file.ManifestEntry.Sha256);
			Assert.Equal(file.Bytes.Length, file.ManifestEntry.Bytes);
		}

		Assert.Equal(run.Content, BytesOf(content, SaveTestData.RunFileName));
		Assert.Equal(items.Content, BytesOf(content, SaveTestData.ItemsFileName));
		Assert.Equal(character.Content, BytesOf(content, SaveArchiveFormat.CharactersFolderName + "/steam-76561198000000001.json"));

		var itemsFile = content.Files.Single(file => SaveArchiveReader.IsDomainFile(file, SaveTestData.ItemsFileName));
		var itemsEntry = JsonDocument.Parse(itemsFile.Bytes).RootElement.EnumerateArray().Single();
		Assert.Equal(SaveTestData.ItemId, itemsEntry.GetProperty("id").GetString());
	}

	[Fact]
	public void BackupArchive_PreservesDirectoryEntriesAndTheSameFileSet()
	{
		var workspace = SaveTestWorkspace.Create("backup-shape");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var payload = new[]
		{
			SaveTestData.RunPayload("fresh"),
			SaveTestData.CharacterPayload("steam-76561198000000002", "guest"),
			SaveTestData.Payload(SaveArchiveFormat.ModStateFolderName + "/example.json", "{\"schemaVersion\":1}\n"),
		};

		var result = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.Auto, Cut, payload));

		Assert.True(result.Success, result.Detail);
		using var archive = ZipFile.OpenRead(result.BackupArchivePath!);
		var fileEntries = archive.Entries.Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal)).Select(entry => entry.FullName).ToList();
		var directoryEntries = archive.Entries.Where(entry => entry.FullName.EndsWith("/", StringComparison.Ordinal)).Select(entry => entry.FullName).ToList();

		Assert.Contains(SaveArchiveFormat.ManifestFileName, fileEntries);
		Assert.Contains(SaveTestData.RunFileName, fileEntries);
		Assert.Contains(SaveArchiveFormat.CharactersFolderName + "/steam-76561198000000002.json", fileEntries);
		Assert.Contains(SaveArchiveFormat.ModStateFolderName + "/example.json", fileEntries);
		Assert.Equal(fileEntries.Count, result.Manifest!.Files.Count + 1); // + manifest.json, which is not listed in itself
		Assert.Contains(SaveArchiveFormat.CharactersFolderName + "/", directoryEntries);
		Assert.Contains(SaveArchiveFormat.ModStateFolderName + "/", directoryEntries);

		var unpacked = Path.Combine(workspace.Root, "unpacked");
		Directory.CreateDirectory(unpacked);
		ZipFile.ExtractToDirectory(result.BackupArchivePath!, unpacked);
		Assert.True(File.Exists(Path.Combine(unpacked, SaveArchiveFormat.ManifestFileName)));
		Assert.True(File.Exists(Path.Combine(unpacked, SaveArchiveFormat.CharactersFolderName, "steam-76561198000000002.json")));
		Assert.True(File.Exists(Path.Combine(unpacked, SaveArchiveFormat.ModStateFolderName, "example.json")));
		Assert.Equal(
			ArchivePathPolicy.EnumerateFiles(workspace.LiveDirectory(worldId)),
			ArchivePathPolicy.EnumerateFiles(unpacked));
	}

	private static byte[] BytesOf(WorldSnapshotContent content, string path) =>
		content.Files.Single(file => string.Equals(file.Path, path, StringComparison.Ordinal)).Bytes;
}
