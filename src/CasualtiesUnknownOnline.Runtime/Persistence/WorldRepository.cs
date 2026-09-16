using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	/// <summary>
	/// The WORLDS this root holds — the folder listing, their metadata and the
	/// <c>index.json</c> cache. Its own object (see <see cref="WorldCatalog"/>): this class
	/// owns ONE world's files, the catalog owns which worlds exist and what they are called.
	/// </summary>
	private readonly WorldCatalog _catalog = new(root, log, utcNow ?? (() => DateTime.UtcNow));

	/// <summary>The repository root (the <c>cuo/saves</c> folder).</summary>
	public string Root => _root;

	/// <summary>
	/// The <c>lastOpenedWorldId</c> pointer of <c>index.json</c>, or "" when no
	/// world has been opened yet or the index is unreadable. The caller still has
	/// to check that the world exists — the index is a cache, not the truth (§3.1).
	/// </summary>
	public string LastOpenedWorldId => _catalog.LastOpenedWorldId;

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

	/// <summary>
	/// Claims this world folder for THIS process, or reports who else is writing it
	/// (the writer lease, §5 / <see cref="WorldLease"/>). A caller that is about to
	/// RESTORE the world takes the lease before it applies anything: a restore is a
	/// write path (it can promote a backup over the live snapshot), and a restore that
	/// landed in a world another instance is playing would be overwritten by that
	/// instance's next cut without either side ever seeing the other.
	/// </summary>
	public bool TryHoldWorld(string worldId, out string refusal)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			refusal = problem;
			return false;
		}

		var directory = PathOfWorld(worldId);
		if (!Directory.Exists(directory))
		{
			refusal = "the world folder does not exist";
			return false;
		}

		return WorldLease.TryAcquire(directory, _utcNow(), _log, out refusal);
	}

	/// <summary>
	/// Releases the world's writer lease when this process owns it. A clean shutdown
	/// refreshes nothing, so leaving the lease behind would make the next instance wait
	/// out the staleness window for a writer that is provably gone.
	/// </summary>
	public void ReleaseWorld(string worldId)
	{
		if (TryPathOfWorld(worldId) is { } directory)
		{
			WorldLease.Release(directory, _log);
		}
	}

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
	public IReadOnlyList<WorldIndex.WorldEntry> ListWorlds() => _catalog.ListWorlds();

	/// <summary>Creates a world folder with a fresh immutable id and its first <c>world.json</c>; a collision is reported, never overwritten.</summary>
	public WorldCreateResult CreateWorld(string displayName) => _catalog.CreateWorld(displayName);

	/// <summary>Renames a world's display name. The folder key and every existing archive stay untouched.</summary>
	public bool RenameWorld(string worldId, string newDisplayName) => _catalog.RenameWorld(worldId, newDisplayName);

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

	/// <summary>
	/// Opens ONE backup archive of a world (§6). The reader's own fallback opens the newest
	/// one whose manifest reads; this is the same gate by NAME, for the caller that has to
	/// walk past a candidate the DECODE refused (the refusal happens after the manifest was
	/// read, so the reader's fallback has already returned by then).
	/// </summary>
	public WorldLoadResult LoadBackup(string worldId, WorldBackup backup, WorldLoadOptions options)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("Backup cannot be opened: {Problem}.", problem);
			return FailedLoad(worldId, problem);
		}

		// The archive must belong to THIS world's backups folder: a backup record is a
		// path, and a caller-supplied path must never be read from anywhere else.
		if (!string.Equals(Path.GetDirectoryName(backup.FullPath), PathOfBackups(worldId), StringComparison.OrdinalIgnoreCase))
		{
			var mismatch = $"backup {backup.FileName} is not inside world {worldId}'s backups folder";
			_log.LogError("Backup cannot be opened: {Problem}.", mismatch);
			return FailedLoad(worldId, mismatch);
		}

		return _reader.LoadBackup(PathOfWorld(worldId), backup, options);
	}

	/// <summary>
	/// Replaces a world's live snapshot with <paramref name="backup"/> (§6, the restore's
	/// recovery): the refused snapshot is preserved as evidence, the pre-restore copy is
	/// archived, and the backup becomes <c>live/</c>. False = nothing was promoted and the
	/// folder was left as it was found.
	/// </summary>
	internal WorldBackupPromotion.Result PromoteBackup(string worldId, WorldBackup backup)
	{
		var problem = DescribeWorldIdProblem(worldId);
		if (problem.Length > 0)
		{
			_log.LogError("Promotion refused: {Problem}.", problem);
			return WorldBackupPromotion.Result.Refused(problem);
		}

		if (!string.Equals(Path.GetDirectoryName(backup.FullPath), PathOfBackups(worldId), StringComparison.OrdinalIgnoreCase))
		{
			var mismatch = $"backup {backup.FileName} is not inside world {worldId}'s backups folder";
			_log.LogError("Promotion refused: {Problem}.", mismatch);
			return WorldBackupPromotion.Result.Refused(mismatch);
		}

		return WorldBackupPromotion.Promote(PathOfWorld(worldId), backup, _utcNow(), _log);
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
		if (!_catalog.TryEnsureDirectory(directory, out var directoryFailure))
		{
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.StageFailed, directoryFailure);
		}

		// The folder has ONE writer (§5): a second CUO instance pointed at it would
		// interleave the transaction below — both stage into one .staging/, both rename
		// live/ aside — and delete the snapshot the other just committed. The lease is
		// refreshed by every write, so a running host never looks stale.
		if (!WorldLease.TryAcquire(directory, _utcNow(), _log, out var leaseRefusal))
		{
			return SaveWriteResult.Failed(worldId, SaveWriteResult.Failure.LeaseHeld, leaseRefusal);
		}

		var metadata = _catalog.ReadMetadata(directory);
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
			_catalog.WriteMetadata(directory, updated);
			_catalog.RefreshIndex();
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
	public bool SetLastOpenedWorld(string worldId) => _catalog.SetLastOpenedWorld(worldId);

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

	/// <summary>A world id in the format's shape, drawn from a timestamp and a random suffix (<see cref="WorldCatalog.GenerateWorldId"/>).</summary>
	internal static string GenerateWorldId(DateTime utc) => WorldCatalog.GenerateWorldId(utc);

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
	private int CountBackups(string worldId) => ListBackups(worldId).Count;
}
