using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The decode-level refusal's recovery (§6, S3 scope 7, S4's acceptance row 6): a snapshot
/// whose manifest reads but whose payload the decode refuses gets the fallback an unreadable
/// manifest has always had — the newest backup that DOES decode — and the snapshot it
/// replaces is preserved as evidence instead of being deleted by the next cut.
/// </summary>
public class WorldSaveRecoveryTests
{
	private const ulong HostId = 1001UL;
	private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void ADecodeRefusal_IsRecoveredFromTheNewestBackupThatDecodes()
	{
		var now = Noon;
		var repository = SaveTestRepository.Create("save-recovery");
		var worldId = FirstCut(repository, () => now);

		// The live snapshot is replaced by one the DECODE refuses — no readable run baseline,
		// while its manifest reads perfectly well. That is exactly the refusal the reader's own
		// fallback cannot see: by the time the payload is decoded, the load has returned.
		now = now.AddMinutes(1);
		var refusedWrite = repository.Repository.WriteSnapshot(
			worldId, SaveTestData.Request(worldId, WorldCutKind.MidRun, now, SaveTestData.Meta(), SaveTestData.RunPayload("refused")));
		Assert.True(refusedWrite.Success, refusedWrite.Detail);
		Assert.Equal(2, repository.Repository.ListBackups(worldId).Count);

		now = now.AddMinutes(1);
		using var resumed = WorldSaveFixture.Create("save-recovery", utcNow: () => now, repository: repository);

		var started = resumed.Service.TryContinue(out var outcome);
		Assert.True(started, outcome.Summary);
		Assert.Equal(worldId, resumed.Service.CurrentWorldId);

		// The world folder this test writes into is the RUN's, not the repository's seed world.
		var worldDirectory = repository.Repository.PathOfWorld(worldId);
		var liveDirectory = Path.Combine(worldDirectory, SaveArchiveFormat.LiveFolderName);

		// The refused snapshot is preserved, untouched, under its own folder ...
		var preserved = Directory.GetDirectories(worldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*");
		var preservedFolder = Assert.Single(preserved);
		Assert.True(File.Exists(Path.Combine(preservedFolder, SaveArchiveFormat.ManifestFileName)));
		Assert.Contains("refused", File.ReadAllText(Path.Combine(preservedFolder, SaveArchiveFormat.RunFileName)), StringComparison.Ordinal);

		// ... the decodable backup is the live snapshot now (its run baseline is the real one) ...
		Assert.DoesNotContain("refused", File.ReadAllText(Path.Combine(liveDirectory, SaveArchiveFormat.RunFileName)), StringComparison.Ordinal);

		// ... and the snapshot that was about to be replaced is archived as the pre-restore copy.
		// (Found by its manifest rather than by position: the archive is stamped with the
		// repository's clock, and this fixture deliberately runs its two clocks apart.)
		var preRestore = repository.Repository.ListBackups(worldId)
			.Select(backup => repository.Repository.LoadBackup(worldId, backup, new WorldLoadOptions()))
			.Where(load => load.Content is not null
				&& string.Equals(load.Content.Manifest.SaveReason, SaveArchiveFormat.CutReasonName(WorldCutReason.PreRestoreBackup), StringComparison.Ordinal))
			.ToList();
		Assert.Single(preRestore);

		// The account the player reads names both halves.
		Assert.Contains("preserved at " + SaveArchiveFormat.DamagedFolderPrefix, outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("was promoted to the live snapshot", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("pre-restore backup", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void WhenNoBackupDecodes_TheContinueIsRefused_AndTheWorldIsLeftAlone()
	{
		var now = Noon;
		var live = SaveTestRepository.Create("save-recovery-refused");
		Assert.True(live.Repository.WriteSnapshot(
			live.WorldId, live.Request(WorldCutKind.MidRun, now, SaveTestData.RunPayload("broken-1"))).Success);
		now = now.AddMinutes(1);
		Assert.True(live.Repository.WriteSnapshot(
			live.WorldId, live.Request(WorldCutKind.MidRun, now, SaveTestData.RunPayload("broken-2"))).Success);

		using var service = WorldSaveFixture.Create("save-recovery-refused", repository: live, utcNow: () => now);

		Assert.False(service.Service.TryContinue(out var outcome));
		Assert.Contains("no readable run baseline", outcome.Summary, StringComparison.Ordinal);

		// Nothing was promoted and nothing was moved: the world is exactly as it was found.
		Assert.Empty(Directory.GetDirectories(live.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.False(Directory.Exists(live.Workspace.StagingDirectory(live.WorldId)));
		Assert.Contains("broken-2", File.ReadAllText(Path.Combine(live.LiveDirectory, SaveTestData.RunFileName)), StringComparison.Ordinal);
	}

	[Fact]
	public void Promotion_MovesTheRefusedSnapshotAsideAndPutsTheBackupInPlace()
	{
		var fixture = SaveTestRepository.Create("save-promotion");
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now, SaveTestData.RunPayload("good"))).Success);
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now.AddMinutes(1), SaveTestData.RunPayload("refused"))).Success);

		var archives = fixture.Repository.ListBackups(fixture.WorldId);
		Assert.Equal(2, archives.Count);
		var result = fixture.Repository.PromoteBackup(fixture.WorldId, archives[1], WorldPromotionTrigger.RefusedSnapshot);

		Assert.True(result.Success, result.Detail);
		Assert.Contains("good", File.ReadAllText(Path.Combine(fixture.LiveDirectory, SaveTestData.RunFileName)), StringComparison.Ordinal);

		var preservedFolder = Assert.Single(Directory.GetDirectories(fixture.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.Contains("refused", File.ReadAllText(Path.Combine(preservedFolder, SaveTestData.RunFileName)), StringComparison.Ordinal);
		Assert.Contains(result.Account, line => line.Contains("pre-restore backup", StringComparison.Ordinal));
		Assert.False(Directory.Exists(fixture.Workspace.StagingDirectory(fixture.WorldId)));
	}

	[Fact]
	public void AnArchiveThatCannotBeUnpacked_PromotesNothing()
	{
		var fixture = SaveTestRepository.Create("save-promotion-broken");
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now, SaveTestData.RunPayload("live"))).Success);
		var backup = Assert.Single(fixture.Repository.ListBackups(fixture.WorldId));
		File.WriteAllBytes(backup.FullPath, SaveTestData.Bytes("this is not a zip archive"));

		var result = fixture.Repository.PromoteBackup(fixture.WorldId, backup, WorldPromotionTrigger.RefusedSnapshot);

		Assert.False(result.Success);
		Assert.Contains("could not be unpacked", result.Detail, StringComparison.Ordinal);
		Assert.Contains("live", File.ReadAllText(Path.Combine(fixture.LiveDirectory, SaveTestData.RunFileName)), StringComparison.Ordinal);
		Assert.Empty(Directory.GetDirectories(fixture.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.False(Directory.Exists(fixture.Workspace.StagingDirectory(fixture.WorldId)));
	}

	[Fact]
	public void AnotherWorldsBackup_IsRefused()
	{
		var fixture = SaveTestRepository.Create("save-promotion-foreign");
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now, SaveTestData.RunPayload("live"))).Success);

