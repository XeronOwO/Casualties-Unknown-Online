using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 cases 11 and 12: <c>index.json</c> is a cache, never the source of truth —
/// a stale entry is dropped when its folder is gone, a missing index is rebuilt
/// from disk — and a rename moves the display name only, never the folder key.
/// </summary>
public class WorldRepositoryIndexTests
{
	private static readonly JsonSerializerOptions ReadOptions = CreateReadOptions();

	[Fact]
	public void MissingIndex_StillListsWorldsFromDiskAndRebuildsTheCache()
	{
		var fixture = SaveTestRepository.Create("index-missing");
		var indexPath = Path.Combine(fixture.Workspace.Root, SaveArchiveFormat.IndexFileName);
		File.Delete(indexPath);
		Assert.False(File.Exists(indexPath), "the index was removed to simulate a first run");

		var worlds = fixture.Repository.ListWorlds();

		Assert.Single(worlds);
		Assert.Equal(fixture.WorldId, worlds[0].WorldId);
		Assert.Equal("Index World", worlds[0].DisplayName);
		Assert.True(File.Exists(indexPath), "the list rebuilds the cache");
		Assert.Equal(fixture.WorldId, ReadIndex(indexPath).Worlds.Single().WorldId);
	}

	[Fact]
	public void StaleIndexEntry_IsDroppedWhenItsWorldFolderIsGone()
	{
		var fixture = SaveTestRepository.Create("index-stale");
		Assert.True(fixture.Repository.SetLastOpenedWorld(fixture.WorldId));
		Directory.Delete(fixture.WorldDirectory, recursive: true);

		var worlds = fixture.Repository.ListWorlds();

		Assert.Empty(worlds);
		var index = ReadIndex(Path.Combine(fixture.Workspace.Root, SaveArchiveFormat.IndexFileName));
		Assert.Empty(index.Worlds);
		Assert.Equal(fixture.WorldId, index.LastOpenedWorldId);
	}

	[Fact]
	public void WorldFolderMissingFromTheIndex_IsStillListed()
	{
		var fixture = SaveTestRepository.Create("index-additions");
		Assert.True(fixture.Repository.SetLastOpenedWorld(fixture.WorldId));
		var extra = fixture.Workspace.NewWorldId();
		Directory.CreateDirectory(fixture.Workspace.WorldDirectory(extra));

		var worlds = fixture.Repository.ListWorlds();

		Assert.Contains(worlds, entry => entry.WorldId == extra);
		Assert.Contains(ReadIndex(Path.Combine(fixture.Workspace.Root, SaveArchiveFormat.IndexFileName)).Worlds, entry => entry.WorldId == extra);
	}

	[Fact]
	public void UnreadableIndex_IsRebuiltRatherThanTrusted()
	{
		var fixture = SaveTestRepository.Create("index-corrupt");
		var indexPath = Path.Combine(fixture.Workspace.Root, SaveArchiveFormat.IndexFileName);
		File.WriteAllText(indexPath, "{ not json");

		var worlds = fixture.Repository.ListWorlds();

		Assert.Equal(fixture.WorldId, worlds.Single().WorldId);
		Assert.Equal(fixture.WorldId, ReadIndex(indexPath).Worlds.Single().WorldId);
	}

	[Fact]
	public void Rename_KeepsTheDirectoryKeyAndUpdatesMetadataAndIndex()
	{
		var fixture = SaveTestRepository.Create("rename");
		var directory = fixture.WorldDirectory;

		Assert.True(fixture.Repository.RenameWorld(fixture.WorldId, "Renamed World"));

		Assert.True(Directory.Exists(directory), "the folder key is immutable");
		Assert.False(Directory.Exists(Path.Combine(fixture.Workspace.Root, "Renamed World")));
		var metadata = ReadMetadata(directory);
		Assert.Equal("Renamed World", metadata.DisplayName);
		Assert.Equal(fixture.WorldId, metadata.WorldId);
		Assert.Equal("Renamed World", fixture.Repository.ListWorlds().Single().DisplayName);
		Assert.Equal("Renamed World", ReadIndex(Path.Combine(fixture.Workspace.Root, SaveArchiveFormat.IndexFileName)).Worlds.Single().DisplayName);
	}

	[Fact]
	public void Rename_DoesNotTouchExistingBackupArchives()
	{
		var fixture = SaveTestRepository.Create("rename-backups");
		var snapshot = fixture.Repository.WriteSnapshot(fixture.WorldId, fixture.Request(WorldCutKind.LayerEnd, fixture.Now, SaveTestData.RunPayload("fresh")));
		Assert.True(snapshot.Success, snapshot.Detail);
		var before = Directory.GetFiles(fixture.Workspace.BackupsDirectory(fixture.WorldId), "*.cuoz").Select(Path.GetFileName).ToList();

		Assert.True(fixture.Repository.RenameWorld(fixture.WorldId, "Later Name"));

		Assert.Equal(before, Directory.GetFiles(fixture.Workspace.BackupsDirectory(fixture.WorldId), "*.cuoz").Select(Path.GetFileName));
	}

