using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Promotes a backup archive into a world's live snapshot, preserving the refused
/// snapshot it replaces as evidence (§6, S4's acceptance row 6).
///
/// A restore that has to fall back — a live manifest that does not read, or a live
/// snapshot the DECODE refuses after the manifest read — used to leave the world folder
/// exactly as it was: the good archive was read in memory, and the refused snapshot stayed
/// in <c>live/</c> until the next cut's transaction renamed it to <c>.previous/</c> and
/// deleted it. The evidence of what went wrong was destroyed by the very next autosave.
/// This is the other half: the good archive becomes the world's live snapshot, and the
/// refused one is kept where nothing writes.
///
/// Order is load-bearing, and it is the transaction order of §5 — nothing is moved until
/// the replacement is safely on disk:
/// <list type="number">
/// <item>the backup is unpacked into a fresh <c>.staging/</c> (the live snapshot is
/// untouched, so an archive that cannot be unpacked changes nothing);</item>
/// <item>the snapshot about to be replaced is ARCHIVED — the pre-restore copy of §6, its
/// manifest's reason rewritten to <c>pre-restore-backup</c> — so the world's history holds
/// a loadable copy where the retention policy and the fallback can see it;</item>
/// <item>the snapshot being replaced is moved aside — to <c>damaged-&lt;stamp&gt;/</c>, or to
/// <c>.previous/</c> for a promotion that already holds a copy of it (see the trigger) —
/// and the staging folder takes its place.</item>
/// </list>
/// A failure in step 3 puts the aside folder back, so a promotion that cannot finish
/// leaves the world as it found it — with ONE residual, and it is named rather than
/// implied: if the filesystem refuses the put-back as well (the same condition that broke
/// the swap), the replaced snapshot stays in <c>.previous/</c> or in <c>damaged-&lt;stamp&gt;/</c>
/// with <c>live/</c> absent. Nothing is lost — <c>WorldFolderRecovery</c> puts a lone
/// <c>.previous/</c> back into <c>live/</c> on the next load, and the pre-restore archive
/// holds the same state in <c>backups/</c> — but that path is logged as an error and is
/// the one case where this promise does not hold literally.
///
/// <see cref="WorldPromotionTrigger"/> decides which of the two shapes step 3 takes: the
/// recovery keeps the snapshot it replaced as evidence, a player-chosen restore keeps it
/// only when the pre-restore archive could not be written, because there the archive IS
/// the copy.
/// </summary>
internal static class WorldBackupPromotion
{
	/// <summary>What a promotion did: whether <c>live/</c> now holds the backup, and the lines the restore's account shows.</summary>
	internal sealed record Result(bool Success, string Detail, IReadOnlyList<string> Account)
	{
		internal static Result Refused(string detail) => new(false, detail, []);
	}

	/// <summary>
	/// Replaces <paramref name="worldDirectory"/>'s live snapshot with
	/// <paramref name="backup"/>, archiving the snapshot it replaces first.
	/// <paramref name="trigger"/> decides what becomes of that replaced snapshot's own
	/// folder once the archive of it exists (see <see cref="WorldPromotionTrigger"/>).
	/// False = nothing was promoted, and the world folder was left as it was found (the
	/// detail says why).
	/// </summary>
	internal static Result Promote(string worldDirectory, WorldBackup backup, WorldPromotionTrigger trigger, DateTime nowUtc, ILogger log)
	{
		var live = Path.Combine(worldDirectory, SaveArchiveFormat.LiveFolderName);
		var staging = Path.Combine(worldDirectory, SaveArchiveFormat.StagingFolderName);
		var account = new List<string>();

		DiscardStaging(staging, log);
		try
		{
			Extract(backup.FullPath, staging);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or SaveArchivePathException or ArgumentException)
		{
			log.LogError(ex, "Backup {Backup} could not be unpacked; the live snapshot is left exactly as it was.", backup.FileName);
			DiscardStaging(staging, log);
			return Result.Refused($"backup {backup.FileName} could not be unpacked ({ex.Message})");
		}

		// Best effort, and reported either way: a snapshot that cannot be archived (a
		// manifest that does not read, bytes that no longer match it) is still preserved
		// as a folder below, so the copy is a second chance, never the only one.
		var preRestore = ArchivePreRestore(live, worldDirectory, nowUtc, log);
		account.Add(preRestore.Line);

		// What becomes of the replaced snapshot's folder, now that its copy either does or
		// does not exist: the recovery keeps it as evidence, a player-chosen restore reuses
		// the writer's `.previous` swap — but only while the pre-restore archive is really on
		// disk, because then that archive is the copy and a `damaged-` folder would be one
		// more full snapshot per restore under a name nothing may ever delete.
		var keepEvidence = trigger == WorldPromotionTrigger.RefusedSnapshot || !preRestore.Archived;
		string? aside;
		try
		{
			aside = keepEvidence
				? PreserveLiveAsEvidence(worldDirectory, live, nowUtc, log)
				: MoveLiveToPrevious(worldDirectory, live, log);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Nothing has been promoted at this point and live/ is still where it was, so a
			// filesystem refusal here is a refused restore rather than a half-replaced world
			// — which is the one thing this method promises its callers.
			log.LogError(ex, "The snapshot being replaced could not be moved aside; nothing was promoted.");
			DiscardStaging(staging, log);
			return Result.Refused($"the snapshot being replaced could not be moved aside ({ex.Message})");
		}

		try
		{
			Directory.Move(staging, live);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			log.LogError(ex, "The promoted backup {Backup} could not be moved into live/; the replaced snapshot is put back.", backup.FileName);
			PutEvidenceBack(aside, live, log);
			DiscardStaging(staging, log);
			return Result.Refused($"the promoted backup could not replace the live snapshot ({ex.Message})");
		}

		if (!keepEvidence)
		{
			// The new snapshot is live and the pre-restore archive holds the state that was
			// replaced, so the transient copy goes. A failure here is not a lost world: the
			// next load discards the same leftover.
			DeletePrevious(worldDirectory, log);
		}

		var replaced = aside is null
			? "there was no live snapshot to replace"
			: keepEvidence
				? $"the refused live snapshot is preserved at {Path.GetFileName(aside)}"
				: "the replaced snapshot is archived as the pre-restore backup and its folder was removed";
		account.Add($"{replaced}; backup {backup.FileName} was promoted to the live snapshot");
		log.LogWarning("World {Directory}: {Account}", worldDirectory, account[account.Count - 1]);
		return new Result(true, $"backup {backup.FileName} promoted to the live snapshot", account);
	}

