using System;
using System.Globalization;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Worlds page (decision 198): the worlds the repository holds, the backup archives of
/// one of them, and the restore that replaces a world's live snapshot with an archive the
/// player picks. This is the picker the repository's <c>index.json</c> was already built for.
///
/// Two rules shape the whole page:
/// <list type="number">
/// <item>it READS on demand. Listing worlds walks the repository's folders and their backup
/// files, and an IMGUI draw callback runs more than once per frame — so the rows live in the
/// window state and are reloaded only when <see cref="IWorldLibrary.Revision"/> moved (the
/// first draw, every committed cut of any trigger, every management action) or when Refresh is
/// clicked. A list left open on screen therefore follows the world, while a draw pass touches
/// no directory;</item>
/// <item>it only ARMS what it may not do itself. A restore replaces the snapshot of a world
/// the player is not currently playing, at a frame boundary, exactly once — so the click
/// records the intent through <see cref="IWorldLibrary.TryRequestRestore"/> and the pump does
/// the promotion. That is also why a restore takes two clicks: the first picks the archive,
/// the second confirms, and the archive of the state being replaced is written by the very
/// same action.</item>
/// </list>
///
/// The refusals and results this page shows come from the Runtime and are shown verbatim,
/// exactly like the console shows a cut report: one wording, one owner.
/// </summary>
internal static class OnlineUiWorldsDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var library = ctx.WorldLibrary;
		if (library is null || !library.IsEnabled)
		{
			GUILayout.Label(ctx.T("worlds.unavailable"), OnlineUiTheme.MutedLabel());
			return;
		}

		var state = ctx.State;

		// ONE staleness rule for every way the data can move: the library bumps its revision on
		// every committed cut (any trigger — a /save, the autosave, a layer boundary, a menu
		// return), on every management action, and this starts at -1 so the first draw loads.
		if (state.SeenRevision != library.Revision)
		{
			Reload(ctx, library);
		}

		DrawHeader(ctx, library);
		DrawStatus(ctx, library);
		DrawWorlds(ctx, library);
		DrawBackups(ctx, library);
	}

	private static void DrawHeader(OnlineUiContext ctx, IWorldLibrary library)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("worlds.section"), OnlineUiTheme.Section());
		GUILayout.FlexibleSpace();
		if (GUILayout.Button(ctx.T("worlds.refresh"), OnlineUiTheme.Button(), GUILayout.Width(110f)))
		{
			Reload(ctx, library);
		}

		GUILayout.EndHorizontal();
	}

	/// <summary>The one line that says what the last action did — or why it did nothing.</summary>
	private static void DrawStatus(OnlineUiContext ctx, IWorldLibrary library)
	{
		if (ctx.Session.Role == SessionRole.Guest)
		{
			GUILayout.Label(ctx.T("worlds.host_only"), OnlineUiTheme.MutedLabel());
		}

		if (library.HasArmedRestore)
		{
			GUILayout.Label(ctx.T("worlds.restore_queued"), OnlineUiTheme.Status(OnlineUiTheme.Warning));
			return;
		}

		if (library.LastReport is { } report)
		{
			GUILayout.Label(
				ctx.F("worlds.last_action", report.Detail),
				OnlineUiTheme.Status(report.Succeeded ? OnlineUiTheme.Positive : OnlineUiTheme.Error));
		}
	}

	private static void DrawWorlds(OnlineUiContext ctx, IWorldLibrary library)
	{
		if (ctx.State.WorldRows.Count == 0)
		{
			GUILayout.Label(ctx.T("worlds.empty"), OnlineUiTheme.MutedLabel());
			return;
		}

		foreach (var row in ctx.State.WorldRows)
		{
			DrawWorldRow(ctx, library, row);
		}
	}

	private static void DrawWorldRow(OnlineUiContext ctx, IWorldLibrary library, WorldLibraryEntry row)
	{
		var state = ctx.State;
		GUILayout.Label(row.World.DisplayName, OnlineUiTheme.Label());
		GUILayout.Label(
			ctx.F("worlds.row_detail", row.World.LayerIndex, row.World.PlayerCount, row.BackupCount, LocalTime(row.World.LastSavedUtc)),
			OnlineUiTheme.MutedLabel());

		GUILayout.BeginHorizontal();
		if (!row.HasSnapshot)
		{
			GUILayout.Label(ctx.T("worlds.no_snapshot"), OnlineUiTheme.MutedLabel());
		}
		else if (row.IsSelected)
		{
			GUILayout.Label(ctx.T("worlds.continue_target"), OnlineUiTheme.MutedLabel());
		}
		else if (GUILayout.Button(ctx.T("worlds.select"), OnlineUiTheme.Button(), GUILayout.Width(180f)))
		{
			// A refusal is not swallowed: the library records it and the status line above shows
			// its reason on the next draw.
			library.TrySelectWorld(row.World.WorldId, out _);
		}

		if (GUILayout.Button(ctx.T("worlds.show_backups"), OnlineUiTheme.Button(), GUILayout.Width(120f)))
		{
			state.BackupRowsWorldId = row.World.WorldId;
			state.BackupRows = [.. library.ListBackups(row.World.WorldId)];
			state.PendingRestoreFile = "";
		}

		GUILayout.EndHorizontal();
		GUILayout.Space(6f);
	}

	private static void DrawBackups(OnlineUiContext ctx, IWorldLibrary library)
	{
		var state = ctx.State;
		if (state.BackupRowsWorldId.Length == 0)
		{
			return;
		}

		GUILayout.Label(ctx.F("worlds.backups_section", DisplayNameOf(state, state.BackupRowsWorldId)), OnlineUiTheme.Section());
		if (state.BackupRows.Count == 0)
		{
			GUILayout.Label(ctx.T("worlds.backups_empty"), OnlineUiTheme.MutedLabel());
			return;
		}

		foreach (var backup in state.BackupRows)
		{
			DrawBackupRow(ctx, library, backup);
		}
	}

	private static void DrawBackupRow(OnlineUiContext ctx, IWorldLibrary library, WorldBackup backup)
	{
		var state = ctx.State;
		GUILayout.Label(
			ctx.F("worlds.backup_row", ctx.T(KindKeyOf(backup.Kind)), LocalTime(backup.Stamp), SizeMb(backup)),
			OnlineUiTheme.Label());

		if (string.Equals(state.PendingRestoreFile, backup.FileName, StringComparison.Ordinal))
		{
			GUILayout.Label(ctx.F("worlds.restore_confirm", backup.FileName), OnlineUiTheme.MutedLabel());
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(ctx.T("worlds.restore_confirm_yes"), OnlineUiTheme.Button(), GUILayout.Width(150f)))
			{
				state.PendingRestoreFile = "";
				// Armed here, executed by the frame pump: this code runs inside a draw callback.
				library.TryRequestRestore(
					state.BackupRowsWorldId,
					backup.FileName,
					ctx.WorldPresence?.IsInWorldOrGenerating ?? false,
					out _);
			}

			if (GUILayout.Button(ctx.T("worlds.restore_cancel"), OnlineUiTheme.Button(), GUILayout.Width(110f)))
			{
				state.PendingRestoreFile = "";
			}

			GUILayout.EndHorizontal();
		}
		else if (GUILayout.Button(ctx.T("worlds.restore"), OnlineUiTheme.Button(), GUILayout.Width(120f)))
		{
			state.PendingRestoreFile = backup.FileName;
		}

		GUILayout.Space(4f);
	}

	/// <summary>Re-reads what the page shows. Cheap enough to run on demand, far too costly to run per draw pass.</summary>
	private static void Reload(OnlineUiContext ctx, IWorldLibrary library)
	{
		var state = ctx.State;
		state.WorldRows = [.. library.ListWorlds()];
		state.BackupRows = state.BackupRowsWorldId.Length == 0 ? [] : [.. library.ListBackups(state.BackupRowsWorldId)];
		state.PendingRestoreFile = "";
		// Last, and after the reads: the rows now describe THIS revision.
		state.SeenRevision = library.Revision;
	}

	private static string DisplayNameOf(OnlineUiWindowState state, string worldId)
	{
		foreach (var row in state.WorldRows)
		{
			if (string.Equals(row.World.WorldId, worldId, StringComparison.Ordinal))
			{
				return row.World.DisplayName;
			}
		}

		return worldId;
	}

	private static string KindKeyOf(WorldCutKind kind) => kind switch
	{
		WorldCutKind.LayerEnd => "worlds.kind.layer_end",
		WorldCutKind.Auto => "worlds.kind.auto",
		_ => "worlds.kind.mid_run",
	};

	/// <summary>
	/// An archive's size in whole megabytes: what a player needs to judge "another one of
	/// these", not a byte count. An archive that cannot be measured is reported as such
	/// instead of as 0.
	/// </summary>
	private static string SizeMb(WorldBackup backup)
	{
		try
		{
			var megabytes = new FileInfo(backup.FullPath).Length / (1024.0 * 1024.0);
			return megabytes.ToString("0.0", CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return "?";
		}
	}

	/// <summary>
	/// A stored timestamp in the player's own local time. Both shapes the archive uses are
	/// parsed by their own format and never guessed; text that is neither is shown as it is.
	/// </summary>
	private static string LocalTime(string stored)
	{
		if (SaveArchiveFormat.TryParseUtc(stored, out var utc))
		{
			return Local(utc);
		}

		return DateTime.TryParseExact(
			stored,
			SaveArchiveFormat.BackupStampFormat,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var stamp)
			? Local(stamp)
			: stored;
	}

	private static string Local(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
}
