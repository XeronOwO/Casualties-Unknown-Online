using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The host's character-data disk store. Owns path, format and the atomic
/// read/write/delete mechanics; <see cref="CharacterDataStore"/> owns the
/// domain decision of WHEN to read/write. Persistence is disabled when the
/// path is null — the default for tests and any composition root that has not
/// opted in.
///
/// Degradation contract: a missing file is an empty table; a corrupt or
/// unknown-version file logs a warning and reads as empty (never a startup
/// crash, never a guessed migration); a failed write/delete logs a warning and
/// lets the in-memory session continue. Writes are atomic — serialize to a
/// temp file in the same directory, flush, then File.Replace (or File.Move for
/// the first write) so a crash can never leave a half-written file as the
/// current one.
/// </summary>
public sealed class CharacterDataFileStore(string? filePath, ILogger<CharacterDataFileStore> log)
{
	private readonly string? _filePath = filePath;
	private readonly ILogger<CharacterDataFileStore> _log = log;

	internal bool IsEnabled => !string.IsNullOrEmpty(_filePath);

	/// <summary>
	/// Reads the current file. True = the load settled (including "no file" and
	/// "persistence disabled"); false = a file existed but could not be read as
	/// this schema. The caller treats the returned table as empty; the next
	/// successful save replaces the unreadable file.
	/// </summary>
	internal bool TryLoad(out Dictionary<ulong, CharacterDataMsg> characters)
	{
		characters = [];
		if (!IsEnabled || !File.Exists(_filePath))
		{
			return true;
		}

		try
		{
			using var stream = File.OpenRead(_filePath!);
			var file = Serializer.Deserialize<CharacterDataFile>(stream);
			if (file is null)
			{
				_log.LogWarning("Character-data file {Path} deserialized to null — treated as empty.", _filePath);
				return false;
			}

			if (file.Version != CharacterDataFile.CurrentVersion)
			{
				_log.LogWarning("Character-data file {Path} has version {Version}; this build reads version {Current} — treated as empty.",
					_filePath, file.Version, CharacterDataFile.CurrentVersion);
				return false;
			}

			foreach (var entry in file.Characters)
			{
				if (entry is null || entry.Data is null)
				{
					_log.LogWarning("Character-data file {Path} contains a null entry — skipped.", _filePath);
					continue;
				}

				characters[entry.SteamId] = entry.Data;
			}

			_log.LogInformation("Loaded {Count} saved character(s) from {Path}.", characters.Count, _filePath);
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Character-data file {Path} is corrupt or unreadable — treated as empty.", _filePath);
			characters = [];
			return false;
		}
	}

	/// <summary>Atomically replaces the file with the full table. False = the write failed (in-memory state stays authoritative for this process).</summary>
	internal bool Save(IEnumerable<KeyValuePair<ulong, CharacterDataMsg>> characters)
	{
		if (!IsEnabled)
		{
			return true;
		}

		var file = new CharacterDataFile();
		foreach (var pair in characters)
		{
			file.Characters.Add(new CharacterDataFile.Entry { SteamId = pair.Key, Data = pair.Value });
		}

		return WriteAtomically(file);
	}

	/// <summary>Deletes the file (a new run voids the old run's saves). False = the delete failed.</summary>
	internal bool Delete()
	{
		if (!IsEnabled)
		{
			return true;
		}

		try
		{
			if (File.Exists(_filePath))
			{
				File.Delete(_filePath);
			}

			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to delete character-data file {Path}.", _filePath);
			return false;
		}
	}

	private bool WriteAtomically(CharacterDataFile file)
	{
		var path = _filePath!;
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var tempPath = path + ".tmp";
		try
		{
			using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				Serializer.Serialize(stream, file);
				stream.Flush(true);
			}

			if (File.Exists(path))
			{
				File.Replace(tempPath, path, destinationBackupFileName: null);
			}
			else
			{
				File.Move(tempPath, path);
			}

			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to write character-data file {Path} — the in-memory save keeps working for this process.", path);
			TryDeleteTemp(tempPath);
			return false;
		}
	}

	private void TryDeleteTemp(string tempPath)
	{
		try
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Failed to remove the abandoned character-data temp file {Path}.", tempPath);
		}
	}
}
