using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Configuration;

/// <summary>
/// The archive policy knobs (S4.4, decision 25): the frozen defaults of §7 and the
/// clamp that a hand-edited config file cannot bypass.
/// </summary>
public class SaveOptionsTests
{
	[Fact]
	public void Defaults_AreTheFrozenPolicy()
	{
		var options = new SaveOptions();

		Assert.True(options.AutosaveEnabled);
		Assert.Equal(TimeSpan.FromMinutes(10), options.AutosaveInterval);
		Assert.Equal(10, options.Retention);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-30, 1)]
	[InlineData(1, 1)]
	[InlineData(45, 45)]
	[InlineData(1440, 1440)]
	[InlineData(100000, 1440)]
	public void TheInterval_IsClampedToTheLegalRange(int configured, int expectedMinutes)
	{
		var options = new SaveOptions { AutosaveIntervalMinutes = configured };

		Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), options.AutosaveInterval);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(1, 1)]
	[InlineData(25, 25)]
	[InlineData(1000, 1000)]
	[InlineData(99999, 1000)]
	public void TheRetention_IsClampedToTheLegalRange(int configured, int expected)
	{
		var options = new SaveOptions { BackupRetentionCount = configured };

		Assert.Equal(expected, options.Retention);
	}
}