		var other = SaveTestRepository.Create("save-promotion-foreign-other");
		Assert.True(other.Repository.WriteSnapshot(
			other.WorldId, other.Request(WorldCutKind.MidRun, other.Now, SaveTestData.RunPayload("other"))).Success);
		var foreign = Assert.Single(other.Repository.ListBackups(other.WorldId));

		Assert.False(fixture.Repository.PromoteBackup(fixture.WorldId, foreign, WorldPromotionTrigger.RefusedSnapshot).Success);
		Assert.False(fixture.Repository.LoadBackup(fixture.WorldId, foreign, new WorldLoadOptions()).Loaded);
	}

	[Fact]
	public void PlayerChosenPromotion_ArchivesTheReplacedSnapshotAndLeavesNoEvidenceFolder()
	{
		var fixture = SaveTestRepository.Create("save-promotion-choice");
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now, SaveTestData.RunPayload("kept"))).Success);
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now.AddMinutes(1), SaveTestData.RunPayload("replaced"))).Success);

		var archives = fixture.Repository.ListBackups(fixture.WorldId);
		Assert.Equal(2, archives.Count);
		var result = fixture.Repository.PromoteBackup(fixture.WorldId, archives[1], WorldPromotionTrigger.PlayerChoice);

		Assert.True(result.Success, result.Detail);
		Assert.Contains("kept", File.ReadAllText(Path.Combine(fixture.LiveDirectory, SaveTestData.RunFileName)), StringComparison.Ordinal);

		// The replaced state is a loadable archive — that IS its copy — so neither the
		// rejected-snapshot evidence folder nor the transient `.previous` is left behind.
		Assert.Empty(Directory.GetDirectories(fixture.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.False(Directory.Exists(Path.Combine(fixture.WorldDirectory, SaveArchiveFormat.PreviousFolderName)));
		Assert.Contains(result.Account, line => line.Contains("pre-restore backup", StringComparison.Ordinal));
		Assert.Contains(result.Account, line => line.Contains("its folder was removed", StringComparison.Ordinal));
		Assert.False(Directory.Exists(fixture.Workspace.StagingDirectory(fixture.WorldId)));

		var preRestore = Assert.Single(fixture.Repository.ListBackups(fixture.WorldId)
			.Where(backup => !string.Equals(backup.FileName, archives[0].FileName, StringComparison.Ordinal)
				&& !string.Equals(backup.FileName, archives[1].FileName, StringComparison.Ordinal)));
		var opened = fixture.Repository.LoadBackup(fixture.WorldId, preRestore, new WorldLoadOptions { VerifyChecksums = true });
		Assert.True(opened.Loaded, opened.Summary);
		Assert.Equal(SaveArchiveFormat.CutReasonName(WorldCutReason.PreRestoreBackup), opened.Content!.Manifest.SaveReason);
	}

	[Fact]
	public void PlayerChosenPromotion_KeepsTheReplacedFolderWhenNoPreRestoreArchiveCouldBeWritten()
	{
		var fixture = SaveTestRepository.Create("save-promotion-choice-evidence");
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now, SaveTestData.RunPayload("kept"))).Success);
		Assert.True(fixture.Repository.WriteSnapshot(
			fixture.WorldId, fixture.Request(WorldCutKind.MidRun, fixture.Now.AddMinutes(1), SaveTestData.RunPayload("replaced"))).Success);

		// A manifest that does not read is what makes the pre-restore archive impossible: the
		// replaced state can then be copied nowhere else, so its folder IS preserved.
		File.WriteAllText(Path.Combine(fixture.LiveDirectory, SaveArchiveFormat.ManifestFileName), "{ not json");

		var archives = fixture.Repository.ListBackups(fixture.WorldId);
		var result = fixture.Repository.PromoteBackup(fixture.WorldId, archives[1], WorldPromotionTrigger.PlayerChoice);

		Assert.True(result.Success, result.Detail);
		var preserved = Assert.Single(Directory.GetDirectories(fixture.WorldDirectory, SaveArchiveFormat.DamagedFolderPrefix + "*"));
		Assert.Contains("replaced", File.ReadAllText(Path.Combine(preserved, SaveTestData.RunFileName)), StringComparison.Ordinal);
		Assert.Contains(result.Account, line => line.Contains("no readable manifest", StringComparison.Ordinal));
	}

	/// <summary>One real cut through the service, so the world holds a snapshot the production decoder accepts.</summary>
	private static string FirstCut(SaveTestRepository repository, Func<DateTime> now)
	{
		using var fixture = WorldSaveFixture.Create("save-recovery", utcNow: now, repository: repository);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		Assert.True(Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0)).Captured);
		return fixture.WorldId;
	}
}
