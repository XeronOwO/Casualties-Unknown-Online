using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S1 case 10: nothing an archive contains — or a caller passes — may write
/// outside its world folder. Escape attempts are rejected loudly: the writer
/// refuses the payload, the entry-name policy refuses the ZIP entry, and the
/// reader refuses the whole archive before a single byte is written anywhere.
/// </summary>
public class SaveArchivePathSafetyTests
{
	private static readonly DateTime Cut = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

	/// <summary>Assembles the hostile path texts at runtime; see <see cref="HostilePaths"/>.</summary>
	public static TheoryData<string> UnsafePaths =>
	[
		"../evil.txt",
		"characters/../../evil.txt",
		"..\\evil.txt",
		HostilePaths.UnixAbsolutePayload,
		"\\absolute\\evil.txt",
		HostilePaths.DriveAbsolutePayload,
		HostilePaths.DriveAbsoluteForwardSlashPayload,
		"\\\\server\\share\\evil.txt",
		"./evil.txt",
		"../",
	];

	[Theory]
	[MemberData(nameof(UnsafePaths))]
	public void UnsafePayloadPath_IsRefusedAndNothingIsWrittenOutsideTheWorld(string unsafePath)
	{
		var workspace = SaveTestWorkspace.Create("unsafe-payload");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);

