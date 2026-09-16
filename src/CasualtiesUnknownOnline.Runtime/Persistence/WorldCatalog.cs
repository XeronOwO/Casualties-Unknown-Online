using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The WORLDS a repository root holds: which folders are worlds, what they are called,
/// creating one, renaming one, and the rebuildable <c>index.json</c> cache beside them
/// (§1–§3). Split out of <see cref="WorldRepository"/>, which keeps ONE world's files —
/// the snapshot transaction, the backups and their retention, the writer lease — because
/// the two halves change for different reasons and the repository crossed the architecture
/// line limit once S4.4's lease and backup promotion joined it.
///
/// The index is a CACHE and this type treats it as one: the folder listing is the truth,
/// an index entry whose folder is gone is dropped with a warning, an unreadable index is
/// rebuilt from the folders, and a failed write is logged and forgotten (the next read
/// rebuilds it). The caller supplies the root, so the whole catalog is testable on a temp
/// folder.
/// </summary>
internal sealed class WorldCatalog(string root, ILogger log, Func<DateTime> utcNow)
{
	private readonly string _root = Path.GetFullPath(root);
	private readonly ILogger _log = log;
	private readonly Func<DateTime> _utcNow = utcNow;

	/// <summary>The repository root (the <c>cuo/saves</c> folder).</summary>
	internal string Root => _root;

	/// <summary>
	/// The <c>lastOpenedWorldId</c> pointer of <c>index.json</c>, or "" when no
	/// world has been opened yet or the index is unreadable. The caller still has
	/// to check that the world exists — the index is a cache, not the truth (§3.1).
	/// </summary>
	internal string LastOpenedWorldId => ReadIndex()?.LastOpenedWorldId ?? string.Empty;

	/// <summary>
	/// The world list, driven by disk: worlds found on disk are always listed,
	/// entries whose folder is gone are dropped with a warning, and a missing or
	/// unreadable <c>index.json</c> makes the list fall back to the folders
	/// themselves. The index is rewritten when it disagreed with disk.
	/// </summary>
	internal IReadOnlyList<WorldIndex.WorldEntry> ListWorlds()
	{
		if (!TryEnsureDirectory(_root, out _))
		{
			// The pointer, the picker and the Continue entry all read this list: an
			// unusable root means "there is nothing to open", which is exactly what an
			// empty list says — the log names the cause.
			return [];
		}

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
	internal WorldCreateResult CreateWorld(string displayName)
	{
		if (!TryEnsureDirectory(_root, out var rootFailure))
		{
			// The run still plays; it just has nowhere to save. This is the read-only
			// directory / missing drive case of S4 scope 5, and it must be an answer
			// rather than an exception thrown into the run's start path.
			return WorldCreateResult.Failed(rootFailure);
		}

		var now = _utcNow();
		for (var attempt = 0; attempt < 16; attempt++)
		{
			var worldId = GenerateWorldId(now);
			var directory = PathOf(worldId);
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
	internal bool RenameWorld(string worldId, string newDisplayName)
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

		var directory = PathOf(worldId);
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

	/// <summary>Sets the "last opened" pointer used by the world picker. A world id that is not ours is refused.</summary>
	internal bool SetLastOpenedWorld(string worldId)
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

	/// <summary>
	/// Creates a directory the save layer is about to use, reporting an unusable path
	/// instead of throwing at the caller. Every case S4 scope 5 names — a read-only save
	/// directory, a drive that went away, a file where the root should be — arrives here
	/// first, and each one has to leave the session playable: the run continues, it just
	/// cannot be saved, and the log says which path refused.
	/// </summary>
	internal bool TryEnsureDirectory(string path, out string failure)
	{
		try
		{
			Directory.CreateDirectory(path);
			failure = string.Empty;
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			failure = $"{path}: {ex.Message}";
			_log.LogError(ex, "The save directory {Path} is unusable; nothing can be written there.", path);
			return false;
		}
	}

	/// <summary>The folder a world id names, without the repository's path validation (this type owns the root).</summary>
	internal string PathOf(string worldId) => Path.Combine(_root, worldId);

	/// <summary>The world's metadata as it is on disk, or null when it cannot be read.</summary>
	internal WorldMetadata? ReadMetadata(string worldDirectory)
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

	/// <summary>Rebuilds the index from disk and writes it (a cache refresh, §3.1).</summary>
	internal void RefreshIndex()
	{
		var worlds = ScanWorldFolders();
		TryWriteIndex(new WorldIndex
		{
			SchemaVersion = WorldIndex.CurrentSchemaVersion,
			Worlds = [.. worlds.Select(ToEntry)],
			LastOpenedWorldId = ReadIndex()?.LastOpenedWorldId ?? string.Empty,
		});
	}

	/// <summary>The reason a world id cannot be used, or "" when it can.</summary>
	internal static string DescribeWorldIdProblem(string? worldId) =>
		SaveArchiveFormat.IsWorldId(worldId)
			? string.Empty
			: $"'{worldId}' is not a world id ({SaveArchiveFormat.WorldIdFormat})";

	internal static string GenerateWorldId(DateTime utc) =>
		$"w-{utc.ToUniversalTime():yyyyMMdd}-{RandomHex4()}";

	private string IndexPath => Path.Combine(_root, SaveArchiveFormat.IndexFileName);

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

	/// <summary>Writes a world's <c>world.json</c>. Used by the world's own cut path too, so it is the ONE place that file is written.</summary>
	internal void WriteMetadata(string worldDirectory, WorldMetadata metadata)
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
}
