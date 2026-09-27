using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Worlds page (decision 198): the worlds the repository holds, the backup archives of
/// one of them, and the restore that replaces a world's live snapshot with an archive the
/// player picks. This is the picker the repository's <c>index.json</c> was already built for.
///
/// Two rules shape the whole page:
/// <list type="number">
/// <item>it READS on demand. Listing worlds walks the repository's folders and their backup
/// files, and the page is rebuilt on every frame the window is open — so the rows live in the
/// window state and are reloaded only when <see cref="IWorldLibrary.Revision"/> moved (the
/// first build, every committed cut of any trigger, every management action) or when Refresh is
/// clicked. A list left open on screen therefore follows the world, while a frame that changes
/// nothing touches no directory;</item>
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
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) the page builds a display list instead of
/// drawing itself; every button is a row on the game's own control and its click comes back as an
/// intent carrying the id registered here.
/// </para>
/// </summary>
internal static class OnlineUiWorldsDrawer
{
	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var library = ctx.WorldLibrary;
		if (library is null || !library.IsEnabled)
		{
			page.Muted(ctx.T("worlds.unavailable"));
			return;
		}

		var state = ctx.State;

		// ONE staleness rule for every way the data can move: the library bumps its revision on
		// every committed cut (any trigger — a /save, the autosave, a layer boundary, a menu
		// return), on every management action, and this starts at -1 so the first build loads.
		if (state.SeenRevision != library.Revision)
		{
			Reload(ctx, library);
		}

		BuildHeader(ctx, page, library);
		BuildStatus(ctx, page, library);
		BuildWorlds(ctx, page, library);
		BuildBackups(ctx, page, library);
	}

	/// <summary>The page's heading and the Reload control that belongs on its line: built through
	/// <see cref="OnlineUiPageBuilder.Section(string, OnlineUiElementModel[])"/>, so the heading keeps the room
	/// every other heading on every page owns (the acceptance pass found one heading without it).</summary>
	private static void BuildHeader(OnlineUiContext ctx, OnlineUiPageBuilder page, IWorldLibrary library) =>
		page.Section(
			ctx.T("worlds.section"),
			page.ButtonElement("worlds.refresh", ctx.T("worlds.refresh"), () => Reload(ctx, library), width: 110f));

	/// <summary>The one line that says what the last action did — or why it did nothing.</summary>
	private static void BuildStatus(OnlineUiContext ctx, OnlineUiPageBuilder page, IWorldLibrary library)
	{
		if (ctx.Session.Role == SessionRole.Guest)
		{
			page.Muted(ctx.T("worlds.host_only"));
		}

		if (library.HasArmedRestore)
		{
			page.Status(ctx.T("worlds.restore_queued"), OnlineUiTheme.Warning);
			return;
		}

		if (library.LastReport is { } report)
		{
			page.Status(
				ctx.F("worlds.last_action", report.Detail),
				report.Succeeded ? OnlineUiTheme.Positive : OnlineUiTheme.Error);
		}
	}

	private static void BuildWorlds(OnlineUiContext ctx, OnlineUiPageBuilder page, IWorldLibrary library)
	{
		if (ctx.State.WorldRows.Count == 0)
		{
			page.Muted(ctx.T("worlds.empty"));
			return;
		}

		foreach (var row in ctx.State.WorldRows)
		{
			BuildWorldRow(ctx, page, library, row);
		}
	}

	private static void BuildWorldRow(OnlineUiContext ctx, OnlineUiPageBuilder page, IWorldLibrary library, WorldLibraryEntry row)
	{
		var state = ctx.State;
		page.Label(row.World.DisplayName);
		page.Muted(ctx.F(
			"worlds.row_detail",
			row.World.LayerIndex,
			row.World.PlayerCount,
			row.BackupCount,
			LocalTime(row.World.LastSavedUtc)));

		var elements = new List<OnlineUiElementModel>(2);
		if (!row.HasSnapshot)
		{
			elements.Add(page.LabelElement(ctx.T("worlds.no_snapshot"), OnlineUiTextStyle.Muted, OnlineUiTheme.Muted));
		}
		else if (row.IsSelected)
		{
			elements.Add(page.LabelElement(ctx.T("worlds.continue_target"), OnlineUiTextStyle.Muted, OnlineUiTheme.Muted));
		}
		else
		{
			// A refusal is not swallowed: the library records it and the status line above shows
			// its reason on the next frame.
			elements.Add(page.ButtonElement(
				$"worlds.select.{row.World.WorldId}",
				ctx.T("worlds.select"),
				() => library.TrySelectWorld(row.World.WorldId, out _),
				width: 180f));
		}

		elements.Add(page.ButtonElement(
			$"worlds.backups.{row.World.WorldId}",
			ctx.T("worlds.show_backups"),
			() =>
			{
				state.BackupRowsWorldId = row.World.WorldId;
				state.BackupRows = [.. library.ListBackups(row.World.WorldId)];
				state.PendingRestoreFile = "";
			},
			width: 120f));

		page.Row([.. elements]);
		page.Space();
	}

	private static void BuildBackups(OnlineUiContext ctx, OnlineUiPageBuilder page, IWorldLibrary library)
	{
		var state = ctx.State;
		if (state.BackupRowsWorldId.Length == 0)
		{
			return;
		}

		page.Section(ctx.F("worlds.backups_section", DisplayNameOf(state, state.BackupRowsWorldId)));
		if (state.BackupRows.Count == 0)
		{
			page.Muted(ctx.T("worlds.backups_empty"));
			return;
		}

		foreach (var backup in state.BackupRows)
		{
			BuildBackupRow(ctx, page, library, backup);
		}
	}

	private static void BuildBackupRow(OnlineUiContext ctx, OnlineUiPageBuilder page, IWorldLibrary library, WorldBackup backup)
	{
		var state = ctx.State;
		page.Label(ctx.F("worlds.backup_row", ctx.T(KindKeyOf(backup.Kind)), LocalTime(backup.Stamp), SizeMb(backup)));

		if (string.Equals(state.PendingRestoreFile, backup.FileName, StringComparison.Ordinal))
		{
			page.Muted(ctx.F("worlds.restore_confirm", backup.FileName));
			page.Row(
				page.ButtonElement(
					$"worlds.restore_yes.{backup.FileName}",
					ctx.T("worlds.restore_confirm_yes"),
					() =>
					{
						state.PendingRestoreFile = "";
						// Armed here, executed by the frame pump: this code runs inside a model build.
						library.TryRequestRestore(
							state.BackupRowsWorldId,
							backup.FileName,
							ctx.WorldPresence?.IsInWorldOrGenerating ?? false,
							out _);
					},
					width: 150f),
				page.ButtonElement(
					$"worlds.restore_cancel.{backup.FileName}",
					ctx.T("worlds.restore_cancel"),
					() => state.PendingRestoreFile = "",
					width: 110f));
		}
		else
		{
			page.Button(
				$"worlds.restore.{backup.FileName}",
				ctx.T("worlds.restore"),
				() => state.PendingRestoreFile = backup.FileName,
				width: 120f);
		}

		page.Space();
	}

	/// <summary>Re-reads what the page shows. Cheap enough to run on demand, far too costly to run per frame.</summary>
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