	[Fact]
	public void RenameWithoutMetadata_IsRefused()
	{
		var fixture = SaveTestRepository.Create("rename-refused");
		File.Delete(Path.Combine(fixture.WorldDirectory, SaveArchiveFormat.MetadataFileName));

		Assert.False(fixture.Repository.RenameWorld(fixture.WorldId, "Lost"));
	}

	[Fact]
	public void WriteSnapshot_UpdatesMetadataAndIndexAndCutsAManifestWithTheWorldsName()
	{
		var fixture = SaveTestRepository.Create("write-metadata");

		var result = fixture.Repository.WriteSnapshot(fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now, SaveTestData.RunPayload("fresh")));

		Assert.True(result.Success, result.Detail);
		var metadata = ReadMetadata(fixture.WorldDirectory);
		Assert.Equal("Index World", metadata.DisplayName);
		Assert.Equal(1, metadata.SaveCount);
		Assert.Equal(1, metadata.BackupCount);
		Assert.Equal("mid-run", metadata.LastKind);
		Assert.Equal(SaveArchiveFormat.FormatUtc(fixture.Now), metadata.LastSavedUtc);
		Assert.StartsWith("2026-09-10T", metadata.CreatedAtUtc, StringComparison.Ordinal);
		Assert.Equal("Index World", result.Manifest!.DisplayName);
		var entry = fixture.Repository.ListWorlds().Single();
		Assert.Equal("mid-run", entry.LastKind);
		Assert.Equal(SaveTestData.Meta().LayerIndex, entry.LayerIndex);
	}

	[Fact]
	public void WriteSnapshot_UsesTheWorldsCurrentName_NotTheCallersDeclaredOne()
	{
		var fixture = SaveTestRepository.Create("write-name-source");
		Assert.True(fixture.Repository.RenameWorld(fixture.WorldId, "Renamed"));
		var stale = SaveTestData.Request(fixture.WorldId, WorldCutKind.LayerEnd, fixture.Now, SaveTestData.Meta("Stale Name"), SaveTestData.RunPayload("fresh"));

		var result = fixture.Repository.WriteSnapshot(fixture.WorldId, stale);

		Assert.True(result.Success, result.Detail);
		Assert.Equal("Renamed", result.Manifest!.DisplayName);
		Assert.Equal("Renamed", ReadMetadata(fixture.WorldDirectory).DisplayName);
	}

	[Fact]
	public void CreateWorld_UsesAnImmutableShortIdAndSeedMetadata()
	{
		var fixture = SaveTestRepository.Create("create");

		var created = fixture.Repository.CreateWorld("Second World");

		Assert.True(created.Success, created.Failure);
		Assert.Matches("^w-[0-9]{8}-[0-9a-f]{4}$", created.WorldId);
		Assert.StartsWith("w-20260910-", created.WorldId, StringComparison.Ordinal);
		Assert.True(Directory.Exists(created.WorldDirectory));
		Assert.Equal(0, created.Metadata!.SaveCount);
		Assert.Contains(fixture.Repository.ListWorlds(), entry => entry.WorldId == created.WorldId && entry.DisplayName == "Second World");
		Assert.Equal(2, ReadIndex(Path.Combine(fixture.Workspace.Root, SaveArchiveFormat.IndexFileName)).Worlds.Count);
	}

	[Fact]
	public void WorldIdGeneration_IsStableInShapeAndUniquePerDraw()
	{
		var utc = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

		var ids = Enumerable.Range(0, 64).Select(_ => WorldRepository.GenerateWorldId(utc)).ToList();

		Assert.All(ids, id => Assert.Matches("^w-20260910-[0-9a-f]{4}$", id));
		Assert.True(ids.Distinct(StringComparer.Ordinal).Count() > 60, "64 draws must not collide in practice");
	}

	private static JsonSerializerOptions CreateReadOptions()
	{
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		options.Converters.Add(new JsonStringEnumConverter());
		return options;
	}

	private static WorldIndex ReadIndex(string path) =>
		JsonSerializer.Deserialize<WorldIndex>(File.ReadAllText(path), ReadOptions)!;

	private static WorldMetadata ReadMetadata(string directory) =>
		JsonSerializer.Deserialize<WorldMetadata>(File.ReadAllText(Path.Combine(directory, SaveArchiveFormat.MetadataFileName)), ReadOptions)!;
}
