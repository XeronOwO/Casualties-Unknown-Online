using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The write transaction of docs/architecture/save-archive-format.md §5. One
/// call turns payload bytes into a committed snapshot plus its backup:
/// <code>
/// stage   → &lt;world&gt;/.staging/ (payload first, manifest last)
/// verify  → re-read every staged file, compare it with the manifest
/// backup  → ZIP .staging/ to &lt;world&gt;/backups/&lt;kind&gt;-&lt;stamp&gt;.cuoz.tmp, then rename
/// commit  → live → .previous, .staging → live, then delete .previous
/// </code>
/// A failure at any step leaves the previous snapshot untouched and reports
/// which step refused; <c>live/</c> is only ever replaced by a complete,
/// verified snapshot. The staged bytes are also read back out of the finished
/// archive before the commit, so a truncated archive is caught while the old
/// snapshot is still live.
/// </summary>
public sealed class SaveArchiveWriter(ILogger<SaveArchiveWriter> log)
{
	private const int EntryBufferSize = 81920;

	private readonly ILogger<SaveArchiveWriter> _log = log;

	/// <summary>
	/// Verification seam for tests: runs after the snapshot is staged and before
	/// §5's verify step. Production never sets it; a crash window is simulated by
	/// a leftover folder rather than by a hook (see <see cref="WorldFolderRecovery"/>).
	/// </summary>
	internal Action? StagedHook { get; set; }

