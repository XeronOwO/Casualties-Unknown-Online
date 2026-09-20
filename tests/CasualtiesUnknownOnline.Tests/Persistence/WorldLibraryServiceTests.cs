using System;
using System.IO;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The world library (decision 198): what the Worlds page lists, which world the Continue
/// entry opens, and the player-chosen restore that replaces a world's live snapshot with a
/// chosen archive.
///
/// The service is composed over the SAME real repository and the SAME real
/// <c>WorldSaveService</c> the page and the native Load button use. That is the point of
/// <see cref="IWorldLibrary.SelectedWorldId"/>: the page marks the world the entry resolves,
/// and a stubbed entry would only prove the stub agrees with itself.
/// </summary>
public sealed class WorldLibraryServiceTests
{
	[Fact]
	public void ListWorlds_MarksExactlyTheWorldTheContinueEntryOpens()
	{
		using var library = Library.Create("library-rows");
		Cut(library.World, "first");
		var second = library.AddWorld("Second World");
		Cut(library.World, second, "second");

		// Nothing has been selected yet, so the entry's own fallback decides and the page must
		// mark exactly that row — never a row of its own choosing.
		var marked = Assert.Single(library.Service.ListWorlds().Where(row => row.IsSelected));
		Assert.Equal(library.Service.SelectedWorldId, marked.World.WorldId);

		Assert.True(library.Service.TrySelectWorld(second, out var refusal), refusal);
		marked = Assert.Single(library.Service.ListWorlds().Where(row => row.IsSelected));
		Assert.Equal(second, marked.World.WorldId);
		Assert.Equal(second, library.Save.Service.ContinueWorldId);
	}

	[Fact]
	public void ListWorlds_ReportsTheSnapshotAndBackupFactsThePickerNeeds()
	{
		using var library = Library.Create("library-facts");
		var empty = library.AddWorld("Never Played");
		Cut(library.World, "first");
		Cut(library.World, "second", minute: 1);

		var rows = library.Service.ListWorlds();
		var played = Assert.Single(rows.Where(row => string.Equals(row.World.WorldId, library.World.WorldId, StringComparison.Ordinal)));
		Assert.True(played.HasSnapshot);
		Assert.Equal(2, played.BackupCount);

		var never = Assert.Single(rows.Where(row => string.Equals(row.World.WorldId, empty, StringComparison.Ordinal)));
		Assert.False(never.HasSnapshot);
		Assert.Equal(0, never.BackupCount);
		Assert.Empty(library.Service.ListBackups(empty));
		Assert.Empty(library.Service.ListBackups("w-20260101-ffff"));
	}

	[Fact]
	public void ListBackups_IsNewestFirstAndIgnoresForeignFiles()
	{
		using var library = Library.Create("library-backup-order");
		Cut(library.World, "oldest");
		Cut(library.World, "middle", minute: 1);
		Cut(library.World, "newest", minute: 2);
		File.WriteAllText(Path.Combine(library.World.Workspace.BackupsDirectory(library.World.WorldId), "notes.txt"), "not a backup");

		var backups = library.Service.ListBackups(library.World.WorldId);
		Assert.Equal(3, backups.Count);
		Assert.True(string.CompareOrdinal(backups[0].Stamp, backups[1].Stamp) > 0, "the list must be newest first");
		Assert.True(string.CompareOrdinal(backups[1].Stamp, backups[2].Stamp) > 0, "the list must be newest first");
	}

	[Fact]
	public void SelectWorld_RefusesAWorldWithNoSnapshotAndRecordsWhy()
	{
		using var library = Library.Create("library-select-empty");
		Cut(library.World, "first");
		var empty = library.AddWorld("Never Played");

		Assert.False(library.Service.TrySelectWorld(empty, out var refusal));
		Assert.Contains("no snapshot", refusal, StringComparison.Ordinal);

		// The refusal is not silent: the page shows LastReport, and the selection did not move.
		var report = Assert.IsType<WorldMaintenanceReport>(library.Service.LastReport);
		Assert.False(report.Succeeded);
		Assert.Equal(WorldMaintenanceKind.Select, report.Kind);
		Assert.NotEqual(empty, library.Service.SelectedWorldId);
	}

