using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The retention pass every committed cut owes the archive's backup policy (§7): the
/// newest N archives stay, the oldest go, the newest is never pruned, and a prune that
/// cannot finish is reported without touching the world.
/// </summary>
public class WorldSaveRetentionTests
{
	private const ulong HostId = 1001UL;
	private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void CommittedCuts_KeepOnlyTheConfiguredNumberOfArchives()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-retention-keep", () => now, retention: 3);

		var newest = string.Empty;
		WorldCutReport? last = null;
		for (var cut = 0; cut < 5; cut++)
		{
			now = now.AddMinutes(1); // one stamp per cut, so the newest is unambiguous
			last = Cut(fixture);
			newest = fixture.Repository.Repository.ListBackups(fixture.WorldId)[0].FileName;
		}
		var kept = fixture.Repository.Repository.ListBackups(fixture.WorldId);
		Assert.Equal(3, kept.Count);
		Assert.Equal(newest, kept[0].FileName);
		Assert.True(Assert.IsType<WorldCutReport>(last).Captured);
		Assert.Contains("3 archive(s) kept, 1 pruned", last.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void RetentionOne_KeepsTheNewestArchive()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-retention-one", () => now, retention: 1);

		var newest = string.Empty;
		for (var cut = 0; cut < 3; cut++)
		{
			now = now.AddMinutes(1);
			Cut(fixture);
			newest = fixture.Repository.Repository.ListBackups(fixture.WorldId)[0].FileName;
		}

		var kept = Assert.Single(fixture.Repository.Repository.ListBackups(fixture.WorldId));
		Assert.Equal(newest, kept.FileName);
	}

	[Fact]
	public void Retention_IsReadAtTheCut_SoAConfigEditAppliesToTheNextOne()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-retention-hot", () => now, retention: 10);
		now = now.AddMinutes(1);
		Cut(fixture);
		now = now.AddMinutes(1);
		Cut(fixture);
		Assert.Equal(2, fixture.Repository.Repository.ListBackups(fixture.WorldId).Count);

		// The next cut reads the new policy (decision 25: the value is read at the
		// decision, not captured when the service was built).
		fixture.Options.Set(new SaveOptions { BackupRetentionCount = 2 });
		now = now.AddMinutes(1);
		Cut(fixture);

		Assert.Equal(2, fixture.Repository.Repository.ListBackups(fixture.WorldId).Count);
	}

	[Fact]
	public void APruneThatCannotFinish_IsReportedAndLeavesTheWorldIntact()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-retention-failure", () => now, retention: 2);
		now = now.AddMinutes(1);
		Cut(fixture);
		now = now.AddMinutes(1);
		Cut(fixture);

		// A backup another process still holds open: the pass must report it and stop
		// there, not fail the cut and not leave a half-pruned world behind.
		var archives = fixture.Repository.Repository.ListBackups(fixture.WorldId);
		var oldest = archives[archives.Count - 1];
		using (new FileStream(oldest.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
		{
			now = now.AddMinutes(1);
			var report = Cut(fixture);

			Assert.True(report.Captured);
			Assert.Contains("could not be pruned", report.Summary, StringComparison.Ordinal);
			Assert.True(File.Exists(oldest.FullPath));
		}

		// The world is written and readable; the archive the pass could not delete is
		// still there, which is the failure mode that loses nothing.
		Assert.True(fixture.Repository.Repository.HasSnapshot(fixture.WorldId));
		Assert.Equal(3, fixture.Repository.Repository.ListBackups(fixture.WorldId).Count);
	}

	private static WorldSaveFixture RunningWorld(string label, Func<DateTime> now, int retention)
	{
		var fixture = WorldSaveFixture.Create(label, utcNow: now, options: new SaveOptions { BackupRetentionCount = retention });
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		return fixture;
	}

	private static WorldCutReport Cut(WorldSaveFixture fixture)
	{
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out var refusal), refusal);
		return Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));
	}
}
