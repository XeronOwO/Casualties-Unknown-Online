using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The retry a decode-level refusal never had (§6, S3 scope 7, S4's acceptance row 6).
///
/// The reader's own fallback runs while the MANIFEST is read: a snapshot whose manifest
/// does not read is replaced by the newest readable backup before the load returns. A
/// refusal that happens LATER — the manifest read fine, but the payload decodes into a
/// self-contradictory snapshot (a <c>layer-end</c> cut that carries in-layer rows), or the
/// run baseline cannot be read — arrives after that fallback has already returned, so the
/// world simply would not open, and the next cut's transaction would delete the refused
/// snapshot it could not explain.
///
/// This walks the world's backups newest-first, opens each in memory through the same
/// manifest gate, decodes it with the same decoder the caller used, and takes the first one
/// that decodes — then PROMOTES it: <see cref="WorldRepository.PromoteBackup"/> preserves
/// the refused snapshot as evidence, archives the pre-restore copy and makes the backup the
/// live snapshot. The world folder, not the archive, is what every later half of the restore
/// works on.
///
/// Every refusal is named in the account it returns: which backup was tried, what it refused
/// with, and what the promotion did. A recovery that finds nothing returns null and the
/// caller refuses the continue with the reason it already had (§6: silent loss is forbidden).
/// </summary>
internal sealed class WorldRestoreRecovery(
	WorldRepository? repository,
	ILoggerFactory loggerFactory,
	ILogger<WorldRestoreRecovery> log)
{
	private readonly WorldRepository? _repository = repository;
	private readonly ILoggerFactory _loggerFactory = loggerFactory;
	private readonly ILogger<WorldRestoreRecovery> _log = log;

	/// <summary>What a recovery produced: the promoted load, its decode, its salvage and the lines the restore's account shows.</summary>
	internal sealed record Outcome(
		WorldLoadResult Load,
		WorldSnapshotDecode Decode,
		SalvageResult Salvage,
		IReadOnlyList<string> Account);

	/// <summary>
	/// Recovers <paramref name="worldId"/> from its own newest decodable backup, or null when
	/// none of them can be restored. <paramref name="refusal"/> is the refusal of the snapshot
	/// the caller was holding (kept in the account so the cause is never lost), and
	/// <paramref name="refused"/> is that load — the backup it already opened is skipped
	/// rather than decoded a second time.
	/// </summary>
	internal Outcome? TryRecover(string worldId, WorldLoadOptions options, WorldLoadResult refused, string refusal)
	{
		if (_repository is null)
		{
			return null;
		}

		var account = new List<string> { $"the live snapshot was refused ({refusal})" };
		foreach (var backup in _repository.ListBackups(worldId))
		{
			if (IsAlreadyOpened(refused, backup))
			{
				continue;
			}

			var candidate = _repository.LoadBackup(worldId, backup, options);
			if (candidate.Content is null)
			{
				account.Add($"backup {backup.FileName} could not be opened");
				continue;
			}

			var decode = Decode(candidate, options, out var salvage);
			if (decode.Checkpoint is null)
			{
				account.Add($"backup {backup.FileName} was refused too ({decode.Refusal})");
				continue;
			}

			var promotion = _repository.PromoteBackup(worldId, backup);
			if (!promotion.Success)
			{
				account.Add($"backup {backup.FileName} could not be promoted ({promotion.Detail})");
				continue;
			}

			account.AddRange(promotion.Account);

			// From here on the world FOLDER is the source, not the archive: the restore's
			// later halves — the world-entry seam, the next cut — read and write live/.
			var promoted = _repository.LoadSnapshot(worldId, options);
			var promotedDecode = promoted.Content is null ? null : Decode(promoted, options, out salvage);
			if (promoted.Content is null || promotedDecode!.Checkpoint is null)
			{
				account.Add("the promoted snapshot could not be reopened");
				continue;
			}

			_log.LogWarning("World {WorldId} was recovered from backup {Backup}: {Account}", worldId, backup.FileName, string.Join("; ", account));
			return new Outcome(promoted, promotedDecode, salvage, account);
		}

		_log.LogError("World {WorldId} could not be recovered from any of its backups: {Account}", worldId, string.Join("; ", account));
		return null;
	}

	/// <summary>Runs the production decoder over one opened snapshot — the same one the caller used, so a candidate is judged by the rule that refused the live snapshot.</summary>
	private WorldSnapshotDecode Decode(WorldLoadResult load, WorldLoadOptions options, out SalvageResult salvage)
	{
		var decoder = new WorldSnapshotDecoder(load.Content!.Manifest, _loggerFactory.CreateLogger<WorldSnapshotDecoder>());
		var (_, read) = _repository!.ReadSalvage(load, decoder.DecodeEntry, options);
		salvage = read;
		return decoder.Finish();
	}

	/// <summary>True = the caller's load already came from this archive (the reader's manifest fallback), so decoding it again proves nothing.</summary>
	private static bool IsAlreadyOpened(WorldLoadResult refused, WorldBackup backup) =>
		refused.Content?.SourcePath is { Length: > 0 } source
		&& string.Equals(Path.GetFullPath(source), Path.GetFullPath(backup.FullPath), StringComparison.OrdinalIgnoreCase);
}
