using System;
using System.IO;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 case 2: the verification step of §5 is real — bytes that no longer match
/// the manifest abort the transaction before the commit, and the previous
/// snapshot stays live. The staging seam stands in for a crash between staging
/// and verification.
/// </summary>
public class SaveArchiveWriterVerificationTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void PayloadChangedAfterStaging_WriteFailsBeforeCommitAndKeepsThePreviousSnapshot()
	{
		var workspace = SaveTestWorkspace.Create("verify-mismatch");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var fresh = SaveTestData.RunPayload("fresh");
		var requested = SaveTestData.RunPayload("requested");

		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, fresh)).Success);

		// The hook rewrites the staged file to bytes that are NOT what the request
		// asked for — the exact corruption §5's verify step must catch before the
		// commit can make it live.
		writer.StagedHook = () =>
		{
			var staged = Path.Combine(workspace.StagingDirectory(worldId), SaveTestData.RunFileName);
			Assert.True(File.Exists(staged), "the hook runs after the snapshot is staged");
			File.WriteAllBytes(staged, SaveTestData.Bytes("{\n  \"schemaVersion\": 1,\n  \"marker\": \"tampered\"\n}\n"));
		};

		var result = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut.AddMinutes(1), requested));
		writer.StagedHook = null;

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.VerifyFailed, result.Reason);
		Assert.Contains(SaveTestData.RunFileName, result.Detail, StringComparison.Ordinal);
		Assert.Equal(fresh.Content, File.ReadAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName)));
		Assert.False(Directory.Exists(workspace.StagingDirectory(worldId)), "an aborted transaction must clean its staging folder");
		Assert.Equal(1, Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz").Length);
	}

	/// <summary>A payload path that appears twice must be refused, not silently overwritten.</summary>
	[Fact]
	public void PayloadPathThatCollides_IsRefusedInsteadOfOverwriting()
	{
		var workspace = SaveTestWorkspace.Create("verify-duplicate");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);

		var result = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut,
			SaveTestData.Payload("run.json", "{\"a\":1}\n"),
			SaveTestData.Payload("run.json", "{\"a\":2}\n")));

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.InvalidRequest, result.Reason);
		Assert.False(Directory.Exists(workspace.LiveDirectory(worldId)));
	}

	[Fact]
	public void StaleStagingLeftover_IsDiscardedBeforeTheNextWrite()
	{
		var workspace = SaveTestWorkspace.Create("stale-staging");
		var worldId = workspace.NewWorldId();
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var stale = Directory.CreateDirectory(Path.Combine(workspace.StagingDirectory(worldId), "characters"));
		File.WriteAllBytes(Path.Combine(stale.FullName, "steam-1.json"), Encoding.UTF8.GetBytes("{\"schemaVersion\":1}"));
		File.WriteAllBytes(Path.Combine(workspace.StagingDirectory(worldId), SaveArchiveFormat.ManifestFileName), Encoding.UTF8.GetBytes("{}"));

		var result = writer.WriteWorldSnapshot(workspace.WorldDirectory(worldId), SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh")));

		// The leftover must not merge into the new snapshot: only the payload of this
		// write is committed.
		Assert.True(result.Success, result.Detail);
		Assert.False(File.Exists(Path.Combine(workspace.LiveDirectory(worldId), SaveArchiveFormat.CharactersFolderName, "steam-1.json")));
		Assert.Empty(Directory.GetDirectories(workspace.LiveDirectory(worldId)));
	}

	[Fact]
	public void BackupsWrittenInTheSameSecond_DoNotOverwriteEachOther()
	{
		var workspace = SaveTestWorkspace.Create("same-second");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);

		var first = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("one")));
		var second = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("two")));

		Assert.True(first.Success, first.Detail);
		Assert.True(second.Success, second.Detail);
		Assert.NotEqual(first.BackupArchivePath, second.BackupArchivePath);
		Assert.Equal(2, Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz").Length);
		Assert.False(File.Exists(first.BackupArchivePath + ".tmp"), "a committed archive must not leave its temporary file");
	}
}
