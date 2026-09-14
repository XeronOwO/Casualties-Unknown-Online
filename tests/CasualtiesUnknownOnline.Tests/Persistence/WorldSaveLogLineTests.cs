using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S4 scope 3: ONE log line per save (cut) and per restore, carrying the world id,
/// the cut kind, the cut phase, the revision and the per-domain record counts — so
/// a mismatch between what a cut wrote and what a restore took is diagnosable from
/// the log alone, without a code change, a deploy and a reproduction.
///
/// The counts are asserted as a PAIR rather than per line: the cut and the restore
/// report the same domains in the same order (WorldSnapshotCounts), and that
/// comparability is the actual contract — a line that reports counts in its own
/// vocabulary would satisfy "logged" while defeating "diagnosable".
/// </summary>
public class WorldSaveLogLineTests
{
	private const ulong HostId = 1001UL;

	[Fact]
	public void Cut_WritesOneInformationLineWithTheKindPhaseRevisionAndCounts()
	{
		using var fixture = Quiet("log-line-cut");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 2), out _, out _));
		Assert.True(fixture.Kernel.TrySpawn(
			HostId,
			new ItemIdentity(100, "bag"),
			ItemLocation.Carried(new ActorId(HostId)),
			new CharacterItemMsg { InstanceId = 100, ItemId = "bag", Condition = 1f },
			out _,
			out _));
		var report = WorldSaveCaptureTests.MenuReturnCut(fixture, WorldSaveCaptureTests.Character(100, "bag"));
		Assert.True(report.Captured, report.Summary);

		var line = Assert.Single(fixture.Recorder.Messages(LogLevel.Information, "WorldCutWriter"));

		Assert.Contains("MenuReturn", line, StringComparison.Ordinal);
		Assert.Contains("mid-run cut taken at frame-end", line, StringComparison.Ordinal);
		Assert.Contains($"world {fixture.WorldId}", line, StringComparison.Ordinal);
		Assert.Contains("revision", line, StringComparison.Ordinal);
		Assert.Contains("layer 2", line, StringComparison.Ordinal);
		Assert.Contains("1 item row(s)", line, StringComparison.Ordinal);
		Assert.Contains("1 character(s)", line, StringComparison.Ordinal);
		Assert.Contains("native run fields absent", line, StringComparison.Ordinal);
	}

	[Fact]
	public void Restore_WritesOneInformationLineWithTheKindPhaseRevisionAndTheSameCounts()
	{
		using var fixture = Quiet("log-line-restore");
		var cutLine = CutLayerEnd(fixture);

		using var restarted = fixture.Restart("log-line-restore-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome));

		var line = Assert.Single(restarted.Recorder.Messages(LogLevel.Information, "WorldRestoreApplier"));

		Assert.Contains("layer-end cut taken at layer-boundary", line, StringComparison.Ordinal);
		Assert.Contains($"world {restarted.WorldId}", line, StringComparison.Ordinal);
		Assert.Contains("layer 1", line, StringComparison.Ordinal);
		Assert.Contains("from the live snapshot", line, StringComparison.Ordinal);

		// The line carries the attempt's account too, not only its counts: whichever
		// damage this composition reports is readable from the same line.
		Assert.Contains(outcome.Summary, line, StringComparison.Ordinal);

		// The comparability contract, as VALUES rather than labels: the domains the cut
		// reported come back in the restore's line with the same names and the same counts.
		// Asserting the label alone would pass for "7 world-block row(s)" against
		// "0 world-block row(s)" — i.e. against the pre-change code too.
		Assert.Equal(1, CountOf(line, "item row"));
		Assert.Equal(CountOf(cutLine, "item row"), CountOf(line, "item row"));
		Assert.Equal(1, CountOf(line, "character"));
		Assert.Equal(CountOf(cutLine, "character"), CountOf(line, "character"));
		Assert.Equal(CountOf(cutLine, "world-block row"), CountOf(line, "world-block row"));
		Assert.Equal(CountOf(cutLine, "transient row"), CountOf(line, "transient row"));
		Assert.Equal(CountOf(cutLine, "enemy row"), CountOf(line, "enemy row"));
	}

	[Fact]
	public void RestoreFromABackup_NamesTheBackupAndItsSourcePath()
	{
		using var fixture = Quiet("log-line-restore-backup");
		CutLayerEnd(fixture);
		var manifestPath = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.ManifestFileName);
		File.WriteAllText(manifestPath, "{ not json");

		using var restarted = fixture.Restart("log-line-restore-backup-restart");
		Assert.True(restarted.Service.TryContinue(out _));

		var line = Assert.Single(restarted.Recorder.Messages(LogLevel.Information, "WorldRestoreApplier"));

		Assert.Contains("from backup ", line, StringComparison.Ordinal);
		Assert.Contains(SaveArchiveFormat.BackupExtension, line, StringComparison.Ordinal);
	}

	[Fact]
	public void RefusedCut_IsLoggedAsARefusalAndWritesNoCommitLine()
	{
		// The other half of "observable": a cut that wrote nothing must be as visible as
		// one that wrote. The kernel holds no run baseline here, so the writer refuses —
		// and the log carries the refusal instead of a commit line that never happened.
		using var fixture = Quiet("log-line-refusal");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		var report = WorldSaveCaptureTests.MenuReturnCut(fixture, character: null);

		Assert.False(report.Captured);
		Assert.Contains("run baseline", report.Summary, StringComparison.Ordinal);
		Assert.NotEmpty(fixture.Recorder.Messages(LogLevel.Warning, "WorldCutWriter"));
		Assert.Empty(fixture.Recorder.Messages(LogLevel.Information, "WorldCutWriter"));
	}

	[Fact]
	public void LayerEndCut_LogsTheRowsTheArchiveHoldsNotTheOnesTheKernelStillHad()
	{
		// A layer-end cut DROPS its in-layer rows: the world-rooted item, the live enemy
		// and the fluid chunk all describe the layer being replaced, and the layer the
		// snapshot names is regenerated from the run baseline. A cut line that counted the
		// KERNEL's tables would therefore disagree with the restore line that reads the
		// archive it wrote — breaking the very comparison S4 scope 3 promises, in the line
		// that promises it. The tombstone is the control: it IS carried, so the enemy count
		// is one on both sides rather than zero.
		using var fixture = Quiet("log-line-layer-end-drops");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		Assert.True(fixture.Kernel.TrySpawn(
			HostId,
			new ItemIdentity(100, "bag"),
			ItemLocation.Carried(new ActorId(HostId)),
			new CharacterItemMsg { InstanceId = 100, ItemId = "bag", Condition = 1f },
			out _,
			out _));
		Assert.True(fixture.Kernel.TrySpawn(
			HostId,
			new ItemIdentity(101, "crate"),
			ItemLocation.World(3f, 4f),
			new CharacterItemMsg { InstanceId = 101, ItemId = "crate", Condition = 1f },
			out _,
			out _));
		var live = new EntityId(1UL, 7U, 0);
		var killed = new EntityId(1UL, 8U, 0);
		Assert.True(fixture.Kernel.TryUpsertEnemy(HostId, new EnemyState(live, "spider", 4f, false, false), out _, out _));
		Assert.True(fixture.Kernel.TryUpsertEnemy(HostId, new EnemyState(killed, "spider", 4f, false, false), out _, out _));
		Assert.True(fixture.Kernel.TryRemoveEnemy(HostId, killed, out _, out _));
		Assert.True(fixture.Kernel.TryUpdateFluidRegion(HostId, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));
		fixture.Characters.SaveHostCharacterData(WorldSaveCaptureTests.Character(100, "bag"));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var cutLine = Assert.Single(fixture.Recorder.Messages(LogLevel.Information, "WorldCutWriter"));
		using var restarted = fixture.Restart("log-line-layer-end-drops-restart");
		Assert.True(restarted.Service.TryContinue(out _));
		var restoreLine = Assert.Single(restarted.Recorder.Messages(LogLevel.Information, "WorldRestoreApplier"));

		Assert.Contains("1 item row(s)", cutLine, StringComparison.Ordinal);
		Assert.Contains("1 item row(s)", restoreLine, StringComparison.Ordinal);
		Assert.Contains("1 enemy row(s)", cutLine, StringComparison.Ordinal);
		Assert.Contains("1 enemy row(s)", restoreLine, StringComparison.Ordinal);
		Assert.Contains("0 fluid chunk(s)", cutLine, StringComparison.Ordinal);
		Assert.Contains("0 fluid chunk(s)", restoreLine, StringComparison.Ordinal);
	}

	// ---- helpers ----

	/// <summary>The number a count line reports for one domain, e.g. <c>"3 world-block row(s)"</c> → 3. Throws when the line does not report that domain at all, so a missing count can never be read as agreement.</summary>
	private static int CountOf(string line, string domain)
	{
		var match = Regex.Match(line, @"(\d+) " + Regex.Escape(domain) + @"\(s\)", RegexOptions.CultureInvariant);
		return match.Success
			? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
			: throw new InvalidOperationException($"the line does not report '{domain}': {line}");
	}

	/// <summary>A fixture whose loggers record what the save layer writes.</summary>
	private static WorldSaveFixture Quiet(string label) =>
		WorldSaveFixture.Create(label, loggerFactory: new RecordingLoggerFactory());

	/// <summary>
	/// One layer-end cut of the fixture's kernel, carrying a spawned (carried) item and
	/// the host's own character — the two domains whose counts the restore's line has to
	/// repeat. Returns the cut's own account line.
	/// </summary>
	private static string CutLayerEnd(WorldSaveFixture fixture)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		Assert.True(fixture.Kernel.TrySpawn(
			HostId,
			new ItemIdentity(100, "bag"),
			ItemLocation.Carried(new ActorId(HostId)),
			new CharacterItemMsg { InstanceId = 100, ItemId = "bag", Condition = 1f },
			out _,
			out _));
		fixture.Characters.SaveHostCharacterData(WorldSaveCaptureTests.Character(100, "bag"));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
		return Assert.Single(fixture.Recorder.Messages(LogLevel.Information, "WorldCutWriter"));
	}
}
