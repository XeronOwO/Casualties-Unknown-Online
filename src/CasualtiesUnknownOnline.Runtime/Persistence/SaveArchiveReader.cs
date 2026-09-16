using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RuntimeProtocolVersion = CasualtiesUnknownOnline.Runtime.Protocol.ProtocolVersion;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The read side of docs/architecture/save-archive-format.md §6. The manifest is
/// the ONLY hard gate: if it cannot be read or parsed, the snapshot is damaged
/// and the newest backup whose manifest reads is opened instead — loudly. If the
/// manifest reads, the load proceeds in repair mode: a damaged file is skipped
/// with its path named, and a readable file is decoded entry by entry through
/// the caller's <see cref="ISaveSalvageDecoder"/> so one unmaterializable entry
/// never drops its domain.
///
/// A backup fallback is read from the archive, not unpacked: a load writes
/// nothing to disk, so an interrupted restore cannot leave a half-unpacked world
/// behind.
/// </summary>
public sealed class SaveArchiveReader(ILogger<SaveArchiveReader> log)
{
	private const int EntryBufferSize = 81920;

	private readonly ILogger<SaveArchiveReader> _log = log;

	/// <summary>The salvage pass this reader hands an opened snapshot to (§6): its own object, because what to do with the entries is not what OPENING a snapshot is about.</summary>
	private readonly SaveSalvagePass _salvage = new(log);

	/// <summary>
	/// Opens the newest readable snapshot of a world: the live folder when its
	/// manifest reads, otherwise the newest backup whose manifest reads. Crash
	/// leftovers are repaired first (<see cref="WorldFolderRecovery"/>), and every
	/// decision lands in the returned report.
	/// </summary>
	public WorldLoadResult LoadSnapshot(string worldDirectory, WorldLoadOptions options)
	{
		var worldId = Path.GetFileName(worldDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var damage = new List<DamageReport.Entry>();

		var recovery = WorldFolderRecovery.Recover(worldDirectory, _log);
		CollectRecoveryDamage(recovery, damage);

		var liveDirectory = Path.Combine(worldDirectory, SaveArchiveFormat.LiveFolderName);
		if (recovery.LiveExists && Directory.Exists(liveDirectory))
		{
			var live = TryReadLiveSnapshot(worldId, liveDirectory, options, damage, recovery.RecoveredAnything);
			if (live is not null)
			{
				return Finish(live.State, live, damage, options);
			}
		}
		else if (recovery.FailureDetail is not null)
		{
			damage.Add(RepositoryEntry(DamageReport.EntryReason.ManifestUnreadable, SaveArchiveFormat.LiveFolderName, recovery.FailureDetail));
		}

		var fallback = TryLoadNewestReadableBackup(worldDirectory, options, damage);
		if (fallback is not null)
		{
			_log.LogError("World {WorldId} opened from backup {Backup} after a damaged live snapshot.", worldId, Path.GetFileName(fallback.SourcePath!));
			return Finish(WorldLoadState.BackupFallback, fallback, damage, options);
		}

		damage.Add(RepositoryEntry(DamageReport.EntryReason.NoReadableBackup, SaveArchiveFormat.BackupsFolderName,
			"no backup archive of this world could be opened"));
		var failureReport = BuildReport(damage);
		_log.LogError("World {WorldId} could not be opened: {Damage}.", worldId, failureReport.Describe());
		return new WorldLoadResult(WorldLoadState.Failed, worldId, null, failureReport, failureReport.Describe());
	}

	/// <summary>
	/// Reads the live snapshot, or null when it is unusable (a damage entry is
	/// recorded in every failure case). A load never throws out of the archive layer:
	/// a hostile or corrupt folder is a *reported* damaged snapshot, because this code
	/// runs on the host's main thread in S2.
	/// </summary>
	private WorldSnapshotContent? TryReadLiveSnapshot(
		string worldId,
		string liveDirectory,
		WorldLoadOptions options,
		List<DamageReport.Entry> damage,
		bool recoveredFolder)
	{
		try
		{
			var liveFiles = ReadLiveArchiveEntries(liveDirectory);
			if (!TryReadManifest(liveFiles, out var manifest, out var reason))
			{
				damage.Add(RepositoryEntry(DamageReport.EntryReason.ManifestUnreadable,
					$"{SaveArchiveFormat.LiveFolderName}/{SaveArchiveFormat.ManifestFileName}", reason));
				_log.LogError("Live manifest of world {WorldId} is unreadable ({Reason}); falling back to the newest readable backup.", worldId, reason);
				return null;
			}

			NoteProtocolMismatch(manifest, damage);
			var files = ReadManifestFiles(liveDirectory, manifest, options, damage);
			if (files.Count > 0 || manifest.Files.Count == 0)
			{
				// RecoveredLive means the folder needed a repair decision; damage to
				// individual files is carried by the report, not by the state.
				var state = recoveredFolder ? WorldLoadState.RecoveredLive : WorldLoadState.Current;
				return new WorldSnapshotContent(state, manifest, files, liveDirectory);
			}

			damage.Add(FileEntry(DamageReport.EntryReason.FileUnreadable, SaveArchiveFormat.ManifestFileName,
				"every file listed by the live manifest was unreadable"));
			_log.LogError("Live snapshot of world {WorldId} lists {Count} file(s) but none could be read; falling back to backups.",
				worldId, manifest.Files.Count);
			return null;
		}
		catch (Exception ex) when (IsRecoverableReadFailure(ex))
		{
			damage.Add(RepositoryEntry(DamageReport.EntryReason.ManifestUnreadable, SaveArchiveFormat.LiveFolderName,
				$"the live snapshot could not be read: {ex.GetType().Name}: {ex.Message}"));
			_log.LogError(ex, "Live snapshot of world {WorldId} could not be read; falling back to the newest readable backup.", worldId);
			return null;
		}
	}

	/// <summary>The failures a load absorbs: file/archive damage and access problems, never a bug.</summary>
	private static bool IsRecoverableReadFailure(Exception ex) =>
		ex is IOException
		or UnauthorizedAccessException
		or InvalidDataException
		or SaveArchivePathException
		or ArgumentException
		or JsonException;

	/// <summary>
	/// Runs a domain decoder over the snapshot's files, entry by entry, and
	/// accumulates every skip. The decoder is handed ONE entry at a time — never a
	/// whole file — so a single unmaterializable entry is skipped there and then
	/// while the remaining entries of that domain still apply (§6).
	/// </summary>
	public (WorldSnapshotContent Content, SalvageResult Salvage) ReadSalvage(
		WorldSnapshotContent content,
		Action<JsonElement, SalvageSession> decode,
		WorldLoadOptions options) => _salvage.Run(content, decode, options);

	/// <summary>True = this file is the named domain file of a snapshot (for example <c>items.json</c>).</summary>
	public static bool IsDomainFile(SnapshotFile file, string fileName) =>
		string.Equals(file.Path, fileName, StringComparison.Ordinal);

	/// <summary>True = this file is one character file of the snapshot's <c>characters/</c> folder.</summary>
	public static bool IsCharacterFile(SnapshotFile file) =>
		file.Path.StartsWith(SaveArchiveFormat.CharactersFolderName + "/", StringComparison.Ordinal)
		&& file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

	/// <summary>The player key of a character file's name (<c>characters/steam-&lt;id&gt;.json</c> → <c>steam-&lt;id&gt;</c>).</summary>
	public static string CharacterKeyOf(SnapshotFile file)
	{
		var name = file.Path.Substring(SaveArchiveFormat.CharactersFolderName.Length + 1);
		return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - ".json".Length) : name;
	}

