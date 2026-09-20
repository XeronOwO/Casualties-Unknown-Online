using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The world library (see <see cref="IWorldLibrary"/>): the repository's worlds and their
/// backup archives, the world the Continue entry opens, and the player-chosen restore.
///
/// It owns no snapshot logic of its own — the cut transaction of §5 and the promotion of §6
/// already do the writing; this type decides WHICH archive a player's click means, WHETHER
/// it may happen right now, and it reports what happened in words the page and the log both
/// use. Every refusal is recorded the same way: one <see cref="LastReport"/> plus one
/// warning line, so a click that did nothing is never silent.
///
/// It is an <see cref="ICuoService"/> because the restore has to run on a frame boundary:
/// the click that arms it happens inside an IMGUI draw callback (which runs more than once
/// per frame), and the promotion must happen exactly once, only while no world is loaded.
/// <paramref name="worldActive"/> is the adapter's answer to "is a world loaded or being
/// generated right now"; a composition without an adapter answers false, and the fact the
/// page passes at arm time still governs that half.
/// </summary>
public sealed class WorldLibraryService(
	WorldRepository? repository,
	IWorldSaveControl saves,
	ISessionControl session,
	ILogger<WorldLibraryService> log,
	IOptionsMonitor<SaveOptions>? options = null,
	Func<bool>? worldActive = null) : IWorldLibrary, ICuoService
{
	/// <summary>The save policy a composition with no options monitor runs with (the frozen defaults of §7).</summary>
	private static readonly SaveOptions FallbackOptions = new();

	private readonly IOptionsMonitor<SaveOptions> _options = options ?? new MutableOptionsMonitor<SaveOptions>(FallbackOptions);
	private readonly Func<bool> _worldActive = worldActive ?? (() => false);

	private ArmedRestore? _armed;
	private WorldMaintenanceReport? _last;
	private int _revision;

	/// <summary>One armed restore: the world and the archive the player picked.</summary>
	private readonly record struct ArmedRestore(string WorldId, WorldBackup Backup);

	public bool IsEnabled => repository is not null;

	public string? SelectedWorldId => saves.ContinueWorldId;

	public WorldMaintenanceReport? LastReport => _last;

	public bool HasArmedRestore => _armed is not null;

	public int Revision => _revision;

	public IReadOnlyList<WorldLibraryEntry> ListWorlds()
	{
		if (repository is not { } worlds)
		{
			return [];
		}

		// Read the Continue target rather than re-deriving it: the row this marks and the world
		// the native Load button opens are then the same answer by construction.
		var selected = SelectedWorldId;
		var rows = new List<WorldLibraryEntry>();
		foreach (var world in worlds.ListWorlds())
		{
			rows.Add(new WorldLibraryEntry(
				world,
				worlds.HasSnapshot(world.WorldId),
				worlds.ListBackups(world.WorldId).Count,
				string.Equals(world.WorldId, selected, StringComparison.Ordinal)));
		}

		return rows;
	}

	public IReadOnlyList<WorldBackup> ListBackups(string worldId) => repository?.ListBackups(worldId) ?? [];

	public bool TrySelectWorld(string worldId, out string? refusal)
	{
		if (repository is not { } worlds)
		{
			return Refuse(WorldMaintenanceKind.Select, worldId, null, "this build has no world repository", out refusal);
		}

		if (ExistingWorldDirectory(worlds, worldId) is null)
		{
			return Refuse(WorldMaintenanceKind.Select, worldId, null, $"world {worldId} does not exist", out refusal);
		}

		if (!worlds.HasSnapshot(worldId))
		{
			// The entry only opens a world that carries a snapshot, so selecting one without a
			// snapshot would leave the pointer naming a world the Load button skips — the silent
			// mismatch this refusal exists to prevent.
			return Refuse(WorldMaintenanceKind.Select, worldId, null,
				$"world {worldId} holds no snapshot yet, and the Continue entry only opens a world that does", out refusal);
		}

		if (!worlds.SetLastOpenedWorld(worldId))
		{
			return Refuse(WorldMaintenanceKind.Select, worldId, null, $"world {worldId} could not be recorded as the last opened", out refusal);
		}

		Report(WorldMaintenanceReport.Completed(WorldMaintenanceKind.Select, worldId, null, "the Continue entry now opens this world", []));
		refusal = null;
		return true;
	}

	public bool TryRequestRestore(string worldId, string backupFileName, bool worldActive, out string? refusal)
	{
		if (repository is not { } worlds)
		{
			return Refuse(WorldMaintenanceKind.Restore, worldId, null, "this build has no world repository", out refusal);
		}

		if (session.Role == SessionRole.Guest)
		{
			return Refuse(WorldMaintenanceKind.Restore, worldId, null,
				"a guest never writes a world archive; the host is the only save authority (decision 164)", out refusal);
		}

		if (worldActive || _worldActive())
		{
			return Refuse(WorldMaintenanceKind.Restore, worldId, null,
				"a world is loaded: leave it first, because a restore replaces the snapshot it is playing from", out refusal);
		}

		if (ExistingWorldDirectory(worlds, worldId) is null)
		{
			return Refuse(WorldMaintenanceKind.Restore, worldId, null, $"world {worldId} does not exist", out refusal);
		}

		var backup = FindBackup(worlds, worldId, backupFileName);
		if (backup is null)
		{
			return Refuse(WorldMaintenanceKind.Restore, worldId, null,
				$"'{backupFileName}' is not one of world {worldId}'s backups", out refusal);
		}

		// Last request wins, exactly like an armed cut: a page cannot arm two restores, and a
		// stale one running behind a newer choice would restore the wrong snapshot.
		_armed = new ArmedRestore(worldId, backup);
		log.LogInformation(
			"Restore armed: backup {Backup} over the live snapshot of world {WorldId}; it runs at the next frame boundary.",
			backup.FileName, worldId);
		refusal = null;
		return true;
	}

	// The ICuoService lifecycle, implemented explicitly: the interface exists so the plugin's
	// frame pump can drive this type, and nothing else in the tree needs those four names.
	//
	// The subscription is taken here rather than in a constructor because a primary constructor
	// has no body: this is the plugin's own "resolved, now wire yourself up" hook, and Dispose
	// takes it back down.
	void ICuoService.Initialize() => saves.CutReported += OnCutReported;

	void ICuoService.Start()
	{
	}

	/// <summary>The frame boundary: an armed restore runs here, and nowhere else.</summary>
	void ICuoService.Update() => Pump();

	void ICuoService.Stop()
	{
	}

	/// <summary>
	/// Releases the cut-report subscription. A singleton lives as long as the container, but the
	/// subscription is the one thing that would keep this type alive past the save layer it reads.
	/// </summary>
	void IDisposable.Dispose() => saves.CutReported -= OnCutReported;

	/// <summary>
	/// ONE attempt per request: a refusal is reported and the request is dropped, because the
	/// click that armed it is the only thing that may arm another. Retrying every frame would
	/// turn one refusal into a loop of log lines and disk probes.
	/// </summary>
	internal void Pump()
	{
		if (_armed is not { } armed)
		{
			return;
		}

		_armed = null;

		if (repository is not { } worlds)
		{
			Report(WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, armed.WorldId, armed.Backup, "this build has no world repository"));
			return;
		}

		if (session.Role == SessionRole.Guest)
		{
			Report(WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, armed.WorldId, armed.Backup,
				"a guest never writes a world archive; the host is the only save authority (decision 164)"));
			return;
		}

		if (_worldActive())
		{
			Report(WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, armed.WorldId, armed.Backup,
				"a world was loaded before the restore could run, and a restore never replaces the snapshot a running world plays from"));
			return;
		}

		// Re-read the list instead of trusting the arm: retention prunes archives and a player
		// can delete a file, and both can happen between the click and this frame boundary.
		var backup = FindBackup(worlds, armed.WorldId, armed.Backup.FileName);
		if (backup is null)
		{
			Report(WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, armed.WorldId, armed.Backup,
				$"backup {armed.Backup.FileName} is no longer in the world's backups folder"));
			return;
		}

		if (!worlds.TryHoldWorld(armed.WorldId, out var leaseRefusal))
		{
			Report(WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, armed.WorldId, backup,
				$"another CUO process is writing this world: {leaseRefusal}"));
			return;
		}

		try
		{
			Report(Restore(worlds, armed.WorldId, backup));
		}
		finally
		{
			worlds.ReleaseWorld(armed.WorldId);
		}
	}

	/// <summary>
	/// The restore itself, with the world's lease held: validate the archive, promote it, make
	/// the restored world the Continue target, then run the same retention pass every committed
	/// cut runs (§7).
	/// </summary>
	private WorldMaintenanceReport Restore(WorldRepository worlds, string worldId, WorldBackup backup)
	{
		// Validate BEFORE anything is replaced. A package whose manifest does not read, or whose
		// bytes no longer match it, must be refused while the working snapshot is still in live/:
		// promoting it first would destroy a good snapshot to install a broken one, and the player
		// would only find out at the next Continue.
		var opened = worlds.LoadBackup(worldId, backup, new WorldLoadOptions { VerifyChecksums = true });
		if (opened.Failed)
		{
			return WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, worldId, backup,
				$"backup {backup.FileName} could not be opened, so nothing was replaced: {opened.Summary}");
		}

		WorldBackupPromotion.Result promotion;
		try
		{
			promotion = worlds.PromoteBackup(worldId, backup, WorldPromotionTrigger.PlayerChoice);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, worldId, backup,
				$"the restore failed ({ex.Message}); the world folder is unchanged");
		}

		if (!promotion.Success)
		{
			return WorldMaintenanceReport.Refused(WorldMaintenanceKind.Restore, worldId, backup, promotion.Detail);
		}

		// Restoring a world is also choosing it: the native Load button then continues the world
		// the player just restored, not whatever was selected before.
		worlds.SetLastOpenedWorld(worldId);

		var account = new List<string>(promotion.Account)
		{
			$"backup {backup.FileName} is now the live snapshot of world {worldId}",
		};

		// The pre-restore archive this restore just wrote is the newest, so it survives; the
		// oldest archive is the one retention takes.
		var pruned = worlds.PruneBackups(worldId, Options.Retention);
		if (pruned.Deleted.Count > 0)
		{
			account.Add($"{pruned.Deleted.Count} older backup(s) were pruned (retention {Options.Retention})");
		}

		foreach (var failure in pruned.Failures)
		{
			account.Add($"a backup could not be pruned and stays on disk: {failure}");
		}

		return WorldMaintenanceReport.Completed(WorldMaintenanceKind.Restore, worldId, backup, "the chosen backup is the world's live snapshot", account);
	}

	/// <summary>
	/// Records a refusal and returns false, so every refusal takes the same two steps: the page
	/// gets its reason in <see cref="LastReport"/> and the log gets the same words.
	/// </summary>
	private bool Refuse(WorldMaintenanceKind kind, string worldId, WorldBackup? backup, string detail, out string? refusal)
	{
		Report(WorldMaintenanceReport.Refused(kind, worldId, backup, detail));
		refusal = detail;
		return false;
	}

	private void Report(WorldMaintenanceReport report)
	{
		_last = report;

		// Every resolved action invalidates a cached listing: a selection moves the index pointer,
		// and a restore rewrites live/, adds an archive and may prune. A refusal bumps it too —
		// one extra reload is cheaper than a page that misses a change.
		_revision++;
		var describe = report.Describe();
		if (report.Succeeded)
		{
			log.LogInformation("{Report}", describe);
		}
		else
		{
			log.LogWarning("{Report}", describe);
		}

		foreach (var line in report.Account)
		{
			log.LogInformation("World maintenance: {Line}", line);
		}
	}

	/// <summary>
	/// A finished cut attempt of ANY trigger changed what this library lists — the archive set,
	/// the counters, the last-saved time — so a cached page has to reload. The save layer reports
	/// captured AND refused attempts; a refusal that wrote nothing costs one redundant reload.
	/// </summary>
	private void OnCutReported(WorldCutReport report) => _revision++;

	private SaveOptions Options => _options.CurrentValue;

	/// <summary>The world's folder, or null when the id is not one of ours or the folder is gone.</summary>
	private static string? ExistingWorldDirectory(WorldRepository worlds, string worldId) =>
		worlds.TryPathOfWorld(worldId) is { } directory && Directory.Exists(directory) ? directory : null;

	private static WorldBackup? FindBackup(WorldRepository worlds, string worldId, string fileName) =>
		worlds.ListBackups(worldId).FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.Ordinal));
}
