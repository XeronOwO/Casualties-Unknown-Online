using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Crash-leftover handling for a world folder (§5). A transaction can be
/// interrupted between any two steps, so a load has to decide what the folder
/// state means before it reads anything:
/// <list type="bullet">
/// <item><c>live/</c> + <c>.previous/</c> — the swap completed but the cleanup did not: keep <c>live/</c>, discard <c>.previous/</c>.</item>
/// <item><c>.previous/</c> without <c>live/</c> — the swap was interrupted after the first rename: restore <c>.previous/</c> to <c>live/</c>.</item>
/// <item><c>.staging/</c> — the cut never committed: discard it.</item>
/// </list>
/// Nothing here is allowed to delete a snapshot that is currently live, and
/// every decision is reported so the load result can surface it.
/// </summary>
public static class WorldFolderRecovery
{
	/// <summary>What was done about a leftover, in the order it was done.</summary>
	public enum RecoveryKind
	{
		/// <summary>An interrupted commit's staging folder was discarded.</summary>
		DiscardedStaging,

		/// <summary>An interrupted commit's previous snapshot was restored to <c>live/</c>.</summary>
		RestoredPrevious,

		/// <summary>A completed commit's leftover previous snapshot was discarded.</summary>
		DiscardedPrevious,

		/// <summary>A backup archive abandoned before its rename was discarded.</summary>
		DiscardedAbandonedArchive,
	}

	/// <summary>One decision: what happened and to which path.</summary>
	public sealed record RecoveryAction(RecoveryKind Kind, string Path, string Detail);

	/// <summary>The recovery outcome: the actions taken and whether a readable live snapshot exists afterwards.</summary>
	public sealed record RecoveryResult(IReadOnlyList<RecoveryAction> Actions, bool LiveExists, string? FailureDetail)
	{
		/// <summary>True = the folder needed a decision (a crash window was found).</summary>
		public bool RecoveredAnything => Actions.Count > 0;
	}

	/// <summary>
	/// Brings <paramref name="worldDirectory"/> into a state where <c>live/</c> means one snapshot,
	/// and reports every decision. A failed repair is reported, never hidden: a half-repaired
	/// folder must not be read as if it were intact.
	/// </summary>
	public static RecoveryResult Recover(string worldDirectory, ILogger log)
	{
		var actions = new List<RecoveryAction>();
		var live = Path.Combine(worldDirectory, SaveArchiveFormat.LiveFolderName);
		var staging = Path.Combine(worldDirectory, SaveArchiveFormat.StagingFolderName);
		var previous = Path.Combine(worldDirectory, SaveArchiveFormat.PreviousFolderName);

		try
		{
			if (Directory.Exists(previous) && !Directory.Exists(live))
			{
				Directory.Move(previous, live);
				actions.Add(new RecoveryAction(RecoveryKind.RestoredPrevious, live,
					"An interrupted commit left a previous snapshot without a live one; the previous snapshot is live again."));
			}
			else if (Directory.Exists(previous))
			{
				Directory.Delete(previous, recursive: true);
				actions.Add(new RecoveryAction(RecoveryKind.DiscardedPrevious, previous,
					"The commit had already completed; the leftover previous snapshot was discarded (the same cut is in backups)."));
			}

			if (Directory.Exists(staging))
			{
				Directory.Delete(staging, recursive: true);
				actions.Add(new RecoveryAction(RecoveryKind.DiscardedStaging, staging,
					"An uncommitted staging folder was discarded; the live snapshot was never replaced."));
			}

			SweepAbandonedArchivesInto(worldDirectory, actions, log);

			var liveExists = Directory.Exists(live);
			var failure = liveExists ? null : "the world folder holds no live snapshot";
			Log(log, actions, liveExists, failure);
			return new RecoveryResult(actions, liveExists, failure);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Log(log, actions, Directory.Exists(live), ex.Message);
			return new RecoveryResult(actions, Directory.Exists(live), ex.Message);
		}
	}

	/// <summary>
	/// Removes <c>backups/*.cuoz.tmp</c> left by a crash between writing the archive
	/// and renaming it into place, and returns the archives actually deleted. Nothing
	/// loads such a file, but without a sweep it would survive every later write,
	/// load and prune forever. <see cref="Recover"/> calls this; so does every write,
	/// so a crashed archive never accumulates.
	/// </summary>
	public static IReadOnlyList<string> SweepAbandonedArchives(string worldDirectory, ILogger log)
	{
		var backups = Path.Combine(worldDirectory, SaveArchiveFormat.BackupsFolderName);
		if (!Directory.Exists(backups))
		{
			return [];
		}

		var deleted = new List<string>();
		foreach (var abandoned in Directory.EnumerateFiles(backups, "*" + SaveArchiveFormat.BackupExtension + ".tmp"))
		{
			try
			{
				File.Delete(abandoned);
				deleted.Add(abandoned);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A temp archive that cannot be removed is harmless — nothing reads it
				// and the next sweep tries again — but it must not vanish silently.
				log.LogWarning(ex, "The abandoned archive {Archive} could not be removed; the next sweep will try again.", abandoned);
			}
		}

		return deleted;
	}

	/// <summary>Removes the open folder's leftovers: the staging folder, the previous snapshot and abandoned archives.</summary>
	private static void SweepAbandonedArchivesInto(string worldDirectory, List<RecoveryAction> actions, ILogger log)
	{
		foreach (var abandoned in SweepAbandonedArchives(worldDirectory, log))
		{
			actions.Add(new RecoveryAction(RecoveryKind.DiscardedAbandonedArchive, abandoned,
				"An archive was written but never renamed into place; it was discarded (the cut it belonged to never committed)."));
		}
	}

	private static void Log(ILogger log, IReadOnlyList<RecoveryAction> actions, bool liveExists, string? failure)
	{
		foreach (var action in actions)
		{
			log.LogWarning("World folder recovery {Kind} at {Path}: {Detail}", action.Kind, action.Path, action.Detail);
		}

		if (actions.Count == 0)
		{
			log.LogDebug("World folder recovery found no crash leftovers (live present: {LiveExists}).", liveExists);
		}
		else if (failure is not null)
		{
			log.LogError("World folder recovery finished with leftovers unresolved: {Failure}", failure);
		}
		else
		{
			log.LogWarning("World folder recovery applied {Count} action(s); live snapshot present: {LiveExists}.", actions.Count, liveExists);
		}
	}
}