	/// <summary>Applies the repair-mode policy (§6) and produces the final result for an opened snapshot.</summary>
	private WorldLoadResult Finish(WorldLoadState state, WorldSnapshotContent content, List<DamageReport.Entry> damage, WorldLoadOptions options)
	{
		if (!options.RepairMode)
		{
			var blocking = damage.FirstOrDefault(entry => entry.Scope == DamageReport.EntryScope.File);
			if (blocking is not null)
			{
				damage.Add(RepositoryEntry(DamageReport.EntryReason.DisabledByPolicy, blocking.Path,
					"repair mode is disabled and a snapshot file is damaged, so the snapshot is refused"));
				var refused = BuildReport(damage);
				_log.LogError("Snapshot of world {WorldId} refused: repair mode is off and {Path} is damaged ({Detail}).",
					content.Manifest.WorldId, blocking.Path, blocking.Detail);
				return new WorldLoadResult(WorldLoadState.Failed, content.Manifest.WorldId, null, refused, refused.Describe());
			}
		}

		var report = BuildReport(damage);
		_log.LogInformation("Opened snapshot for world {WorldId} in state {State} from {Source}: {FileCount} file(s); damage: {Damage}.",
			content.Manifest.WorldId, state, content.SourcePath, content.Files.Count, report.Describe());
		return new WorldLoadResult(state, content.Manifest.WorldId, content, report, report.Describe());
	}