	[Fact]
	public void SelectWorld_RefusesAWorldThatIsNotThere()
	{
		using var library = Library.Create("library-select-missing");
		Cut(library.World, "first");

		Assert.False(library.Service.TrySelectWorld("w-20260101-ffff", out var refusal));
		Assert.Contains("does not exist", refusal, StringComparison.Ordinal);
		Assert.False(library.Service.LastReport!.Succeeded);
	}

	[Fact]
	public void SelectedWorld_IsRememberedAcrossARestart()
	{
		using var library = Library.Create("library-selection-persists");
		Cut(library.World, "first");
		var second = library.AddWorld("Second World");
		Cut(library.World, second, "second");
		Assert.True(library.Service.TrySelectWorld(second, out var refusal), refusal);

		// A fresh repository over the same root: the selection lives in index.json (§3.1), so a
		// restarted game opens the world the player picked, not one the page remembered.
		var reopened = new WorldRepository(
			library.World.Workspace.Root,
			NullLogger<WorldRepository>.Instance,
			new SaveArchiveWriter(NullLogger<SaveArchiveWriter>.Instance),
			new SaveArchiveReader(NullLogger<SaveArchiveReader>.Instance));
		Assert.Equal(second, reopened.LastOpenedWorldId);
	}

	[Fact]
	public void Restore_ReplacesTheLiveSnapshotArchivesWhatItReplacedAndSelectsTheWorld()
	{
		using var library = Library.Create("library-restore");
		var world = library.World.WorldId;
		Cut(library.World, "kept");
		Cut(library.World, "replaced", minute: 1);
		var before = library.World.Repository.ListBackups(world);
		Assert.Equal(2, before.Count);

		// Somewhere else is the Continue target, so the restore's own selection is visible.
		var other = library.AddWorld("Other World");
		Cut(library.World, other, "other");
		Assert.True(library.Service.TrySelectWorld(other, out var selected), selected);

		Assert.True(library.Service.TryRequestRestore(world, before[1].FileName, worldActive: false, out var refusal), refusal);
		Assert.True(library.Service.HasArmedRestore);
		library.Service.Pump();

		var report = Assert.IsType<WorldMaintenanceReport>(library.Service.LastReport);
		Assert.True(report.Succeeded, report.Detail);
		Assert.False(library.Service.HasArmedRestore);
		Assert.Contains("kept", LiveRunText(library), StringComparison.Ordinal);

		// What was replaced is a loadable archive of its own (the player can step back again)...
		var preRestore = Assert.Single(library.World.Repository.ListBackups(world)
			.Where(backup => before.All(original => !string.Equals(original.FileName, backup.FileName, StringComparison.Ordinal))));
		var opened = library.World.Repository.LoadBackup(world, preRestore, new WorldLoadOptions { VerifyChecksums = true });
		Assert.True(opened.Loaded, opened.Summary);
		Assert.Equal(SaveArchiveFormat.CutReasonName(WorldCutReason.PreRestoreBackup), opened.Content!.Manifest.SaveReason);
		var run = Assert.Single(opened.Content.Files.Where(file => string.Equals(file.Path, SaveTestData.RunFileName, StringComparison.Ordinal)));
		Assert.Contains("replaced", Encoding.UTF8.GetString(run.Bytes), StringComparison.Ordinal);

		// ...and no folder of the replaced state is left behind: a player-chosen restore is not a
		// refusal, and `damaged-` folders are never deleted by anything.
		Assert.Empty(Directory.GetDirectories(library.World.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.False(Directory.Exists(library.World.Workspace.PreviousDirectory(world)));

		// Restoring a world is also choosing it: the Load button continues what was restored.
		Assert.Equal(world, library.Service.SelectedWorldId);
		Assert.Equal(world, library.Save.Service.ContinueWorldId);
	}

	[Fact]
	public void Restore_IsRefusedWhileAWorldIsLoadedAtTheClickAndAgainAtTheFrameBoundary()
	{
		using var library = Library.Create("library-restore-in-world");
		var world = library.World.WorldId;
		Cut(library.World, "kept");
		Cut(library.World, "latest", minute: 1);
		var chosen = library.World.Repository.ListBackups(world)[1];

		Assert.False(library.Service.TryRequestRestore(world, chosen.FileName, worldActive: true, out var refusal));
		Assert.Contains("a world is loaded", refusal, StringComparison.Ordinal);
		Assert.False(library.Service.HasArmedRestore);

		// A request armed at the menu is judged AGAIN at the boundary: a world can be entered in
		// the frames between the click and the pump.
		Assert.True(library.Service.TryRequestRestore(world, chosen.FileName, worldActive: false, out var armed), armed);
		library.WorldActive = true;
		library.Service.Pump();

		Assert.False(library.Service.LastReport!.Succeeded);
		Assert.Contains("was loaded before the restore", library.Service.LastReport.Detail, StringComparison.Ordinal);
		Assert.Contains("latest", LiveRunText(library), StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_IsRefusedForAGuest()
	{
		using var library = Library.Create("library-restore-guest");
		var world = library.World.WorldId;
		Cut(library.World, "kept");
		Cut(library.World, "latest", minute: 1);
		var chosen = library.World.Repository.ListBackups(world)[0];
		library.Save.Session.Role = SessionRole.Guest;

		Assert.False(library.Service.TryRequestRestore(world, chosen.FileName, worldActive: false, out var refusal));
		Assert.Contains("guest", refusal, StringComparison.Ordinal);
		Assert.False(library.Service.HasArmedRestore);
	}

	[Fact]
	public void Restore_RefusesAnArchiveThatIsNotOneOfTheWorlds()
	{
		using var library = Library.Create("library-restore-foreign");
		var world = library.World.WorldId;
		Cut(library.World, "latest");

		Assert.False(library.Service.TryRequestRestore(world, "mid-run-20260101-000000.cuoz", worldActive: false, out var refusal));
		Assert.Contains("is not one of world", refusal, StringComparison.Ordinal);
		Assert.False(library.Service.TryRequestRestore("w-20260101-ffff", "mid-run-20260101-000000.cuoz", worldActive: false, out _));
		Assert.False(library.Service.HasArmedRestore);
	}

	[Fact]
	public void Restore_RefusesAnArchiveThatCannotBeOpenedAndLeavesTheLiveSnapshotAlone()
	{
		using var library = Library.Create("library-restore-broken");
		var world = library.World.WorldId;
		Cut(library.World, "kept");
		Cut(library.World, "latest", minute: 1);
		var chosen = library.World.Repository.ListBackups(world)[0];
		var archivesBefore = library.World.Repository.ListBackups(world).Select(backup => backup.FileName).ToList();
		File.WriteAllBytes(chosen.FullPath, SaveTestData.Bytes("this is not a zip archive"));

		Assert.True(library.Service.TryRequestRestore(world, chosen.FileName, worldActive: false, out var refusal), refusal);
		library.Service.Pump();

		// Validated BEFORE the swap: the world still holds the snapshot it had, and the refusal
		// says why instead of leaving a broken live snapshot for the next Continue to discover.
		Assert.False(library.Service.LastReport!.Succeeded);
		Assert.Contains("could not be opened", library.Service.LastReport.Detail, StringComparison.Ordinal);
		Assert.Contains("latest", LiveRunText(library), StringComparison.Ordinal);
		Assert.Equal(archivesBefore, library.World.Repository.ListBackups(world).Select(backup => backup.FileName).ToList());
		Assert.Empty(Directory.GetDirectories(library.World.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
	}

	[Fact]
	public void Restore_IsRefusedWhileAnotherInstanceIsWritingTheWorld()
	{
		using var library = Library.Create("library-restore-leased");
		var world = library.World.WorldId;
		Cut(library.World, "kept");
		Cut(library.World, "latest", minute: 1);
		var chosen = library.World.Repository.ListBackups(world)[0];
		SaveArchiveJson.WriteFile(
			WorldLease.PathOf(library.World.WorldDirectory),
			new WorldLeaseEntry("OTHER-MACHINE:4242", SaveArchiveFormat.FormatUtc(library.World.Now)));

		Assert.True(library.Service.TryRequestRestore(world, chosen.FileName, worldActive: false, out var refusal), refusal);
		library.Service.Pump();

		Assert.False(library.Service.LastReport!.Succeeded);
		Assert.Contains("OTHER-MACHINE:4242", library.Service.LastReport.Detail, StringComparison.Ordinal);
		Assert.Contains("latest", LiveRunText(library), StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_RunsTheConfiguredRetentionAfterPromoting()
	{
		using var library = Library.Create("library-restore-retention", new SaveOptions { BackupRetentionCount = 2 });
		var world = library.World.WorldId;

		// Every cut is stamped BEFORE the promotion's own clock, which is what production does too:
		// the pre-restore archive the restore writes is then the newest archive of the world.
		Cut(library.World, "oldest", minute: -3);
		Cut(library.World, "middle", minute: -2);
		Cut(library.World, "latest", minute: -1);
		var backups = library.World.Repository.ListBackups(world);
		Assert.Equal(3, backups.Count);

		// The middle archive is the one restored, so the oldest cut and the archive it was restored
		// FROM are both candidates for retention afterwards.
		Assert.True(library.Service.TryRequestRestore(world, backups[1].FileName, worldActive: false, out var refusal), refusal);
		library.Service.Pump();

		var report = Assert.IsType<WorldMaintenanceReport>(library.Service.LastReport);
		Assert.True(report.Succeeded, report.Detail);
		Assert.Contains(report.Account, line => line.Contains("were pruned", StringComparison.Ordinal));

		// WHICH archives survive, not merely how many: the newest cut and the pre-restore copy of
		// the state this restore replaced are kept (the newest archive is structurally undeletable),
		// while the oldest cut AND the archive that was just restored from are the ones taken.
		var kept = library.World.Repository.ListBackups(world);
		Assert.Equal(2, kept.Count);
		Assert.Contains(kept, backup => string.Equals(backup.FileName, backups[0].FileName, StringComparison.Ordinal));
		Assert.DoesNotContain(kept, backup => string.Equals(backup.FileName, backups[1].FileName, StringComparison.Ordinal));
		Assert.DoesNotContain(kept, backup => string.Equals(backup.FileName, backups[2].FileName, StringComparison.Ordinal));
		var preRestore = Assert.Single(kept.Where(backup => !string.Equals(backup.FileName, backups[0].FileName, StringComparison.Ordinal)));
		var opened = library.World.Repository.LoadBackup(world, preRestore, new WorldLoadOptions { VerifyChecksums = true });
		Assert.True(opened.Loaded, opened.Summary);
		Assert.Equal(SaveArchiveFormat.CutReasonName(WorldCutReason.PreRestoreBackup), opened.Content!.Manifest.SaveReason);
		Assert.Contains("middle", LiveRunText(library), StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_WorksWhenTheWorldHasNoLiveSnapshot()
	{
		using var library = Library.Create("library-restore-no-live");
		var world = library.World.WorldId;
		Cut(library.World, "kept");
		Cut(library.World, "latest", minute: 1);
		var chosen = library.World.Repository.ListBackups(world)[1];

		// A world folder can hold backups with no live snapshot (an interrupted earlier attempt, a
		// folder a player cleaned up). There is then nothing to archive or to preserve, and the
		// restore must still work and say so instead of refusing or inventing an evidence folder.
		Directory.Delete(library.World.LiveDirectory, recursive: true);

		Assert.True(library.Service.TryRequestRestore(world, chosen.FileName, worldActive: false, out var refusal), refusal);
		library.Service.Pump();

		var report = Assert.IsType<WorldMaintenanceReport>(library.Service.LastReport);
		Assert.True(report.Succeeded, report.Detail);
		Assert.Contains("no live snapshot", report.Account[0], StringComparison.Ordinal);
		Assert.Contains("kept", LiveRunText(library), StringComparison.Ordinal);
		Assert.Empty(Directory.GetDirectories(library.World.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.False(Directory.Exists(library.World.Workspace.PreviousDirectory(world)));
	}

	[Fact]
	public void Revision_AdvancesOnACommittedCutAndOnAManagementActionSoThePageCannotGoStale()
	{
		using var library = Library.Create("library-revision");
		Cut(library.World, "first");
		var before = library.Service.Revision;

		// A real cut through the save service — the same path a /save, the interval autosave and a
		// layer boundary take — is what a page listing backups must react to.
		Assert.True(library.Save.Service.TryBeginRun(isTutorial: false));
		Assert.True(library.Save.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		Assert.True(library.Save.Service.TryRequestCut(WorldCutReason.Command, out var cutRefusal), cutRefusal);
		Assert.True(Assert.IsType<WorldCutReport>(library.Save.Service.TryCaptureArmedCut(null, frame: 0)).Captured);
		Assert.True(library.Service.Revision > before, "a committed cut must invalidate the cached rows");

		var afterCut = library.Service.Revision;
		Assert.True(library.Service.TrySelectWorld(library.World.WorldId, out var selectRefusal), selectRefusal);
		Assert.True(library.Service.Revision > afterCut, "a management action must invalidate the cached rows");
	}

	private const ulong HostId = 1001UL;

	private static string LiveRunText(Library library) =>
		File.ReadAllText(Path.Combine(library.World.LiveDirectory, SaveTestData.RunFileName));

	private static void Cut(SaveTestRepository world, string marker, int minute = 0) =>
		Cut(world, world.WorldId, marker, minute);

	private static void Cut(SaveTestRepository world, string worldId, string marker, int minute = 0) =>
		Assert.True(
			world.Repository.WriteSnapshot(
				worldId,
				SaveTestData.Request(worldId, WorldCutKind.MidRun, world.Now.AddMinutes(minute), SaveTestData.RunPayload(marker))).Success,
			$"the cut for '{marker}' in world {worldId} did not commit");

	/// <summary>The world library over one real repository, with a real save control and a scripted session.</summary>
	private sealed class Library : IDisposable
	{
		private Library(SaveTestRepository world, WorldSaveFixture save)
		{
			World = world;
			Save = save;
			Service = new WorldLibraryService(
				world.Repository,
				save.Service,
				save.Session,
				NullLogger<WorldLibraryService>.Instance,
				save.Options,
				worldActive: () => WorldActive);
			// The plugin's own lifecycle call, for the same reason: it is where the service takes
			// its cut-report subscription, and a fixture that skipped it would test a service the
			// production composition never builds.
			((ICuoService)Service).Initialize();
		}

		internal SaveTestRepository World { get; }

		internal WorldSaveFixture Save { get; }

		internal WorldLibraryService Service { get; }

		/// <summary>The adapter's answer the page reads, scripted per test.</summary>
		internal bool WorldActive { get; set; }

		internal static Library Create(string label, SaveOptions? options = null)
		{
			var world = SaveTestRepository.Create(label);
			var save = WorldSaveFixture.Create(label, repository: world, options: options, utcNow: () => world.Now);
			return new Library(world, save);
		}

		/// <summary>A second world in the same root, with no snapshot of its own yet.</summary>
		internal string AddWorld(string displayName)
		{
			var created = World.Repository.CreateWorld(displayName);
			Assert.True(created.Success, created.Failure);
			return created.WorldId;
		}

		public void Dispose()
		{
			((IDisposable)Service).Dispose();
			Save.Dispose();
		}
	}
}
