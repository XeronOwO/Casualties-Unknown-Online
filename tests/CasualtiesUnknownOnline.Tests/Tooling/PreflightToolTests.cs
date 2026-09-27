using Xunit;
using static CasualtiesUnknownOnline.Tests.Tooling.PreflightToolHarness;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// preflight.ps1's app-id cross-check: a set game-app-id is `present` only when every source that
/// exists — the install's own `steam_appid.txt` and the library's app manifest — confirms it. The
/// 2026-09-27 run launched an unrelated game because the row merely echoed the fact.
///
/// The fixture does not stage the whole install, so a green run is machine-dependent (the machine's
/// Sandboxie services and interactive desktop decide the other required rows). The blocking direction
/// is asserted instead: a non-present `steam` row always yields exit code 2.
/// </summary>
[Trait("Category", "Integration")]
public class PreflightToolTests
{
	[Fact]
	public void InstallAppId_MatchesTheFact_IsPresent()
	{
		using var fixture = Fixture.Create("4576510").WithInstallAppId("4576510");

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("present", row.State);
		Assert.Contains("steam_appid.txt", row.Detail);
		Assert.DoesNotContain("steam", BlockingIds(result.Output));
	}

	[Fact]
	public void InstallAppId_ContradictsTheFact_IsMissingAndBlocking()
	{
		using var fixture = Fixture.Create("410900").WithInstallAppId("4576510");

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("missing", row.State);
		Assert.Contains("410900", row.Detail);
		Assert.Contains("4576510", row.Detail);
		Assert.Equal(2, result.ExitCode);
		Assert.Contains("steam", BlockingIds(result.Output));
	}

	[Fact]
	public void AppManifest_ConfirmsTheInstall_IsPresent()
	{
		using var fixture = Fixture.Create("4576510").WithAppManifest("4576510", "Casualties: Unknown Demo", Fixture.InstallName);

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("present", row.State);
		Assert.Contains("app manifest", row.Detail);
		Assert.DoesNotContain("steam", BlockingIds(result.Output));
	}

	[Fact]
	public void AppManifest_NamesAnotherInstall_IsMissingAndBlocking()
	{
		using var fixture = Fixture.Create("410900").WithAppManifest("410900", "Forts", "Forts");

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("missing", row.State);
		Assert.Contains("Forts", row.Detail);
		Assert.Equal(2, result.ExitCode);
		Assert.Contains("steam", BlockingIds(result.Output));
	}

	[Fact]
	public void AppId_WithNoInstallSource_IsUnknownAndBlocking()
	{
		using var fixture = Fixture.Create("4576510");

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("unknown", row.State);
		Assert.Contains("cross-checked", row.Detail);
		Assert.Equal(2, result.ExitCode);
		Assert.Contains("steam", BlockingIds(result.Output));
	}

	[Fact]
	public void BothSources_Agree_IsPresent()
	{
		using var fixture = Fixture.Create("4576510")
			.WithInstallAppId("4576510")
			.WithAppManifest("4576510", "Casualties: Unknown Demo", Fixture.InstallName);

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("present", row.State);
		Assert.Contains("steam_appid.txt", row.Detail);
		Assert.Contains("app manifest", row.Detail);
		Assert.DoesNotContain("steam", BlockingIds(result.Output));
	}

	[Fact]
	public void BothSources_Disagree_IsMissingAndBlocking()
	{
		using var fixture = Fixture.Create("4576510")
			.WithInstallAppId("4576510")
			.WithAppManifest("4576510", "Casualties: Unknown Demo", "Forts");

		var result = Run(fixture);
		var row = ReadRow(result.Output, "steam");

		Assert.Equal("missing", row.State);
		Assert.Contains("Forts", row.Detail);
		Assert.Equal(2, result.ExitCode);
		Assert.Contains("steam", BlockingIds(result.Output));
	}
}