	/// <summary>
	/// §6.1: a protocol that differs from this build opens in repair mode with a
	/// loud warning — never silently, and never a refusal. The world still loads;
	/// entities a newer protocol created may not restore.
	/// </summary>
	private void NoteProtocolMismatch(SaveManifest manifest, List<DamageReport.Entry> damage)
	{
		if (manifest.ProtocolVersion == RuntimeProtocolVersion.Current)
		{
			return;
		}

		damage.Add(RepositoryEntry(DamageReport.EntryReason.ProtocolMismatch, manifest.WorldId,
			$"the snapshot was cut by protocol {manifest.ProtocolVersion}, this build speaks {RuntimeProtocolVersion.Current}"));
		_log.LogWarning("Snapshot of world {WorldId} was cut by protocol {FileProtocol}; this build speaks {BuildProtocol} — repair mode, entries may not restore.",
			manifest.WorldId, manifest.ProtocolVersion, RuntimeProtocolVersion.Current);
	}

	private void CollectRecoveryDamage(WorldFolderRecovery.RecoveryResult recovery, List<DamageReport.Entry> damage)
	{
		foreach (var action in recovery.Actions)
		{
			var reason = action.Kind switch
			{
				WorldFolderRecovery.RecoveryKind.DiscardedStaging => DamageReport.EntryReason.CrashLeftoverStaging,
				WorldFolderRecovery.RecoveryKind.DiscardedAbandonedArchive => DamageReport.EntryReason.CrashLeftoverStaging,
				_ => DamageReport.EntryReason.CrashLeftoverPrevious,
			};
			damage.Add(RepositoryEntry(reason, Path.GetFileName(action.Path), action.Detail));
		}
	}

	/// <summary>
	/// Reads the manifest that is the gate (§3.2, §6.1): the JSON must carry every
	/// required field, the format marker and schema must match this build, and every
	/// listed path must be a safe, unique snapshot path. A manifest that fails any
	/// of these is *damaged* — the caller falls back to a backup rather than
	/// trusting a payload the manifest does not really describe.
	/// </summary>
	private bool TryReadManifest(IReadOnlyList<ArchiveFileEntry> files, out SaveManifest manifest, out string reason)
	{
		manifest = null!;
		var entry = files.FirstOrDefault(file => file.Path == SaveArchiveFormat.ManifestFileName);
		if (entry is null)
		{
			reason = "there is no manifest.json";
			return false;
		}

		try
		{
			if (!SaveArchiveJson.HasProperties(entry.Bytes, SaveManifest.RequiredProperties))
			{
				reason = "the manifest does not carry every required field";
				return false;
			}

			var parsed = SaveArchiveJson.Deserialize<SaveManifest>(entry.Bytes);
			if (parsed is null)
			{
				reason = "the manifest deserialized to null";
				return false;
			}

			if (!string.Equals(parsed.Format, SaveManifest.FormatMarker, StringComparison.Ordinal))
			{
				reason = $"the 'format' marker is '{parsed.Format}', not '{SaveManifest.FormatMarker}'";
				return false;
			}

			if (parsed.SchemaVersion != SaveManifest.CurrentSchemaVersion)
			{
				// Newer: the payload must not be guessed. Older: the reader has to map
				// it explicitly and S1 has one schema, so there is nothing to map yet.
				reason = parsed.SchemaVersion > SaveManifest.CurrentSchemaVersion
					? $"the manifest schemaVersion {parsed.SchemaVersion} is newer than this build's {SaveManifest.CurrentSchemaVersion}"
					: $"the manifest schemaVersion {parsed.SchemaVersion} is older than this build's {SaveManifest.CurrentSchemaVersion}";
				return false;
			}

			reason = DescribeFileListProblem(parsed);
			if (reason.Length > 0)
			{
				return false;
			}

			manifest = parsed;
			reason = string.Empty;
			return true;
		}
		catch (JsonException ex)
		{
			reason = $"the manifest is not valid JSON: {ex.Message}";
			return false;
		}
		catch (SaveArchivePathException ex)
		{
			reason = ex.Message;
			return false;
		}
	}

	/// <summary>The reason the manifest's file list is unusable, or "" when it is sound.</summary>
	private static string DescribeFileListProblem(SaveManifest manifest)
	{
		if (manifest.Files is null)
		{
			return "the manifest's 'files' list is null";
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var file in manifest.Files)
		{
			if (file is null)
			{
				return "the manifest's 'files' list carries a null entry";
			}

			var reason = ArchivePathPolicy.DescribeUnsafePath(file.Path);
			if (reason is not null)
			{
				return $"the manifest lists the unsafe path '{file.Path}': {reason}";
			}

			if (!seen.Add(file.Path))
			{
				return $"the manifest lists '{file.Path}' more than once";
			}
		}

		return string.Empty;
	}

