using System;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The interval autosave's clock (S4 scope 4, §7): the window opens when a world is
/// entered, restarts on every COMMITTED cut of any trigger, and never runs without a
/// world. The pure policy behind it, without a service or a disk.
/// </summary>
public class WorldAutosaveIntervalTests
{
	private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
	private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

	[Fact]
	public void NoWorldEntered_NeverDue()
	{
		var interval = new WorldAutosaveInterval();

		Assert.False(interval.Armed);
		Assert.False(interval.IsDue(Noon.AddDays(1), enabled: true, TenMinutes));
	}

	[Fact]
	public void EnteringAWorld_OpensTheWindow()
	{
		var interval = new WorldAutosaveInterval();
		interval.Restart(Noon);

		Assert.True(interval.Armed);
		Assert.False(interval.IsDue(Noon.AddMinutes(9), enabled: true, TenMinutes));
		Assert.True(interval.IsDue(Noon.AddMinutes(10), enabled: true, TenMinutes));
	}

	[Fact]
	public void CommittedCut_RestartsTheWindow()
	{
		var interval = new WorldAutosaveInterval();
		interval.Restart(Noon);
		interval.NoteCutTaken(Noon.AddMinutes(9));

		Assert.False(interval.IsDue(Noon.AddMinutes(15), enabled: true, TenMinutes));
		Assert.True(interval.IsDue(Noon.AddMinutes(19), enabled: true, TenMinutes));
	}

	[Fact]
	public void Disabled_NeverDue()
	{
		var interval = new WorldAutosaveInterval();
		interval.Restart(Noon);

		Assert.False(interval.IsDue(Noon.AddHours(5), enabled: false, TenMinutes));
	}

	[Fact]
	public void NonPositiveInterval_NeverDue()
	{
		var interval = new WorldAutosaveInterval();
		interval.Restart(Noon);

		Assert.False(interval.IsDue(Noon.AddHours(5), enabled: true, TimeSpan.Zero));
		Assert.False(interval.IsDue(Noon.AddHours(5), enabled: true, TimeSpan.FromMinutes(-10)));
	}

	[Fact]
	public void StandingDown_ClosesTheWindow()
	{
		var interval = new WorldAutosaveInterval();
		interval.Restart(Noon);
		interval.StandDown();

		Assert.False(interval.Armed);
		Assert.False(interval.IsDue(Noon.AddHours(5), enabled: true, TenMinutes));
	}
}