	/// <summary>
	/// The pre-restore copy of §6: the snapshot about to be replaced is archived into
	/// <c>backups/</c> with its reason rewritten, so the world's history holds a loadable
	/// copy under the retention policy. Returns the account line for this half and whether
	/// the archive really is on disk — the caller's decision about the replaced folder
	/// depends on it (the archive is that state's only other copy).
	/// </summary>
	private static (string Line, bool Archived) ArchivePreRestore(string live, string worldDirectory, DateTime nowUtc, ILogger log)
	{
		// No live snapshot at all is not the same fact as an unreadable one, and the account says
		// which it was: a world whose live folder is gone (a failed earlier attempt, a folder a
		// player cleaned up) restores from its archive without a pre-restore copy of anything.
		if (!Directory.Exists(live))
		{
			return ("there was no live snapshot to archive (the world folder held none)", false);
		}

		var manifestPath = Path.Combine(live, SaveArchiveFormat.ManifestFileName);
		SaveManifest? manifest;
		try
		{
			manifest = File.Exists(manifestPath) ? SaveArchiveJson.Deserialize<SaveManifest>(File.ReadAllBytes(manifestPath)) : null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			log.LogWarning(ex, "The manifest of the snapshot about to be replaced could not be read; it is preserved as a folder only.");
			manifest = null;
		}

		if (manifest is null)
		{
			return ($"the snapshot about to be replaced carries no readable manifest, so it is preserved as a folder only (no pre-restore archive was written)", false);
		}

		var preRestore = AsPreRestore(manifest, nowUtc);

		try
		{
			var archive = SaveArchiveWriter.ArchiveExistingSnapshot(live, Path.Combine(worldDirectory, SaveArchiveFormat.BackupsFolderName), preRestore, nowUtc, log);
			return ($"the snapshot about to be replaced was archived as {archive.FileName} (pre-restore backup)", true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			return ($"the pre-restore archive of the snapshot about to be replaced could not be written ({ex.Message}); its preserved folder is the only copy", false);
		}
	}

	/// <summary>
	/// The same snapshot with its PROVENANCE rewritten: the reason becomes
	/// <c>pre-restore-backup</c> and the stamp is the moment of the promotion. Everything the
	/// snapshot HOLDS is copied field by field — including its kind, which is what the archive
	/// name carries, and its file list with the checksums the archive is verified against.
	/// </summary>
	private static SaveManifest AsPreRestore(SaveManifest manifest, DateTime nowUtc) => new()
	{
		SchemaVersion = manifest.SchemaVersion,
		Format = manifest.Format,
		GameBuild = manifest.GameBuild,
		CuoBuild = manifest.CuoBuild,
		ProtocolVersion = manifest.ProtocolVersion,
		ContentFingerprint = manifest.ContentFingerprint,
		WorldId = manifest.WorldId,
		DisplayName = manifest.DisplayName,
		Kind = manifest.Kind,
		RunEpoch = manifest.RunEpoch,
		GlobalRevision = manifest.GlobalRevision,
		LayerIndex = manifest.LayerIndex,
		BiomeDepth = manifest.BiomeDepth,
		CutPhase = manifest.CutPhase,
		SaveReason = SaveArchiveFormat.CutReasonName(WorldCutReason.PreRestoreBackup),
		SavedAtUtc = SaveArchiveFormat.FormatUtc(nowUtc),
		ChecksumPolicy = manifest.ChecksumPolicy,
		Files = manifest.Files,
	};

	/// <summary>
	/// Moves <c>live/</c> to <c>damaged-&lt;stamp&gt;[-n]/</c> and returns the folder, or null
	/// when there was no live snapshot. The name never collides: a second promotion inside
	/// the same second gets a <c>-n</c> suffix, exactly like a backup of the same second.
	/// </summary>
	private static string? PreserveLiveAsEvidence(string worldDirectory, string live, DateTime nowUtc, ILogger log)
	{
		if (!Directory.Exists(live))
		{
			return null;
		}

		var stem = SaveArchiveFormat.DamagedFolderPrefix + SaveArchiveFormat.StampOf(nowUtc);
		var preserved = Path.Combine(worldDirectory, stem);
		for (var suffix = 2; Directory.Exists(preserved); suffix++)
		{
			preserved = Path.Combine(worldDirectory, $"{stem}-{suffix}");
		}

		Directory.Move(live, preserved);
		log.LogWarning("The refused live snapshot was preserved at {Preserved} (evidence of what the restore could not open).", preserved);
		return preserved;
	}

	/// <summary>Puts a preserved folder back where it came from, for a promotion whose last step failed.</summary>
	private static void PutEvidenceBack(string? preserved, string live, ILogger log)
	{
		if (preserved is null)
		{
			return;
		}

		try
		{
			if (!Directory.Exists(live))
			{
				Directory.Move(preserved, live);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// The snapshot is not lost — it is still in the folder named here, and the next load
			// puts a lone `.previous/` back into live/ or falls back to the archives — but the
			// world folder is no longer in the state it was found, which is what this rollback
			// exists to guarantee. The pre-restore archive of the same state is the other copy.
			log.LogError(ex, "The replaced snapshot {Preserved} could not be put back into {Live}; it stays where it is, and the next load recovers it (the pre-restore archive holds the same state).", preserved, live);
		}
	}

	/// <summary>
	/// Moves <c>live/</c> to <c>.previous/</c> — the writer's own transient name (§5) — for a
	/// promotion that already holds the replaced state in <c>backups/</c>. Returns the folder,
	/// or null when there was no live snapshot. A leftover from an interrupted commit goes
	/// first: the next load would discard it anyway, and leaving two of them would make the
	/// name ambiguous.
	/// </summary>
	private static string? MoveLiveToPrevious(string worldDirectory, string live, ILogger log)
	{
		if (!Directory.Exists(live))
		{
			return null;
		}

		var previous = Path.Combine(worldDirectory, SaveArchiveFormat.PreviousFolderName);
		DeletePrevious(worldDirectory, log);
		Directory.Move(live, previous);
		return previous;
	}

	/// <summary>
	/// Removes the <c>.previous/</c> folder a finished swap left behind. Best effort and
	/// logged: what it holds is already archived under <c>backups/</c> and the next load
	/// discards the same leftover, so a failure costs disk, never a world.
	/// </summary>
	private static void DeletePrevious(string worldDirectory, ILogger log)
	{
		var previous = Path.Combine(worldDirectory, SaveArchiveFormat.PreviousFolderName);
		try
		{
			if (Directory.Exists(previous))
			{
				Directory.Delete(previous, recursive: true);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			log.LogWarning(ex, "The transient {Previous} folder could not be removed; it stays on disk and the next load discards it.", SaveArchiveFormat.PreviousFolderName);
		}
	}

	/// <summary>
	/// Unpacks an archive into <paramref name="destination"/>, refusing any entry name the
	/// path policy rejects: a backup is a file a player can copy between machines, so its
	/// entries are untrusted input exactly like a payload path.
	/// </summary>
	private static void Extract(string archivePath, string destination)
	{
		Directory.CreateDirectory(destination);
		using var stream = File.OpenRead(archivePath);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
		foreach (var entry in archive.Entries)
		{
			if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
			{
				continue;
			}

			var relative = ArchivePathPolicy.ValidateArchiveEntryName(entry.FullName);
			var target = ArchivePathPolicy.CombineWithin(destination, relative);
			var directory = Path.GetDirectoryName(target);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using var entryStream = entry.Open();
			using var targetStream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
			entryStream.CopyTo(targetStream);
		}
	}

	/// <summary>Removes a staging folder this promotion created or found; a leftover from an interrupted write is never read.</summary>
	private static void DiscardStaging(string staging, ILogger log)
	{
		try
		{
			if (Directory.Exists(staging))
			{
				Directory.Delete(staging, recursive: true);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			log.LogWarning(ex, "The staging folder {Staging} could not be cleared before the promotion.", staging);
		}
	}
}