	/// <summary>Lists a live snapshot's files as raw bytes; the manifest gate runs on the result.</summary>
	private static List<ArchiveFileEntry> ReadLiveArchiveEntries(string liveDirectory) =>
		[.. ArchivePathPolicy.EnumerateFiles(liveDirectory)
			.Select(relative => new ArchiveFileEntry(relative, File.ReadAllBytes(ArchivePathPolicy.CombineWithin(liveDirectory, relative))))];

	/// <summary>Reads every file the manifest lists, reporting (and optionally verifying) each one.</summary>
	private List<SnapshotFile> ReadManifestFiles(string liveDirectory, SaveManifest manifest, WorldLoadOptions options, List<DamageReport.Entry> damage)
	{
		var files = new List<SnapshotFile>(manifest.Files.Count);
		foreach (var file in manifest.Files)
		{
			var path = ArchivePathPolicy.CombineWithin(liveDirectory, file.Path);
			if (!File.Exists(path))
			{
				damage.Add(FileEntry(DamageReport.EntryReason.FileUnreadable, file.Path, "the manifest lists it but the file is missing"));
				continue;
			}

			var bytes = File.ReadAllBytes(path);
			if (ShouldVerify(manifest, options))
			{
				VerifyBytes(file, bytes, damage);
			}

			files.Add(new SnapshotFile(file.Path, bytes, file));
		}

		return files;
	}

