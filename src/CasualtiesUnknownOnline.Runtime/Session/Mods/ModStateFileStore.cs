using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The host's mod-state disk store. Owns path, format and the atomic
/// read/write mechanics; <see cref="ModService"/> owns the per-mod state table
/// and WHEN to persist. Persistence is disabled when the path is null — the
/// default for tests and any composition root that has not opted in.
///
/// Degradation contract (mirrors the character-data store): a missing file is
/// an empty table; a corrupt or unknown-version file logs a warning and reads
/// as empty (never a startup crash, never a guessed migration); a failed
/// write/delete logs a warning and lets the in-memory table continue. Writes
/// are atomic — serialize to a temp file in the same directory, flush, then
/// File.Replace (or File.Move for the first write) so a crash can never leave
/// a half-written file as the current one.
/// </summary>
public sealed class ModStateFileStore(string? filePath, ILogger<ModStateFileStore> log)
{
	private readonly string? _filePath = filePath;
	private readonly ILogger<ModStateFileStore> _log = log;

	internal bool IsEnabled => !string.IsNullOrEmpty(_filePath);

	/// <summary>
	/// Reads the current file. True = the load settled (including "no file" and
	/// "persistence disabled"); false = a file existed but could not be read as
	/// this schema. The caller treats the returned table as empty; the next
	/// successful save replaces the unreadable file.
	/// </summary>
	internal bool TryLoad(out List<ModStateFile.Entry> entries)
	{
		entries = [];
		if (!IsEnabled || !File.Exists(_filePath))
		{
			return true;
		}

		try
		{
			using var stream = File.OpenRead(_filePath!);
			var file = Serializer.Deserialize<ModStateFile>(stream);
			if (file is null)
			{
				_log.LogWarning("Mod-state file {Path} deserialized to null — treated as empty.", _filePath);
				return false;
			}

			if (file.Version != ModStateFile.CurrentVersion)
			{
				_log.LogWarning("Mod-state file {Path} has version {Version}; this build reads version {Current} — treated as empty.",
					_filePath, file.Version, ModStateFile.CurrentVersion);
				return false;
			}

			foreach (var entry in file.Entries)
			{
				if (entry is null || entry.States is null)
				{
					_log.LogWarning("Mod-state file {Path} contains a null entry — skipped.", _filePath);
					continue;
				}

				entries.Add(entry);
			}

			_log.LogInformation("Loaded {Count} mod state(s) from {Path}.", entries.Count, _filePath);
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Mod-state file {Path} is corrupt or unreadable — treated as empty.", _filePath);
			entries = [];
			return false;
		}
	}

	/// <summary>Atomically replaces the file with the full table. False = the write failed (in-memory state stays authoritative for this process).</summary>
	internal bool Save(IEnumerable<ModStateFile.Entry> entries)
	{
		if (!IsEnabled)
		{
			return true;
		}

		var file = new ModStateFile();
		foreach (var entry in entries)
		{
			file.Entries.Add(entry);
		}

		return WriteAtomically(file);
	}

	private bool WriteAtomically(ModStateFile file)
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
			_log.LogWarning(ex, "Failed to write mod-state file {Path} — the in-memory table keeps working for this process.", path);
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
			_log.LogWarning(ex, "Failed to remove the abandoned mod-state temp file {Path}.", tempPath);
		}
	}
}
