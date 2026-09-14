using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The Continue click's account as a REPORT (S4 scope 2): the disposition, the
/// one-line summary and the itemized detail, raised where a player-facing surface
/// can render it. §6 forbids silent loss and names the surface's content — the
/// count per domain, the reason, the affected content id and the backup a load
/// fell back to — so this suite pins the account at the seam the console
/// subscribes to, not the log text (which <see cref="WorldSaveLogLineTests"/>
/// pins separately).
///
/// The three dispositions are the whole point of the type: an applied attempt, a
/// refused one (nothing was applied), and an abandoned one (applied, but no world
/// generation will consume it) are different things a player must be able to tell
/// apart — the last one is otherwise experienced as "the button did nothing".
/// </summary>
public class WorldRestoreReportTests
{
	private const ulong HostId = 1001UL;
	private const ulong GuestId = 2002UL;

	[Fact]
	public void CleanContinue_ReportsAppliedWithNothingLost()
	{
		// "Clean" is a claim about the WHOLE account, so the fixture has to be one that
		// CAN be clean: a native reader on both sides (so the run-field row is written
		// and applied) and a character carrying its native fields. A composition without
		// either produces a restore that NAMES the gap — and a suite that ignored that
		// would call a damaged restore clean.
		var cutNative = new FakeNativeWorldFacts();
		cutNative.SeedRunFields(1f, 1f, 42.5f, new SaveRecipeUnlockRow { Index = 0, MadeBefore = true, IntValue = 0 });
		using var fixture = WorldSaveFixture.Create("restore-report-clean", nativeWorldFacts: cutNative);
		CutLayerEnd(fixture, CharacterWithNativeFields());

		using var restarted = WorldSaveFixture.Create(
			"restore-report-clean-restart",
			repository: fixture.Repository,
			nativeWorldFacts: new FakeNativeWorldFacts());
		var reports = Subscribe(restarted);
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var report = Assert.Single(reports);
		Assert.Equal(WorldRestoreReport.Disposition.Applied, report.Result);
		Assert.Equal(restarted.WorldId, report.WorldId);
		Assert.True(report.Clean, report.Summary);
		Assert.Empty(report.Details);
		Assert.Contains("restored from the live snapshot", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void TwoPresentPlayersSharingAKey_ReportTheRefusedCharacterInTheItemizedAccount()
	{
		// The S4.1 rule reaches the surface here: two players whose transport-scoped keys
		// collide get the character of NEITHER of them (decision 177), and that loss is a
		// line in the account — a summary that only says "damage" would leave the player
		// unable to tell a refused claim from a skipped item.
		using var fixture = WorldSaveFixture.Create("restore-report-ambiguous", ipDirect: true, displayName: "Bob");
		CutLayerEnd(fixture);

		using var restarted = fixture.Restart("restore-report-ambiguous-restart", ipDirect: true, displayName: "Bob");
		restarted.Session.AddMember(GuestId, "Bob");
		var reports = Subscribe(restarted);
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var report = Assert.Single(reports);
		Assert.False(report.Clean);
		Assert.Contains(report.Details, line => line.Contains("name-bob", StringComparison.Ordinal) && line.Contains("more than one player present", StringComparison.Ordinal));
		Assert.Contains("name-bob", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void BackupFallback_NamesTheBackupInBothTheSummaryAndTheItemizedAccount()
	{
		// §6's "fell back to backup X" is a player-facing message, not a path: the
		// summary names the snapshot the restore read, and the itemized account names
		// the archive file the load opened instead of the damaged live folder.
		using var fixture = WorldSaveFixture.Create("restore-report-fallback");
		CutLayerEnd(fixture);
		var manifestPath = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.ManifestFileName);
		File.WriteAllText(manifestPath, "{ not json");

		using var restarted = fixture.Restart("restore-report-fallback-restart");
		var reports = Subscribe(restarted);
		Assert.True(restarted.Service.TryContinue(out _));

		var report = Assert.Single(reports);
		Assert.Equal(WorldRestoreReport.Disposition.Applied, report.Result);
		Assert.False(report.Clean);
		Assert.Contains("backup ", report.Summary, StringComparison.Ordinal);
		Assert.Contains(SaveArchiveFormat.BackupExtension, report.Summary, StringComparison.Ordinal);
		Assert.Contains(report.Details, line => line.Contains("was opened instead", StringComparison.Ordinal) && line.Contains(SaveArchiveFormat.BackupExtension, StringComparison.Ordinal));
	}

	[Fact]
	public void ContinueWithoutAWorld_ReportsARefusal()
	{
		using var fixture = WorldSaveFixture.Create("restore-report-refused");
		var reports = Subscribe(fixture);
		Assert.False(fixture.Service.TryContinue(out _));

		var report = Assert.Single(reports);
		Assert.Equal(WorldRestoreReport.Disposition.Refused, report.Result);
		Assert.Contains("no CUO world exists to continue", report.Summary, StringComparison.Ordinal);
		Assert.Empty(report.Details);
	}

	[Fact]
	public void AbandonedAttempt_ReportsASecondTimeWithTheAbandonment()
	{
		// The restore was applied at the click and the run still cannot start: the
		// account has to say so, otherwise the player's last word from CUO is
		// "restored" while the run never begins.
		using var fixture = WorldSaveFixture.Create("restore-report-abandoned");
		CutLayerEnd(fixture);

		using var restarted = fixture.Restart("restore-report-abandoned-restart");
		var reports = Subscribe(restarted);
		Assert.True(restarted.Service.TryContinue(out _));

		restarted.Service.AbandonRestore("the restore published no run baseline, so no world generation will consume it");

		Assert.Equal(
			[WorldRestoreReport.Disposition.Applied, WorldRestoreReport.Disposition.Abandoned],
			reports.ConvertAll(report => report.Result));
		Assert.Contains("no run baseline", reports[1].Summary, StringComparison.Ordinal);
		Assert.Equal(restarted.WorldId, reports[1].WorldId);
	}

	// ---- helpers ----

	private static List<WorldRestoreReport> Subscribe(WorldSaveFixture fixture)
	{
		var reports = new List<WorldRestoreReport>();
		fixture.Service.RestoreReported += reports.Add;
		return reports;
	}

	/// <summary>
	/// A character whose native fields are all WRITABLE: the happiness window is the
	/// game's own ten values and the wound window's details are the native four, which
	/// is the only shape whose restore is not reported as a gap.
	/// </summary>
	private static CharacterDataMsg CharacterWithNativeFields()
	{
		var character = WorldSaveCaptureTests.Character(100, "bag");
		character.NativeFields = new CharacterNativeFieldsMsg
		{
			LastHappiness = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f],
			CaloriesConsumed = 4100,
			CharacterInfo = [171, 24, 8123, 3],
		};
		return character;
	}

	/// <summary>One layer-end cut of the fixture's kernel, carrying <paramref name="character"/> as the host's own (so a claim can be refused).</summary>
	private static void CutLayerEnd(WorldSaveFixture fixture, CharacterDataMsg? character = null)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(character ?? WorldSaveCaptureTests.Character(100, "bag"));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}
}
