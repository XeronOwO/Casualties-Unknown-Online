using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The world repository of docs/architecture/save-archive-format.md §1–§3: world
/// folders under one root, their player-facing metadata, the rebuildable
/// <c>index.json</c> cache, the backup archives and the transport-scoped player
/// keys. Filesystem and manifest concerns only — what a snapshot *means* belongs
/// to the stages that fill the domains in.
///
/// The caller supplies the root the constructor takes (<c>cuo/saves</c> under the
/// game's persistent-data path in production); this type never asks the game
/// where that is, which is why the whole repository is testable on a temp folder.
/// </summary>
public sealed class WorldRepository(string root, ILogger<WorldRepository> log, SaveArchiveWriter writer, SaveArchiveReader reader, Func<DateTime>? utcNow = null)
{
	private readonly string _root = Path.GetFullPath(root);
	private readonly ILogger<WorldRepository> _log = log;
	private readonly SaveArchiveWriter _writer = writer;
	private readonly SaveArchiveReader _reader = reader;
	private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);

	/// <summary>The repository root (the <c>cuo/saves</c> folder).</summary>
	public string Root => _root;

	/// <summary>
	/// The <c>lastOpenedWorldId</c> pointer of <c>index.json</c>, or "" when no
	/// world has been opened yet or the index is unreadable. The caller still has
	/// to check that the world exists — the index is a cache, not the truth (§3.1).
	/// </summary>
	public string LastOpenedWorldId => ReadIndex()?.LastOpenedWorldId ?? string.Empty;

	/// <summary>
	/// True = the world folder holds a committed snapshot. A world folder created
	/// by a run that never cut anything (an aborted start) has nothing to open, so
	/// a caller offering "continue" must not count it.
	/// </summary>
	public bool HasSnapshot(string worldId) =>
		TryPathOfWorld(worldId) is { } directory
		&& File.Exists(Path.Combine(directory, SaveArchiveFormat.LiveFolderName, SaveArchiveFormat.ManifestFileName));

	/// <summary>
	/// The folder a world id names. The id is the directory key, so it is validated
	/// here: a caller-supplied id must never join onto the root as a path fragment
	/// (a <c>..</c> id would otherwise escape the repository entirely).
	/// </summary>
	public string PathOfWorld(string worldId) =>
		SaveArchiveFormat.IsWorldId(worldId)
			? Path.Combine(_root, worldId)
			: throw new SaveArchivePathException(worldId ?? string.Empty, $"not a world id ({SaveArchiveFormat.WorldIdFormat})");

	/// <summary>The backups folder of a world, or null when the world id is not one of ours.</summary>
	public string? TryPathOfWorld(string worldId) =>
		SaveArchiveFormat.IsWorldId(worldId) ? Path.Combine(_root, worldId) : null;

	public string PathOfBackups(string worldId) => Path.Combine(PathOfWorld(worldId), SaveArchiveFormat.BackupsFolderName);

	/// <summary>The reason a world id cannot be used, or "" when it can.</summary>
	private static string DescribeWorldIdProblem(string? worldId) =>
		SaveArchiveFormat.IsWorldId(worldId)
			? string.Empty
			: $"'{worldId}' is not a world id ({SaveArchiveFormat.WorldIdFormat})";

	/// <summary>
	/// The world list, driven by disk: worlds found on disk are always listed,
	/// entries whose folder is gone are dropped with a warning, and a missing or
	/// unreadable <c>index.json</c> makes the list fall back to the folders
	/// themselves. The index is rewritten when it disagreed with disk.
	/// </summary>
	public IReadOnlyList<WorldIndex.WorldEntry> ListWorlds()
	{
		Directory.CreateDirectory(_root);
		var fromDisk = ScanWorldFolders();
		var index = ReadIndex();

		if (index is null)
		{
			_log.LogWarning("World index {Path} is missing or unreadable; the list was rebuilt from the world folders.", IndexPath);
			RefreshIndex();
		}
		else
		{
			foreach (var entry in index.Worlds.Where(entry => !Contains(fromDisk, entry.WorldId)))
			{
				_log.LogWarning("World index lists {WorldId}, but its folder is gone; the entry is dropped.", entry.WorldId);
			}

			if (fromDisk.Count != index.Worlds.Count || index.Worlds.Any(entry => !Contains(fromDisk, entry.WorldId)))
			{
				RefreshIndex();
			}
		}

		return [.. fromDisk.Select(ToEntry)];
	}

	/// <summary>Creates a world folder with a fresh immutable id and its first <c>world.json</c>; a collision is reported, never overwritten.</summary>
	public WorldCreateResult CreateWorld(string displayName)
	{
		Directory.CreateDirectory(_root);
		var now = _utcNow();
		for (var attempt = 0; attempt < 16; attempt++)
		{
			var worldId = GenerateWorldId(now);
			var directory = PathOfWorld(worldId);
			if (Directory.Exists(directory))
			{
				_log.LogWarning("World id {WorldId} already exists; drawing another one.", worldId);
				continue;
			}

			var metadata = new WorldMetadata
			{
				WorldId = worldId,
				DisplayName = displayName,
				CreatedAtUtc = SaveArchiveFormat.FormatUtc(now),
				LastSavedUtc = SaveArchiveFormat.FormatUtc(now),
				SaveCount = 0,
				BackupCount = 0,
			};

			try
			{
				Directory.CreateDirectory(directory);
				WriteMetadata(directory, metadata);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				_log.LogError(ex, "World {WorldId} could not be created under {Root}.", worldId, _root);
				return WorldCreateResult.Failed(ex.Message);
			}

			_log.LogInformation("Created world {WorldId} ({DisplayName}) at {Directory}.", worldId, displayName, directory);
			RefreshIndex();
			return WorldCreateResult.Created(worldId, directory, metadata);
		}

		_log.LogError("Could not draw a free world id under {Root} after 16 attempts.", _root);
		return WorldCreateResult.Failed("no free world id could be generated");
	}

	/// <summary>Renames a world's display name. The folder key and every existing archive stay untouched.</summary>
	public bool RenameWorld(string worldId, string newDisplayName)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("Rename refused: {Problem}.", problem);
			return false;
		}

		if (string.IsNullOrWhiteSpace(newDisplayName))
		{
			_log.LogError("World {WorldId} cannot be renamed to an empty display name.", worldId);
			return false;
		}

		var directory = PathOfWorld(worldId);
		var metadata = ReadMetadata(directory);
		if (metadata is null)
		{
			_log.LogError("World {WorldId} has no readable {File}; the rename is refused.", worldId, SaveArchiveFormat.MetadataFileName);
			return false;
		}

		var renamed = new WorldMetadata
		{
			WorldId = metadata.WorldId,
			DisplayName = newDisplayName,
			CreatedAtUtc = metadata.CreatedAtUtc,
			LastSavedUtc = metadata.LastSavedUtc,
			LastKind = metadata.LastKind,
			LayerIndex = metadata.LayerIndex,
			PlayerCount = metadata.PlayerCount,
			RunEpoch = metadata.RunEpoch,
			SaveCount = metadata.SaveCount,
			BackupCount = metadata.BackupCount,
		};

		try
		{
			// Existing backup archives keep the display name they were cut with: a
			// manifest freezes a cut's identity, and rewriting archives during a
			// rename would risk world data for a cosmetic field. The live metadata and
			// the index are what the picker and the next cut read.
			WriteMetadata(directory, renamed);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_log.LogError(ex, "World {WorldId} could not be renamed.", worldId);
			return false;
		}

		_log.LogInformation("Renamed world {WorldId} to {DisplayName}; the folder key is unchanged.", worldId, newDisplayName);
		RefreshIndex();
		return true;
	}

	/// <summary>Opens the newest readable snapshot of a world (§6), crash leftovers included.</summary>
	public WorldLoadResult LoadSnapshot(string worldId, WorldLoadOptions options)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("World cannot be opened: {Problem}.", problem);
			return FailedLoad(worldId, problem);
		}

		var directory = PathOfWorld(worldId);
		if (!Directory.Exists(directory))
		{
			_log.LogError("World {WorldId} cannot be opened: its folder {Directory} does not exist.", worldId, directory);
			return FailedLoad(worldId, "the world folder does not exist");
		}

		return _reader.LoadSnapshot(directory, options);
	}

	private static WorldLoadResult FailedLoad(string worldId, string detail)
	{
		var report = new DamageReport([new DamageReport.Entry(
			DamageReport.EntryScope.Repository,
			DamageReport.EntryReason.ManifestUnreadable,
			string.Empty,
			worldId,
			detail)]);
		return new WorldLoadResult(WorldLoadState.Failed, worldId, null, report, report.Describe());
	}

	/// <summary>Runs a domain decode pass over an opened snapshot (§6) and merges the load's own damage report.</summary>
	public (WorldSnapshotContent Content, SalvageResult Salvage) ReadSalvage(
		WorldLoadResult load,
		Action<JsonElement, SalvageSession> decode,
		WorldLoadOptions options)
	{
		if (load.Content is null)
		{
			return (new WorldSnapshotContent(WorldLoadState.Failed, new SaveManifest(), [], null), new SalvageResult(load.Report));
		}

		var (content, salvage) = _reader.ReadSalvage(load.Content, decode, options);
		return (content, salvage with { Report = Merge(load, salvage.Report) });
	}

	/// <summary>Writes one snapshot through the §5 transaction and updates <c>world.json</c> plus <c>index.json</c>.</summary>
	public SaveWriteResult WriteSnapshot(string worldId, SaveWorldRequest request)
	{
		// Both ids are validated before anything touches the disk: a bad id must not
		// create a folder — least of all outside the repository root.
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("Snapshot refused: {Problem}.", problem);
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.InvalidRequest, problem);
		}

		if (!string.Equals(worldId, request.WorldId, StringComparison.Ordinal))
		{
			var mismatch = $"the request names world '{request.WorldId}' but the repository was asked for '{worldId}'";
			_log.LogError("Snapshot refused: {Problem}.", mismatch);
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.InvalidRequest, mismatch);
		}

		var directory = PathOfWorld(worldId);
		Directory.CreateDirectory(directory);
		var metadata = ReadMetadata(directory);
		var displayName = metadata?.DisplayName;
		if (displayName is null)
		{
			displayName = request.Meta.DisplayName;
			_log.LogWarning("World {WorldId} has no readable {File}; the snapshot uses the declared display name '{DisplayName}'.",
				worldId, SaveArchiveFormat.MetadataFileName, displayName);
		}
		else if (!string.Equals(request.Meta.DisplayName, displayName, StringComparison.Ordinal))
		{
			// A cut's display name is the world's current name, not whatever a caller
			// happened to pass: the manifest's cut identity and world.json must agree.
			_log.LogWarning("Snapshot for world {WorldId} declared display name '{Declared}'; using the world's '{Actual}'.",
				worldId, request.Meta.DisplayName, displayName);
		}

		var result = _writer.WriteWorldSnapshot(directory, request with { Meta = request.Meta.WithDisplayName(displayName) });
		if (!result.Success || result.Manifest is null)
		{
			return result;
		}

		var updated = new WorldMetadata
		{
			WorldId = worldId,
			DisplayName = displayName,
			CreatedAtUtc = metadata?.CreatedAtUtc ?? SaveArchiveFormat.FormatUtc(request.SavedAtUtc),
			LastSavedUtc = result.Manifest.SavedAtUtc,
			LastKind = SaveArchiveFormat.CutKindName(result.Manifest.Kind),
			LayerIndex = request.Meta.LayerIndex,
			PlayerCount = request.Meta.PlayerCount,
			RunEpoch = request.Meta.RunEpoch,
			SaveCount = (metadata?.SaveCount ?? 0) + 1,
			BackupCount = CountBackups(worldId),
		};

		try
		{
			WriteMetadata(directory, updated);
			RefreshIndex();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// The snapshot is committed; a failed cache update must not lose it.
			_log.LogError(ex, "Snapshot for world {WorldId} is committed, but {File} or the index could not be updated.",
				worldId, SaveArchiveFormat.MetadataFileName);
		}

		return result;
	}

	/// <summary>Sets the "last opened" pointer used by the world picker. A world id that is not ours is refused.</summary>
	public bool SetLastOpenedWorld(string worldId)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("The last-opened pointer was not updated: {Problem}.", problem);
			return false;
		}

		var index = ReadIndex() ?? new WorldIndex();
		return TryWriteIndex(new WorldIndex
		{
			SchemaVersion = WorldIndex.CurrentSchemaVersion,
			Worlds = [.. index.Worlds],
			LastOpenedWorldId = worldId,
		});
	}

	/// <summary>The world's backup archives, newest first (§7). Archives that do not match the name grammar are ignored.</summary>
	public IReadOnlyList<WorldBackup> ListBackups(string worldId)
	{
		if (!SaveArchiveFormat.IsWorldId(worldId))
		{
			_log.LogError("Backups cannot be listed: {Problem}.", DescribeWorldIdProblem(worldId));
			return [];
		}

		var directory = PathOfBackups(worldId);
		if (!Directory.Exists(directory))
		{
			return [];
		}

		return WorldBackup.SortedNewestFirst(Directory
			.EnumerateFiles(directory, "*" + SaveArchiveFormat.BackupExtension)
			.Select(path => WorldBackup.TryParse(directory, Path.GetFileName(path)))
			.OfType<WorldBackup>());
	}

	/// <summary>
	/// Keeps the newest <paramref name="keep"/> archives, deleting the oldest first.
	/// The newest archive is never deleted, whatever the caller asked for, and a
	/// failed deletion is reported rather than aborting the pass (§7).
	/// </summary>
	public BackupPruneResult PruneBackups(string worldId, int keep)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("Retention refused: {Problem}.", problem);
			return new BackupPruneResult([], [], [problem]);
		}

		var backups = ListBackups(worldId);
		if (keep < 0)
		{
			_log.LogWarning("World {WorldId}: a retention of {Keep} is invalid; nothing was pruned.", worldId, keep);
			return new BackupPruneResult(backups, [], [$"retention {keep} is invalid"]);
		}

		var deletable = backups.Skip(Math.Max(keep, 1)).ToList();
		var deleted = new List<WorldBackup>(deletable.Count);
		var failures = new List<string>();
		foreach (var backup in deletable)
		{
			try
			{
				File.Delete(backup.FullPath);
				deleted.Add(backup);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				failures.Add($"{backup.FileName}: {ex.Message}");
				_log.LogWarning(ex, "Backup {Backup} of world {WorldId} could not be pruned; it stays on disk.", backup.FileName, worldId);
			}
		}

		if (deleted.Count > 0)
		{
			_log.LogInformation("Pruned {Deleted} old backup(s) of world {WorldId}, keeping {Kept} (requested retention {Keep}).",
				deleted.Count, worldId, backups.Count - deleted.Count, keep);
		}

		var kept = backups.Where(backup => !deleted.Contains(backup)).ToList();
		return new BackupPruneResult(kept, deleted, failures);
	}

	private string IndexPath => Path.Combine(_root, SaveArchiveFormat.IndexFileName);

	internal static string GenerateWorldId(DateTime utc) =>
		$"w-{utc.ToUniversalTime():yyyyMMdd}-{RandomHex4()}";

	private static string RandomHex4()
	{
		var bytes = new byte[2];
		using (var random = RandomNumberGenerator.Create())
		{
			random.GetBytes(bytes);
		}

		return $"{bytes[0]:x2}{bytes[1]:x2}";
	}

	private static bool Contains(IReadOnlyList<WorldMetadata> worlds, string worldId) =>
		worlds.Any(metadata => string.Equals(metadata.WorldId, worldId, StringComparison.Ordinal));

	private static WorldIndex.WorldEntry ToEntry(WorldMetadata metadata) => new(
		metadata.WorldId,
		metadata.DisplayName,
		metadata.LastSavedUtc,
		metadata.LastKind,
		metadata.LayerIndex,
		metadata.PlayerCount);

	private static SaveManifestMeta WithDisplayName(SaveManifestMeta meta, string displayName) => new()
	{
		DisplayName = displayName,
		GameBuild = meta.GameBuild,
		CuoBuild = meta.CuoBuild,
		ProtocolVersion = meta.ProtocolVersion,
		ContentFingerprint = meta.ContentFingerprint,
		RunEpoch = meta.RunEpoch,
		GlobalRevision = meta.GlobalRevision,
		LayerIndex = meta.LayerIndex,
		BiomeDepth = meta.BiomeDepth,
		CutPhase = meta.CutPhase,
		SaveReason = meta.SaveReason,
	};

	private static DamageReport Merge(WorldLoadResult load, DamageReport salvage) =>
		salvage.Entries.Count == 0 ? load.Report : new DamageReport([.. load.Report.Entries, .. salvage.Entries]);

	/// <summary>Every world folder under the root, newest first. A folder that is not a world id is not a world.</summary>
	private IReadOnlyList<WorldMetadata> ScanWorldFolders()
	{
		var worlds = new List<WorldMetadata>();
		foreach (var directory in Directory.EnumerateDirectories(_root))
		{
			var folderName = Path.GetFileName(directory);
			if (!SaveArchiveFormat.IsWorldId(folderName))
			{
				_log.LogWarning("Ignoring {Directory}: '{Folder}' is not a world id ({Format}).",
					directory, folderName, SaveArchiveFormat.WorldIdFormat);
				continue;
			}

			worlds.Add(ReadMetadata(directory) ?? new WorldMetadata { WorldId = folderName, DisplayName = folderName });
		}

		return [.. worlds
			.OrderByDescending(metadata => metadata.LastSavedUtc, StringComparer.Ordinal)
			.ThenBy(metadata => metadata.WorldId, StringComparer.Ordinal)];
	}

	private WorldMetadata? ReadMetadata(string worldDirectory)
	{
		var path = Path.Combine(worldDirectory, SaveArchiveFormat.MetadataFileName);
		if (!File.Exists(path))
		{
			return null;
		}

		try
		{
			var metadata = SaveArchiveJson.Deserialize<WorldMetadata>(File.ReadAllBytes(path));
			if (metadata is null || string.IsNullOrEmpty(metadata.WorldId))
			{
				_log.LogWarning("World metadata {Path} is empty or has no world id; the folder is listed by its folder name.", path);
				return null;
			}

			return metadata;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			_log.LogWarning(ex, "World metadata {Path} is unreadable; the folder is listed by its folder name.", path);
			return null;
		}
	}

	private void WriteMetadata(string worldDirectory, WorldMetadata metadata)
	{
		Directory.CreateDirectory(worldDirectory);
		SaveArchiveJson.WriteFile(Path.Combine(worldDirectory, SaveArchiveFormat.MetadataFileName), metadata);
	}

	private WorldIndex? ReadIndex()
	{
		if (!File.Exists(IndexPath))
		{
			return null;
		}

		try
		{
			var index = SaveArchiveJson.Deserialize<WorldIndex>(File.ReadAllBytes(IndexPath));
			if (index is null || index.SchemaVersion != WorldIndex.CurrentSchemaVersion)
			{
				_log.LogWarning("World index {Path} has schema {Schema}; this build writes {Current} — it is rebuilt from disk.",
					IndexPath, index?.SchemaVersion, WorldIndex.CurrentSchemaVersion);
				return null;
			}

			return index;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			_log.LogWarning(ex, "World index {Path} is unreadable; it is rebuilt from disk.", IndexPath);
			return null;
		}
	}

	/// <summary>Rebuilds the index from disk and writes it (a cache refresh, §3.1).</summary>
	private void RefreshIndex()
	{
		var worlds = ScanWorldFolders();
		TryWriteIndex(new WorldIndex
		{
			SchemaVersion = WorldIndex.CurrentSchemaVersion,
			Worlds = [.. worlds.Select(ToEntry)],
			LastOpenedWorldId = ReadIndex()?.LastOpenedWorldId ?? string.Empty,
		});
	}

	private bool TryWriteIndex(WorldIndex index)
	{
		try
		{
			Directory.CreateDirectory(_root);
			SaveArchiveJson.WriteFile(IndexPath, index);
			_log.LogDebug("World index {Path} written with {Count} world(s).", IndexPath, index.Worlds.Count);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_log.LogWarning(ex, "World index {Path} could not be written; it is a cache and will be rebuilt from disk.", IndexPath);
			return false;
		}
	}

	private int CountBackups(string worldId) => ListBackups(worldId).Count;
}