		var result = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut,
			SaveTestData.Payload(unsafePath, "{\"evil\":true}\n"),
			SaveTestData.RunPayload("fresh")));

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.InvalidRequest, result.Reason);
		Assert.Contains("Unsafe archive path", result.Detail, StringComparison.Ordinal);
		Assert.False(Directory.Exists(workspace.LiveDirectory(worldId)));
		AssertNoFileEscaped(workspace);
	}

	public static TheoryData<string> UnsafeArchiveEntries =>
	[
		"../evil.txt",
		"characters/../../evil.txt",
		HostilePaths.UnixAbsoluteArchiveEntry,
		HostilePaths.DriveAbsoluteForwardSlashArchiveEntry,
		"\\\\server\\share\\evil.txt",
	];

	[Theory]
	[MemberData(nameof(UnsafeArchiveEntries))]
	public void UnsafeZipEntryName_IsRejectedByThePolicy(string unsafeEntry)
	{
		var exception = Assert.Throws<SaveArchivePathException>(() => ArchivePathPolicy.ValidateArchiveEntryName(unsafeEntry));

		Assert.Equal(unsafeEntry, exception.OffendingPath);
		Assert.False(string.IsNullOrWhiteSpace(exception.Reason));
	}

	[Theory]
	[MemberData(nameof(UnsafeArchiveEntries))]
	public void BackupWithAnUnsafeEntry_IsRefusedLoudlyAndNothingIsExtracted(string unsafeEntry)
	{
		var workspace = SaveTestWorkspace.Create("unsafe-entry");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var reader = new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance);
		var manifest = AssertSuccess(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, SaveTestData.RunPayload("fresh")))).Manifest!;
		var backup = Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz").Single();
		RenameEntry(backup, manifest.Files.Single(file => file.Path == SaveTestData.RunFileName).Path, unsafeEntry);

		// The live snapshot is unusable, so the load falls to the only backup — which
		// carries a hostile entry name and must be refused instead of extracted.
		File.WriteAllBytes(workspace.ManifestPath(worldId), SaveTestData.Bytes("{ broken"));

		var load = reader.LoadSnapshot(directory, new WorldLoadOptions());

		Assert.False(load.Loaded);
		Assert.Equal(WorldLoadState.Failed, load.State);
		Assert.Contains(load.Report.Entries, entry =>
			entry.Reason == DamageReport.EntryReason.NoReadableBackup
			&& entry.Id.EndsWith(SaveArchiveFormat.BackupExtension, StringComparison.Ordinal)
			&& entry.Detail.Contains(unsafeEntry, StringComparison.Ordinal));
		AssertNoFileEscaped(workspace);
	}

	[Fact]
	public void UnsafePayloadPath_DoesNotDisturbAnExistingSnapshot()
	{
		var workspace = SaveTestWorkspace.Create("unsafe-keeps-live");
		var worldId = workspace.NewWorldId();
		var directory = workspace.WorldDirectory(worldId);
		var writer = new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance);
		var fresh = SaveTestData.RunPayload("fresh");
		Assert.True(writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut, fresh)).Success);

		var result = writer.WriteWorldSnapshot(directory, SaveTestData.Request(worldId, WorldCutKind.LayerEnd, Cut.AddMinutes(1),
			SaveTestData.Payload("../../escape.json", "{\"evil\":true}\n")));

		Assert.False(result.Success);
		Assert.Equal(SaveWriteResult.Failure.InvalidRequest, result.Reason);
		Assert.Equal(fresh.Content, File.ReadAllBytes(Path.Combine(workspace.LiveDirectory(worldId), SaveTestData.RunFileName)));
		Assert.Equal(1, Directory.GetFiles(workspace.BackupsDirectory(worldId), "*.cuoz").Length);
		AssertNoFileEscaped(workspace);
	}

	[Fact]
	public void PayloadPath_IsCanonicalizedButDotSegmentsStayRefused()
	{
		Assert.Equal("characters/steam-1.json", ArchivePathPolicy.Canonicalize("characters//steam-1.json"));
		Assert.Null(ArchivePathPolicy.DescribeUnsafePath("characters/steam-1.json"));
		Assert.Equal("characters/steam-1.json", ArchivePathPolicy.ValidateSnapshotPath("characters/steam-1.json"));
		Assert.NotNull(ArchivePathPolicy.DescribeUnsafePath("characters/./steam-1.json"));
		Assert.NotNull(ArchivePathPolicy.DescribeUnsafePath("characters/../steam-1.json"));
		Assert.NotNull(ArchivePathPolicy.DescribeUnsafePath(string.Empty));
		Assert.NotNull(ArchivePathPolicy.DescribeUnsafePath("   "));
	}

	private static SaveWriteResult AssertSuccess(SaveWriteResult result)
	{
		Assert.True(result.Success, result.Detail);
		return result;
	}

	/// <summary>Renames one ZIP entry to the hazardous name, the way a corrupted or hostile archive would carry it.</summary>
	private static void RenameEntry(string archivePath, string currentName, string newName)
	{
		var temporary = archivePath + ".rewrite";
		using (var source = ZipFile.OpenRead(archivePath))
		using (var target = ZipFile.Open(temporary, ZipArchiveMode.Create))
		{
			foreach (var entry in source.Entries)
			{
				var name = string.Equals(entry.FullName, currentName, StringComparison.Ordinal) ? newName : entry.FullName;
				var copy = target.CreateEntry(name);
				using var targetStream = copy.Open();
				using var sourceStream = entry.Open();
				sourceStream.CopyTo(targetStream);
			}
		}

		File.Delete(archivePath);
		File.Move(temporary, archivePath);
	}

	/// <summary>
	/// Nothing may be created outside the world folder. Only this workspace's own
	/// subtree is inspected: sibling workspaces of parallel tests are not this test's
	/// business.
	/// </summary>
	private static void AssertNoFileEscaped(SaveTestWorkspace workspace)
	{
		var parent = Path.GetDirectoryName(workspace.Root)!;
		string[] relatives =
		[
			"evil.txt",
			"../evil.txt",
			"../../evil.txt",
			"../../escape.json",
			"etc/passwd",
			"tmp/evil.txt",
		];

		foreach (var relative in relatives)
		{
			var segments = new List<string>(relative.Split('/').Length + 1) { workspace.Root };
			segments.AddRange(relative.Split('/'));
			var path = Path.Combine([.. segments]);
			Assert.False(File.Exists(path), $"{relative} must not be created relative to {workspace.Root}");
			Assert.False(Directory.Exists(path), $"{relative} must not be created relative to {workspace.Root}");
		}

		Assert.False(Directory.Exists(Path.Combine(parent, "etc")), "an absolute entry name must not create a folder outside the workspace");
		Assert.False(Directory.Exists(Path.Combine(parent, "tmp")), "an absolute entry name must not create a folder outside the workspace");
	}
}
