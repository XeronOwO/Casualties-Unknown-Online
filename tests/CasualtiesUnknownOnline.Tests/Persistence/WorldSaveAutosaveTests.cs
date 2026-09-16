using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The interval autosave at the service (S4 scope 4): the pump's tick is the only
/// thing that arms it, the frame-end seam takes it as an <c>auto</c> cut with its own
/// archive name, and a trigger the player asked for is never superseded by it.
/// </summary>
public class WorldSaveAutosaveTests
{
	private const ulong HostId = 1001UL;
	private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void BeforeTheIntervalElapsed_TheTickArmsNothing()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-early", () => now);

		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		Assert.False(fixture.Service.HasArmedCut);

		now = now.AddMinutes(9);
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));
	}

	[Fact]
	public void TheIntervalAutosave_IsAnAutoCutWithItsOwnArchiveName()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-auto", () => now);
		now = now.AddMinutes(10);

		Assert.True(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		Assert.True(fixture.Service.HasArmedCut);

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		Assert.True(report.Captured);
		Assert.Equal(WorldCutReason.AutoInterval, report.Reason);

		// The kind is what names the archive (§2): an interval cut is not a mid-run cut
		// with a different reason — its backup file carries the auto kind.
		var manifest = WorldSaveCaptureTests.LiveManifest(fixture);
		Assert.Equal("auto", manifest.GetProperty("kind").GetString());
		Assert.Equal("auto-interval", manifest.GetProperty("saveReason").GetString());
		var backup = Assert.Single(fixture.Repository.Repository.ListBackups(fixture.WorldId));
		Assert.StartsWith("auto-", backup.FileName, StringComparison.Ordinal);
		Assert.Equal(WorldCutKind.Auto, backup.Kind);

		// An autosave is a system trigger: it reaches the log, never the player's console
		// as a notification of its own.
		Assert.False(report.PlayerInitiated);
	}

	[Fact]
	public void InTheMainMenu_TheTickArmsNothing()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-menu", () => now);
		now = now.AddHours(1);

		// The host owns a world folder, but no world is LOADED: cutting here would write
		// the run that was left behind, every interval, for as long as the menu is open.
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: false));
		Assert.False(fixture.Service.HasArmedCut);
	}

	[Fact]
	public void WithThePlayersOwnCutArmed_TheTickKeepsThatTrigger()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-player", () => now);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		now = now.AddMinutes(30);

		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));

		// The seam takes the player's cut: the report the console answers is the /save,
		// not an autosave that silently took its place.
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));
		Assert.Equal(WorldCutReason.Command, report.Reason);
		Assert.True(report.PlayerInitiated);
	}

	[Fact]
	public void AutosaveDisabled_TheTickArmsNothing()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-off", () => now);
		now = now.AddHours(3);

		// Read at the decision, so the config edit hot-reloads into the very next tick
		// (decision 25) — and back on again.
		fixture.Options.Set(new SaveOptions { AutosaveEnabled = false });
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));

		fixture.Options.Set(new SaveOptions { AutosaveEnabled = true });
		Assert.True(fixture.Service.TryArmIntervalAutosave(inWorld: true));
	}

	[Fact]
	public void WithNoWorldOfItsOwn_TheTickArmsNothing()
	{
		var now = Noon;
		using var fixture = WorldSaveFixture.Create("save-autosave-noworld", utcNow: () => now);
		now = now.AddHours(1);
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));

		// A tutorial entry gets no archive at all, so it has nothing to autosave either.
		Assert.False(fixture.Service.TryBeginRun(isTutorial: true));
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));
	}

	[Fact]
	public void OnAGuest_TheTickArmsNothing()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-guest", () => now);
		fixture.Session.Role = SessionRole.Guest;
		now = now.AddHours(1);

		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		Assert.False(fixture.Service.HasArmedCut);
	}

	[Fact]
	public void ACommittedCut_RestartsTheInterval()
	{
		var now = Noon;
		using var fixture = RunningWorld("save-autosave-restart", () => now);
		now = now.AddMinutes(10);
		Assert.True(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		// The world was just written: the next interval counts from that cut.
		now = now.AddMinutes(9);
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		now = now.AddMinutes(1);
		Assert.True(fixture.Service.TryArmIntervalAutosave(inWorld: true));
	}

	[Fact]
	public void ARefusedAutosave_DoesNotArmAgainOnTheNextFrame()
	{
		// No kernel run baseline: the writer refuses the cut. The world was NOT written, but the
		// attempt WAS made — and the tick must not arm another one on the very next frame, or a
		// world that cannot be written would run a full snapshot transaction every frame for as
		// long as its cause lasts (a read-only folder, a vanished drive).
		var now = Noon;
		using var fixture = WorldSaveFixture.Create("save-autosave-refused", utcNow: () => now);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		now = now.AddMinutes(10);

		Assert.True(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));
		Assert.False(report.Captured);

		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));

		// ... and the window it restarted is a full one, not a per-frame one.
		now = now.AddMinutes(9);
		Assert.False(fixture.Service.TryArmIntervalAutosave(inWorld: true));
		now = now.AddMinutes(1);
		Assert.True(fixture.Service.TryArmIntervalAutosave(inWorld: true));
	}

	[Fact]
	public void ContinuingAWorld_RestartsTheIntervalAtTheClick()
	{
		var now = Noon;
		var repository = SaveTestRepository.Create("save-autosave-continue");
		string worldId;
		using (var first = WorldSaveFixture.Create("save-autosave-continue", utcNow: () => now, repository: repository))
		{
			Assert.True(first.Service.TryBeginRun(isTutorial: false));
			Assert.True(first.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
			Assert.True(first.Service.TryRequestCut(WorldCutReason.Command, out _));
			Assert.IsType<WorldCutReport>(first.Service.TryCaptureArmedCut(null, frame: 0));
			worldId = first.WorldId;
		}

		// A later session over the same world folder, a long time after that cut.
		now = now.AddHours(6);
		using var resumed = WorldSaveFixture.Create("save-autosave-continue", utcNow: () => now, repository: repository);
		Assert.True(resumed.Service.TryContinue(out var outcome), outcome.Summary);
		Assert.Equal(worldId, resumed.Service.CurrentWorldId);

		// The click opened a fresh window: the restored world's first autosave is one
		// interval after the click, not one interval after the cut of the session before.
		Assert.False(resumed.Service.TryArmIntervalAutosave(inWorld: true));
		now = now.AddMinutes(10);
		Assert.True(resumed.Service.TryArmIntervalAutosave(inWorld: true));
	}

	/// <summary>A fixture whose clock the test moves, over a world the host has begun and is playing.</summary>
	private static WorldSaveFixture RunningWorld(string label, Func<DateTime> now)
	{
		var fixture = WorldSaveFixture.Create(label, utcNow: now);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		return fixture;
	}
}
