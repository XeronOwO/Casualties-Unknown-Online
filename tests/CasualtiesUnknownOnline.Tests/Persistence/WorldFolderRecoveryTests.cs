using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 cases 3 and 4: both crash windows of the §5 transaction. A leftover
/// <c>.staging/</c> is discarded (the cut never committed), a leftover
/// <c>.previous/</c> is restored (the rename did not finish) — and both are
/// reported, never applied silently.
/// </summary>
public class WorldFolderRecoveryTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void LeftoverStagingFolder_IsDiscardedAndReported_WhileThePreviousSnapshotStaysLive()
	{
		var workspace = SaveTestWorkspace.Create("crash-staging");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var live = SaveTestData.RunPayload("live");
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, live)).Success);

		var staging = Directory.CreateDirectory(workspace.StagingDirectory(worldId));
		File.WriteAllBytes(Path.Combine(staging.FullName, SaveArchiveFormat.ManifestFileName), Encoding.UTF8.GetBytes("{ \"partial\": true }"));

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.RecoveredLive, load.State);
		Assert.False(Directory.Exists(workspace.StagingDirectory(worldId)), "the uncommitted staging folder is discarded at load");
		Assert.Equal(live.Content, File.ReadAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName)));
		Assert.Contains(load.Report.Entries, entry =>
			entry.Reason == DamageReport.EntryReason.CrashLeftoverStaging
			&& entry.Id == SaveArchiveFormat.StagingFolderName);
	}

	[Fact]
	public void LeftoverPreviousFolderWithoutLive_IsRestoredAndReported()
	{
		var workspace = SaveTestWorkspace.Create("crash-previous");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var restored = SaveTestData.RunPayload("restored");

		// A complete snapshot, taken the way §5 takes it, then left in .previous
		// with no live folder: exactly the window between the two commit renames.
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, restored)).Success);
		Directory.Move(workspace.LiveDirectory(worldId), workspace.PreviousDirectory(worldId));
		Assert.False(Directory.Exists(workspace.LiveDirectory(worldId)), "the crash left no live folder");

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.RecoveredLive, load.State);
		Assert.False(Directory.Exists(workspace.PreviousDirectory(worldId)));
		Assert.Contains(load.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.CrashLeftoverPrevious);
		Assert.Equal(restored.Content, BytesOf(load.Content!, SaveTestData.RunFileName));
	}

	[Fact]
	public void RecoveryIsIdempotent_ASecondPassHasNothingLeftToDo()
	{
		var workspace = SaveTestWorkspace.Create("crash-idempotent");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var previous = Directory.CreateDirectory(workspace.PreviousDirectory(worldId));
		File.WriteAllBytes(Path.Combine(previous.FullName, SaveTestData.RunFileName), SaveTestData.RunPayload("restored").Content);

		var first = WorldFolderRecovery.Recover(directory, NullLogger.Instance);
		var second = WorldFolderRecovery.Recover(directory, NullLogger.Instance);

		Assert.True(first.RecoveredAnything);
		Assert.False(second.RecoveredAnything);
		Assert.True(second.LiveExists);
		Assert.Equal("restored", MarkerOf(File.ReadAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName))));
	}

	[Fact]
	public void CompletedCommitLeftoverPrevious_IsDiscardedAndReported()
	{
		var workspace = SaveTestWorkspace.Create("crash-previous-done");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("live"))).Success);

		var previous = Directory.CreateDirectory(workspace.PreviousDirectory(worldId));
		File.WriteAllBytes(Path.Combine(previous.FullName, SaveTestData.RunFileName), SaveTestData.RunPayload("old").Content);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.True(load.Loaded, load.Summary);
		Assert.Equal(WorldLoadState.RecoveredLive, load.State);
		Assert.False(Directory.Exists(workspace.PreviousDirectory(worldId)));
		Assert.Equal("live", MarkerOf(File.ReadAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName))));
		Assert.Contains(load.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.CrashLeftoverPrevious);
	}

	[Fact]
	public void ForkedCommitState_KeepsLiveAndDropsTheLeftoverPrevious()
	{
		var workspace = SaveTestWorkspace.Create("crash-forked");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var live = Directory.CreateDirectory(workspace.LiveDirectory(worldId));
		File.WriteAllBytes(Path.Combine(live.FullName, SaveTestData.RunFileName), SaveTestData.RunPayload("new").Content);
		var previous = Directory.CreateDirectory(workspace.PreviousDirectory(worldId));
		File.WriteAllBytes(Path.Combine(previous.FullName, SaveTestData.RunFileName), SaveTestData.RunPayload("old").Content);

		var recovery = WorldFolderRecovery.Recover(directory, NullLogger.Instance);

		Assert.True(recovery.RecoveredAnything);
		Assert.Contains(recovery.Actions, action => action.Kind == WorldFolderRecovery.RecoveryKind.DiscardedPrevious);
		Assert.False(Directory.Exists(workspace.PreviousDirectory(worldId)));
		Assert.Equal("new", MarkerOf(File.ReadAllBytes(Path.Combine(live.FullName, SaveTestData.RunFileName))));
	}

	private static string MarkerOf(byte[] bytes) =>
		JsonDocument.Parse(Encoding.UTF8.GetString(bytes)).RootElement.GetProperty("marker").GetString()!;

	private static byte[] BytesOf(WorldSnapshotContent content, string path) =>
		content.Files.Single(file => string.Equals(file.Path, path, StringComparison.Ordinal)).Bytes;
}