	/// <summary>Runs the whole transaction for one cut.</summary>
	public SaveWriteResult WriteWorldSnapshot(string worldDirectory, SaveWorldRequest request)
	{
		var worldId = request.WorldId;
		try
		{
			return WriteSnapshotTransaction(worldDirectory, request);
		}
		catch (SaveArchivePathException ex)
		{
			_log.LogError("Save write refused for {WorldId}: {Reason} ({OffendingPath}).", worldId, ex.Reason, ex.OffendingPath);
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.InvalidRequest, ex.Message);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			_log.LogError(ex, "Save write failed for {WorldId} at {Directory}; the previous snapshot is intact.", worldId, worldDirectory);
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.StageFailed, ex.Message);
		}
	}

	private SaveWriteResult WriteSnapshotTransaction(string worldDirectory, SaveWorldRequest request)
	{
		var worldId = request.WorldId;
		if (!SaveArchiveFormat.IsWorldId(worldId))
		{
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.InvalidRequest,
				$"'{worldId}' is not a world id ({SaveArchiveFormat.WorldIdFormat}).");
		}

		// Validate every caller-supplied path BEFORE touching the filesystem: an
		// unsafe payload must not create a staging folder or delete a leftover.
		var payload = request.Payload.Select(file => new SaveArchiveEntry(ArchivePathPolicy.ValidateSnapshotPath(file.Path), file.Content)).ToList();
		RejectDuplicatePaths(payload);

		Directory.CreateDirectory(worldDirectory);
		var staging = Path.Combine(worldDirectory, SaveArchiveFormat.StagingFolderName);
		ResetStaging(staging);
		var manifest = Stage(worldDirectory, request, payload, staging);

		StagedHook?.Invoke();

		var verifyFailure = VerifyStagedBytes(staging, manifest);
		if (verifyFailure is not null)
		{
			_log.LogError("Save verification failed for {WorldId}: {Failure}. Transaction aborted; the previous snapshot is still live.", worldId, verifyFailure);
			DeleteIfExists(staging, "staging folder");
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.VerifyFailed, verifyFailure);
		}

		WorldBackup archive;
		try
		{
			archive = WriteBackupArchive(worldDirectory, request, staging, manifest);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			// §5: no backup means no commit — and the staging folder goes with it, so a
			// later reader never finds a half-transaction to puzzle over.
			DeleteIfExists(staging, "staging folder after a failed backup");
			_log.LogError("Save backup step failed for {WorldId}: {Failure}. Transaction aborted; the previous snapshot is still live.",
				worldId, ex.Message);
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.BackupFailed, ex.Message);
		}

		var commitFailure = Commit(worldDirectory, staging);
		if (commitFailure is not null)
		{
			_log.LogError("Save commit failed for {WorldId}: {Failure}", worldId, commitFailure);
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.CommitFailed, commitFailure);
		}

		_log.LogInformation("Committed {Kind} snapshot for world {WorldId}: {FileCount} file(s), archive {Archive}.",
			SaveArchiveFormat.CutKindName(request.Kind), worldId, manifest.Files.Count, archive.FileName);
		return SaveWriteResult.Ok(worldId, manifest, Path.Combine(worldDirectory, SaveArchiveFormat.LiveFolderName), archive.FullPath);
	}

	private static void RejectDuplicatePaths(IReadOnlyList<SaveArchiveEntry> payload)
	{
		var duplicate = payload
			.GroupBy(entry => entry.Path, StringComparer.Ordinal)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate is not null)
		{
			throw new SaveArchivePathException(duplicate.Key, "the same snapshot path appears more than once in the payload");
		}
	}

	/// <summary>Stages the payload and writes the manifest last (§5), returning the manifest that was written.</summary>
	private SaveManifest Stage(string worldDirectory, SaveWorldRequest request, IReadOnlyList<SaveArchiveEntry> payload, string staging)
	{
		Directory.CreateDirectory(staging);
		var files = new List<SaveManifest.SaveManifestFile>(payload.Count);
		foreach (var entry in payload)
		{
			files.Add(WritePayload(staging, entry));
		}

		var manifest = new SaveManifest
		{
			SchemaVersion = SaveManifest.CurrentSchemaVersion,
			Format = SaveManifest.FormatMarker,
			GameBuild = request.Meta.GameBuild,
			CuoBuild = request.Meta.CuoBuild,
			ProtocolVersion = request.Meta.ProtocolVersion,
			ContentFingerprint = request.Meta.ContentFingerprint,
			WorldId = request.WorldId,
			DisplayName = request.Meta.DisplayName,
			Kind = request.Kind,
			RunEpoch = request.Meta.RunEpoch,
			GlobalRevision = request.Meta.GlobalRevision,
			LayerIndex = request.Meta.LayerIndex,
			BiomeDepth = request.Meta.BiomeDepth,
			CutPhase = request.Meta.CutPhase,
			SaveReason = request.Meta.SaveReason,
			SavedAtUtc = SaveArchiveFormat.FormatUtc(request.SavedAtUtc),
			Files = [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)],
		};

		SaveArchiveJson.WriteFile(Path.Combine(staging, SaveArchiveFormat.ManifestFileName), manifest);
		return manifest;
	}

	private SaveManifest.SaveManifestFile WritePayload(string stagingRoot, SaveArchiveEntry entry)
	{
		var target = ArchivePathPolicy.CombineWithin(stagingRoot, entry.Path);
		var directory = Path.GetDirectoryName(target);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// FileMode.CreateNew: a name that is already staged is a collision, not an overwrite.
		using (var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
		{
			stream.Write(entry.Content!, 0, entry.Content!.Length);
			stream.Flush(flushToDisk: true);
		}

		return new SaveManifest.SaveManifestFile(entry.Path, SaveArchiveChecksum.OfBytes(entry.Content!), entry.Content!.Length);
	}

	/// <summary>§5's verify step: re-read every staged file from disk and compare it with the manifest.</summary>
	private static string? VerifyStagedBytes(string stagingRoot, SaveManifest manifest)
	{
		foreach (var file in manifest.Files)
		{
			var path = ArchivePathPolicy.CombineWithin(stagingRoot, file.Path);
			if (!File.Exists(path))
			{
				return $"staged file '{file.Path}' is missing";
			}

			var length = new FileInfo(path).Length;
			if (length != file.Bytes)
			{
				return $"staged file '{file.Path}' is {length} bytes but the manifest says {file.Bytes}";
			}

			var digest = SaveArchiveChecksum.OfFile(path);
			if (!string.Equals(digest, file.Sha256, StringComparison.Ordinal))
			{
				return $"staged file '{file.Path}' hashes to {digest} but the manifest says {file.Sha256}";
			}
		}

		return null;
	}

	/// <summary>ZIPs the staging folder to a temporary archive, proves it re-reads, then renames it into place.</summary>
	private WorldBackup WriteBackupArchive(string worldDirectory, SaveWorldRequest request, string staging, SaveManifest manifest)
	{
		var backupsDirectory = Path.Combine(worldDirectory, SaveArchiveFormat.BackupsFolderName);
		Directory.CreateDirectory(backupsDirectory);
		var backup = WorldBackup.Create(backupsDirectory, request.Kind, request.SavedAtUtc);
		var temporary = backup.FullPath + ".tmp";

		try
		{
			// A write is also the moment to clear an archive abandoned by a crash
			// between writing a ZIP and renaming it (WorldFolderRecovery sweeps on the
			// load path; this keeps the folder clean without waiting for a load).
			foreach (var swept in WorldFolderRecovery.SweepAbandonedArchives(worldDirectory, _log))
			{
				_log.LogWarning("Discarded the abandoned backup archive {Archive} found before this write.", swept);
			}

			DeleteIfExists(temporary, "abandoned backup archive");
			WriteArchive(staging, temporary, manifest);
			var damage = VerifyArchive(temporary, manifest);
			if (damage is not null)
			{
				throw new InvalidDataException($"the backup archive does not round-trip: {damage}");
			}

			if (File.Exists(backup.FullPath))
			{
				File.Replace(temporary, backup.FullPath, destinationBackupFileName: null);
			}
			else
			{
				File.Move(temporary, backup.FullPath);
			}

			return backup;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			_log.LogError(ex, "Backup archive {Archive} could not be produced; the commit is aborted and the previous snapshot stays live.", backup.FileName);
			DeleteIfExists(temporary, "abandoned backup archive");
			throw;
		}
	}

	/// <summary>
	/// Writes the snapshot's file set (the directories its payload paths imply,
	/// preserved so an unpacked backup has the snapshot's shape) as a deflate ZIP.
	/// The stream is opened with <see cref="FileOptions.WriteThrough"/> so the
	/// archive is on disk before the commit rename can happen.
	/// </summary>
	private static void WriteArchive(string snapshotRoot, string archivePath, SaveManifest manifest)
	{
		using var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, EntryBufferSize, FileOptions.WriteThrough);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

		var directories = new SortedSet<string>(StringComparer.Ordinal);
		foreach (var file in manifest.Files)
		{
			var separator = file.Path.LastIndexOf('/');
			if (separator > 0)
			{
				directories.Add(file.Path.Substring(0, separator));
			}
		}

		foreach (var directory in directories)
		{
			archive.CreateEntry(directory + "/", CompressionLevel.NoCompression);
		}

		foreach (var file in manifest.Files)
		{
			var sourcePath = ArchivePathPolicy.CombineWithin(snapshotRoot, file.Path);
			var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
			entry.LastWriteTime = File.GetLastWriteTime(sourcePath);
			using var entryStream = entry.Open();
			using var source = File.OpenRead(sourcePath);
			source.CopyTo(entryStream, EntryBufferSize);
		}

		// The manifest goes in last (§5) and is the archive's gate: without it the
		// backup would be unusable exactly when it is needed.
		var manifestPath = ArchivePathPolicy.CombineWithin(snapshotRoot, SaveArchiveFormat.ManifestFileName);
		var manifestEntry = archive.CreateEntry(SaveArchiveFormat.ManifestFileName, CompressionLevel.Optimal);
		manifestEntry.LastWriteTime = File.GetLastWriteTime(manifestPath);
		using (var manifestStream = manifestEntry.Open())
		using (var manifestSource = File.OpenRead(manifestPath))
		{
			manifestSource.CopyTo(manifestStream, EntryBufferSize);
		}
	}

	/// <summary>Reads every manifest file back out of the finished archive and compares bytes; null = intact.</summary>
	private static string? VerifyArchive(string archivePath, SaveManifest manifest)
	{
		using var stream = File.OpenRead(archivePath);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
		foreach (var file in manifest.Files)
		{
			var entry = archive.GetEntry(file.Path);
			if (entry is null)
			{
				return $"the archive has no entry '{file.Path}'";
			}

			using var entryStream = entry.Open();
			using var buffer = new MemoryStream();
			entryStream.CopyTo(buffer, EntryBufferSize);
			var content = buffer.ToArray();
			if (content.Length != file.Bytes)
			{
				return $"archive entry '{file.Path}' is {content.Length} bytes but the manifest says {file.Bytes}";
			}

			if (!string.Equals(SaveArchiveChecksum.OfBytes(content), file.Sha256, StringComparison.Ordinal))
			{
				return $"archive entry '{file.Path}' does not match the manifest checksum";
			}
		}

		return null;
	}

	/// <summary>The commit step of §5, including the undo when the second rename fails.</summary>
	private string? Commit(string worldDirectory, string staging)
	{
		var live = Path.Combine(worldDirectory, SaveArchiveFormat.LiveFolderName);
		var previous = Path.Combine(worldDirectory, SaveArchiveFormat.PreviousFolderName);
		var movedLive = false;
		try
		{
			DeleteIfExists(previous, "leftover previous snapshot");
			if (Directory.Exists(live))
			{
				Directory.Move(live, previous);
				movedLive = true;
			}

			Directory.Move(staging, live);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			var failure = $"the directory swap failed: {ex.Message}";
			if (movedLive && !Directory.Exists(live))
			{
				try
				{
					Directory.Move(previous, live);
					failure += " The previous snapshot was restored to live.";
				}
				catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
				{
					failure += $" The previous snapshot could NOT be restored ({restoreException.Message}); it is in {SaveArchiveFormat.PreviousFolderName} and the next load recovers it.";
				}
			}

			DeleteIfExists(staging, "staging folder");
			_log.LogError("Save commit failed for {WorldId}: {Failure}", Path.GetFileName(worldDirectory), failure);
			return failure;
		}

		DeleteIfExists(previous, "previous snapshot after a completed commit");
		return null;
	}

	/// <summary>Removes any leftover, and any unreadable leftover, so the new snapshot starts from a clean staging folder.</summary>
	private void ResetStaging(string staging)
	{
		if (!Directory.Exists(staging))
		{
			return;
		}

		try
		{
			Directory.Delete(staging, recursive: true);
			_log.LogWarning("Discarded the leftover staging folder {Staging} found before this write.", staging);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			throw new IOException($"the leftover staging folder {staging} could not be removed", ex);
		}
	}

	private void DeleteIfExists(string path, string what)
	{
		if (!File.Exists(path) && !Directory.Exists(path))
		{
			return;
		}

		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
			else
			{
				File.Delete(path);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_log.LogWarning(ex, "Could not remove the {What} at {Path}; it is harmless but stays on disk.", what, path);
		}
	}
}
