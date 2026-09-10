using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 cases 7 and 8: salvage is per entry, not per domain. One unknown entry is
/// skipped and named while its siblings apply, a newer per-entry schema is
/// skipped instead of guessed, and a decoder that throws costs only its entry.
/// </summary>
public class SaveArchiveSalvageTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void UnknownEntry_IsSkippedAndReported_WhileTheRestOfTheDomainApplies()
	{
		var workspace = SaveTestWorkspace.Create("salvage-entry");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		Assert.True(writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId),
			SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut,
				SaveTestData.Payload(SaveTestData.ItemsFileName,
					"[{\"schemaVersion\":1,\"id\":\"" + SaveTestData.ItemId + "\",\"kind\":\"stone\"},{\"schemaVersion\":1,\"id\":\"known.item\",\"kind\":\"stone\"}]\n"))).Success);
		var load = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());
		Assert.True(load.Loaded, load.Summary);

		var applied = new List<string>();
		var missing = new HashSet<string>(StringComparer.Ordinal) { SaveTestData.ItemId };
		var (_, salvage) = reader.ReadSalvage(load.Content!, SaveTestDecoders.Ids(SaveTestData.ItemsFileName, (id, session) =>
		{
			if (missing.Contains(id))
			{
				session.Skip(id, "content-removed", "the content set no longer contains this id");
				return;
			}

			applied.Add(id);
		}), new WorldLoadOptions());

		Assert.Equal("known.item", Assert.Single(applied));
		Assert.False(salvage.IsClean);
		Assert.Equal(1, salvage.SkippedByFile[SaveTestData.ItemsFileName]);
		Assert.Contains(salvage.SkippedEntries, entry =>
			entry.Id == SaveTestData.ItemId
			&& entry.Reason == DamageReport.EntryReason.ContentMissing
			&& entry.Path == SaveTestData.ItemsFileName);
		Assert.Contains(salvage.SkippedEntries, entry => entry.Detail.Contains("content-removed", StringComparison.Ordinal));
	}

	[Fact]
	public void EntrySchemaNewerThanTheReader_IsSkippedAndNamesItsVersion()
	{
		var workspace = SaveTestWorkspace.Create("salvage-entry-schema");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var items = SaveTestData.Payload(SaveTestData.ItemsFileName,
			"[{\"schemaVersion\":9,\"id\":\"future.item\"},{\"schemaVersion\":1,\"id\":\"known.item\"}]\n");
		Assert.True(writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId), SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, items)).Success);

		var load = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());
		var applied = new List<string>();
		var (_, salvage) = reader.ReadSalvage(load.Content!,
			SaveTestDecoders.Ids(SaveTestData.ItemsFileName, (id, _) => applied.Add(id)),
			new WorldLoadOptions { ReaderSchemaVersion = 1 });

		Assert.Equal("known.item", Assert.Single(applied));
		Assert.Contains(salvage.SkippedEntries, entry =>
			entry.Id == "future.item"
			&& entry.Reason == DamageReport.EntryReason.EntrySchemaNewer
			&& entry.Detail.Contains("schemaVersion 9", StringComparison.Ordinal));
	}

	[Fact]
	public void DecoderFailureOnOneEntry_LeavesTheOtherEntriesReportedAndCostsOnlyThatEntry()
	{
		var workspace = SaveTestWorkspace.Create("salvage-throw");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var items = SaveTestData.Payload(SaveTestData.ItemsFileName,
			"[{\"schemaVersion\":1,\"id\":\"boom.item\"},{\"schemaVersion\":1,\"id\":\"known.item\"}]\n");
		Assert.True(writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId), SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, items)).Success);

		var load = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());
		var applied = new List<string>();
		var (_, salvage) = reader.ReadSalvage(load.Content!, SaveTestDecoders.Ids(SaveTestData.ItemsFileName, (id, _) =>
		{
			if (id == "boom.item")
			{
				throw new InvalidOperationException("the decoder cannot materialize this entry");
			}

			applied.Add(id);
		}), new WorldLoadOptions());

		Assert.Equal("known.item", Assert.Single(applied));
		Assert.Contains(salvage.SkippedEntries, entry =>
			entry.Id == "boom.item"
			&& entry.Reason == DamageReport.EntryReason.DecoderRejected
			&& entry.Detail.Contains("InvalidOperationException", StringComparison.Ordinal));
	}

	[Fact]
	public void NonArrayPayload_IsAWholeFileProblem_NotAGuessedEntryList()
	{
		var workspace = SaveTestWorkspace.Create("salvage-not-array");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var items = SaveTestData.Payload(SaveTestData.ItemsFileName, "{\"schemaVersion\":1,\"items\":[]}\n");
		Assert.True(writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId), SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, items)).Success);

		var load = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());
		var (_, salvage) = reader.ReadSalvage(load.Content!, (_, _) => { }, new WorldLoadOptions());

		Assert.Contains(salvage.Report.Entries, entry =>
			entry.Scope == DamageReport.EntryScope.File
			&& entry.Reason == DamageReport.EntryReason.FileDecodeFailed
			&& entry.Path == SaveTestData.ItemsFileName);
	}

	[Fact]
	public void SalvageNeverSeesTheManifestOrOtherDomains()
	{
		var workspace = SaveTestWorkspace.Create("salvage-scope");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		Assert.True(writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId), SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut,
			SaveTestData.RunPayload("fresh"), SaveTestData.ItemsPayload(SaveTestData.ItemId))).Success);
		var load = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());
		var seen = new List<string>();

		reader.ReadSalvage(load.Content!, (_, session) => seen.Add(session.CurrentPath), new WorldLoadOptions());

		Assert.Equal([SaveTestData.ItemsFileName], seen);
		Assert.DoesNotContain(SaveArchiveFormat.ManifestFileName, seen);
	}

	[Fact]
	public void SalvagedSnapshot_OpensTwiceIdentically_AndLeavesTheLiveFilesUntouched()
	{
		var workspace = SaveTestWorkspace.Create("salvage-idempotent");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var payload = new[] { SaveTestData.RunPayload("fresh"), SaveTestData.ItemsPayload(SaveTestData.ItemId) };
		Assert.True(writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId), SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, payload)).Success);
		var before = LiveFileSnapshot(workspace.LiveDirectory(worldId));

		var first = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());
		var second = reader.LoadSnapshot(workspace.WorldDirectory(worldId), new WorldLoadOptions());

		Assert.True(first.Loaded, first.Summary);
		Assert.True(second.Loaded, second.Summary);
		Assert.Equal(before, LiveFileSnapshot(workspace.LiveDirectory(worldId)));
		Assert.Equal(
			first.Content!.Files.Select(file => file.Path),
			second.Content!.Files.Select(file => file.Path));
	}

	private static IReadOnlyList<string> LiveFileSnapshot(string directory) =>
		[.. ArchivePathPolicy.EnumerateFiles(directory)
			.Select(path => path + ":" + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)))))];
}
