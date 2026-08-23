using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The host's persistent ban-list disk store. Owns path, format and the atomic
/// read/write mechanics; <see cref="HostBanService"/> owns the domain decision
/// of when to add/remove a SteamID. Persistence is disabled when the path is
/// null — the default for tests and composition roots that have not opted in.
///
/// Degradation contract: a missing file is an empty list; a corrupt or
/// unknown-version file logs a warning and reads as empty (never a startup
/// crash, never a guessed migration); a failed write logs a warning and lets
/// the in-memory list continue for this process. Writes are atomic — serialize
/// to a temp file in the same directory, flush, then File.Replace (or
/// File.Move for the first write) so a crash can never leave a half-written
/// file as the current one.
/// </summary>
public sealed class HostBanFileStore(string? filePath, ILogger<HostBanFileStore> log)
{
	private readonly string? _filePath = filePath;
	private readonly ILogger<HostBanFileStore> _log = log;

	internal bool IsEnabled => !string.IsNullOrEmpty(_filePath);

	/// <summary>
	/// Reads the current file. True = the load settled (including "no file" and
	/// "persistence disabled"); false = a file existed but could not be read as
	/// this schema. The caller treats the returned list as empty; the next
	/// successful save replaces the unreadable file.
	/// </summary>
	internal bool TryLoad(out HashSet<ulong> banned)
	{
		banned = [];
		if (!IsEnabled || !File.Exists(_filePath))
		{
			return true;
		}

		try
		{
			using var stream = File.OpenRead(_filePath!);
			var file = Serializer.Deserialize<HostBanFile>(stream);
			if (file is null)
			{
				_log.LogWarning("Host ban file {Path} deserialized to null — treated as empty.", _filePath);
				return false;
			}

			if (file.Version != HostBanFile.CurrentVersion)
			{
				_log.LogWarning("Host ban file {Path} has version {Version}; this build reads version {Current} — treated as empty.",
					_filePath, file.Version, HostBanFile.CurrentVersion);
				return false;
			}

			foreach (var steamId in file.BannedSteamIds)
			{
				if (steamId == 0)
				{
					_log.LogWarning("Host ban file {Path} contains an invalid zero SteamID — skipped.", _filePath);
					continue;
				}

				banned.Add(steamId);
			}

			_log.LogInformation("Loaded {Count} banned SteamID(s) from {Path}.", banned.Count, _filePath);
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Host ban file {Path} is corrupt or unreadable — treated as empty.", _filePath);
			banned = [];
			return false;
		}
	}

	/// <summary>Atomically replaces the file with the full ban list. False = the write failed (in-memory state stays authoritative for this process).</summary>
	internal bool Save(IEnumerable<ulong> banned)
	{
		if (!IsEnabled)
		{
			return true;
		}

		var file = new HostBanFile { BannedSteamIds = [.. banned] };
		return WriteAtomically(file);
	}

	private bool WriteAtomically(HostBanFile file)
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
			_log.LogWarning(ex, "Failed to write host ban file {Path} — the in-memory ban list keeps working for this process.", path);
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
			_log.LogWarning(ex, "Failed to remove the abandoned host ban temp file {Path}.", tempPath);
		}
	}
}