	/// <summary>
	/// Opens ONE backup archive of a world — the same manifest gate the live snapshot
	/// gets, with the archive as the source. Public because a decode-level refusal
	/// happens AFTER this reader's own fallback ran (the manifest had already been read),
	/// so the recoverer has to open candidates by name itself: see
	/// <c>WorldRestoreRecovery</c> and §6.
	/// </summary>
	public WorldLoadResult LoadBackup(string worldDirectory, WorldBackup backup, WorldLoadOptions options)
	{
		var worldId = Path.GetFileName(worldDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var damage = new List<DamageReport.Entry>();
		var content = TryReadBackup(backup, options, damage);
		if (content is null)
		{
			var failed = BuildReport(damage);
			_log.LogError("Backup {Backup} of world {WorldId} could not be opened: {Damage}.", backup.FileName, worldId, failed.Describe());
			return new WorldLoadResult(WorldLoadState.Failed, worldId, null, failed, failed.Describe());
		}

		var report = BuildReport(damage);
		_log.LogInformation("Opened backup {Backup} of world {WorldId}: {FileCount} file(s); damage: {Damage}.",
			backup.FileName, worldId, content.Files.Count, report.Describe());
		return new WorldLoadResult(WorldLoadState.BackupFallback, content.Manifest.WorldId, content, report, report.Describe());
	}

	/// <summary>Opens the newest backup archive whose manifest reads, or null when none does.</summary>
	private WorldSnapshotContent? TryLoadNewestReadableBackup(string worldDirectory, WorldLoadOptions options, List<DamageReport.Entry> damage)
	{
		var backupsDirectory = Path.Combine(worldDirectory, SaveArchiveFormat.BackupsFolderName);
		if (!Directory.Exists(backupsDirectory))
		{
			return null;
		}

		var backups = WorldBackup.SortedNewestFirst(Directory
			.EnumerateFiles(backupsDirectory, "*" + SaveArchiveFormat.BackupExtension)
			.Select(path => WorldBackup.TryParse(backupsDirectory, Path.GetFileName(path)))
			.OfType<WorldBackup>());

		foreach (var backup in backups)
		{
			var opened = TryReadBackup(backup, options, damage);
			if (opened is null)
			{
				continue;
			}

			damage.Add(RepositoryEntry(DamageReport.EntryReason.ManifestUnreadable, backup.FileName,
				$"the live snapshot could not be read, so backup {backup.FileName} was opened instead"));
			return opened;
		}

		return null;
	}

	/// <summary>
	/// Reads one archive into a snapshot content, or null when it cannot be used (a damage
	/// entry is recorded for every refusal). The archive's manifest is the gate; its listed
	/// files are read out of the same archive, verified when the load asks for it.
	/// </summary>
	private WorldSnapshotContent? TryReadBackup(WorldBackup backup, WorldLoadOptions options, List<DamageReport.Entry> damage)
	{
		try
		{
			var entries = ReadArchiveEntries(backup.FullPath);
			if (!TryReadManifest(entries, out var manifest, out var reason))
			{
				_log.LogError("Backup {Backup} cannot be used: {Reason}", backup.FileName, reason);
				damage.Add(RepositoryEntry(DamageReport.EntryReason.NoReadableBackup, backup.FileName, reason));
				return null;
			}

			NoteProtocolMismatch(manifest, damage);
			var byPath = entries.ToDictionary(entry => entry.Path, entry => entry, StringComparer.Ordinal);
			var files = new List<SnapshotFile>(manifest.Files.Count);
			foreach (var file in manifest.Files)
			{
				if (!byPath.TryGetValue(file.Path, out var archiveEntry))
				{
					damage.Add(FileEntry(DamageReport.EntryReason.FileUnreadable, file.Path, $"backup {backup.FileName} has no such entry"));
					continue;
				}

				if (ShouldVerify(manifest, options))
				{
					VerifyBytes(file, archiveEntry.Bytes, damage);
				}

				files.Add(new SnapshotFile(file.Path, archiveEntry.Bytes, file));
			}

			return new WorldSnapshotContent(WorldLoadState.BackupFallback, manifest, files, backup.FullPath);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or SaveArchivePathException or UnauthorizedAccessException or ArgumentException or JsonException)
		{
			_log.LogError(ex, "Backup {Backup} could not be opened: {Reason}", backup.FileName, ex.Message);
			damage.Add(RepositoryEntry(DamageReport.EntryReason.NoReadableBackup, backup.FileName, ex.Message));
			return null;
		}
	}

	/// <summary>True = this load's manifest requires its digests to be enforced (§3.2).</summary>
	private static bool ShouldVerify(SaveManifest manifest, WorldLoadOptions options) =>
		options.VerifyChecksums && manifest.ChecksumPolicy == ChecksumPolicy.Required;

	private void VerifyBytes(SaveManifest.SaveManifestFile file, byte[] bytes, List<DamageReport.Entry> damage)
	{
		if (bytes.Length != file.Bytes)
		{
			damage.Add(FileEntry(DamageReport.EntryReason.ChecksumMismatch, file.Path,
				$"the file is {bytes.Length} bytes but the manifest says {file.Bytes}"));
			_log.LogError("Checksum gate: {Path} is {Actual} bytes, manifest says {Expected}.", file.Path, bytes.Length, file.Bytes);
			return;
		}

		var digest = SaveArchiveChecksum.OfBytes(bytes);
		if (!string.Equals(digest, file.Sha256, StringComparison.Ordinal))
		{
			damage.Add(FileEntry(DamageReport.EntryReason.ChecksumMismatch, file.Path,
				$"sha256 {digest} does not match the manifest's {file.Sha256}"));
			_log.LogError("Checksum gate: {Path} hashes to {Actual}, manifest says {Expected}.", file.Path, digest, file.Sha256);
		}
	}

	/// <summary>Opens one archive and returns its file entries; directory entries carry no content and are skipped.</summary>
	private static List<ArchiveFileEntry> ReadArchiveEntries(string archivePath)
	{
		using var stream = File.OpenRead(archivePath);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
		var files = new List<ArchiveFileEntry>(archive.Entries.Count);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var entry in archive.Entries)
		{
			if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
			{
				continue;
			}

			// Validated before the bytes are used: a hostile or corrupted entry name
			// must fail here, not at a later join onto a destination path.
			var path = ArchivePathPolicy.ValidateArchiveEntryName(entry.FullName);
			if (!seen.Add(path))
			{
				// Duplicate names are legal ZIP; an archive whose paths are ambiguous
				// cannot say which bytes a file is. That is damage, not a crash.
				throw new InvalidDataException($"the archive carries the entry '{path}' more than once");
			}

			using var entryStream = entry.Open();
			using var buffer = new MemoryStream();
			entryStream.CopyTo(buffer, EntryBufferSize);
			files.Add(new ArchiveFileEntry(path, buffer.ToArray()));
		}

		return files;
	}

	private static DamageReport BuildReport(IReadOnlyList<DamageReport.Entry> damage) =>
		new([.. damage
			.OrderBy(entry => entry.Scope)
			.ThenBy(entry => entry.Path, StringComparer.Ordinal)
			.ThenBy(entry => entry.Id, StringComparer.Ordinal)]);

	private static DamageReport.Entry FileEntry(DamageReport.EntryReason reason, string path, string detail) =>
		new(DamageReport.EntryScope.File, reason, path, string.Empty, detail);

	private static DamageReport.Entry RepositoryEntry(DamageReport.EntryReason reason, string id, string detail) =>
		new(DamageReport.EntryScope.Repository, reason, string.Empty, id, detail);
}
