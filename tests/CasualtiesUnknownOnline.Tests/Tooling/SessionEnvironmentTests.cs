using System.IO;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Tooling.SessionEnvironmentHarness;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The gate that keeps a run off the owner's playing machine: the install's BepInEx trees are classified
/// by their own marker DLLs, never by folder names, and a swap runs only when no game process is running —
/// a running game is left alone, process and folders both. The marker names are parameters so this
/// repository carries no machine's private layout.
///
/// <para>
/// <c>-GameProcessName powershell</c> stands in for a running game: the script's own PowerShell process
/// carries that name, so the "a game is up" branch is deterministic without a real game.
/// </para>
/// </summary>
public class SessionEnvironmentTests
{
	private const string NoSuchProcess = "cuo-session-environment-no-such-process";

	[Fact]
	public void Status_ReportsTheOwnerPlaying()
	{
		using var fixture = Fixture.WithTheOwnerPlaying();

		var run = Run(fixture, "-Mode", "status", "-GameProcessName", "powershell", "-PlayMarkerName", Fixture.OtherMarkerName);

		Assert.Equal(0, run.ExitCode);
		Assert.Contains("active=play", run.Output);
		Assert.Contains("game-running=true", run.Output);
		Assert.Contains("swap-needed=true", run.Output);
		Assert.Contains("launch=blocked", run.Output);
	}

	[Fact]
	public void Status_NamesNoTreeWithoutItsMarker()
	{
		using var fixture = Fixture.WithTheOwnerPlaying();

		var run = Run(fixture, "-Mode", "status", "-GameProcessName", NoSuchProcess);

		Assert.Equal(0, run.ExitCode);
		Assert.Contains("active=other", run.Output);
		Assert.Contains("launch=ok", run.Output);
	}

	[Fact]
	public void Status_LeavesTheTreesAlone()
	{
		using var fixture = Fixture.WithTheOwnerPlaying();

		Run(fixture, "-Mode", "status", "-GameProcessName", "powershell", "-PlayMarkerName", Fixture.OtherMarkerName);

		Assert.True(fixture.MarkerExists("BepInEx", Fixture.OtherMarkerPath), "the owner's tree is still the active one");
		Assert.True(fixture.MarkerExists("BepInEx-cuo", Fixture.CuoMarkerPath), "CUO's tree was not touched");
		Assert.False(fixture.TreeExists("BepInEx-play"), "a report never parks a tree");
	}

	[Fact]
	public void EnsureCuo_RefusesWhileAGameProcessIsRunning()
	{
		using var fixture = Fixture.WithTheOwnerPlaying();

		var run = Run(fixture, "-Mode", "ensure-cuo", "-GameProcessName", "powershell", "-PlayMarkerName", Fixture.OtherMarkerName);

		Assert.Equal(2, run.ExitCode);
		Assert.Contains("action=refused", run.Output);
		Assert.Contains("reason=game-running", run.Output);
		Assert.True(fixture.MarkerExists("BepInEx", Fixture.OtherMarkerPath), "the owner's tree is exactly where it was");
		Assert.False(fixture.TreeExists("BepInEx-play"), "nothing was parked while the game runs");
	}

	[Fact]
	public void EnsureCuo_SwapsWhenNoGameProcessIsRunning()
	{
		using var fixture = Fixture.WithTheOwnerPlaying();

		var run = Run(fixture, "-Mode", "ensure-cuo", "-GameProcessName", NoSuchProcess, "-PlayMarkerName", Fixture.OtherMarkerName);

		Assert.Equal(0, run.ExitCode);
		Assert.Contains("action=swapped", run.Output);
		Assert.True(fixture.MarkerExists("BepInEx", Fixture.CuoMarkerPath), "CUO's tree is the active one now");
		Assert.True(fixture.MarkerExists("BepInEx-play", Fixture.OtherMarkerPath), "the parked tree keeps its marker");
		Assert.False(fixture.TreeExists("BepInEx-cuo"), "the renamed CUO tree did not leave a copy behind");
	}

	[Fact]
	public void EnsureCuo_SwapsWithoutThePlayMarkerConfigured()
	{
		using var fixture = Fixture.WithTheOwnerPlaying();

		var run = Run(fixture, "-Mode", "ensure-cuo", "-GameProcessName", NoSuchProcess);

		Assert.Equal(0, run.ExitCode);
		Assert.Contains("action=swapped", run.Output);
		Assert.True(fixture.MarkerExists("BepInEx", Fixture.CuoMarkerPath), "CUO's tree is the active one now");
	}

	[Fact]
	public void EnsureCuo_IsANoOpWhenCuoIsAlreadyActive()
	{
		using var fixture = Fixture.WithCuoActive();

		var run = Run(fixture, "-Mode", "ensure-cuo", "-GameProcessName", NoSuchProcess);

		Assert.Equal(0, run.ExitCode);
		Assert.Contains("action=already-cuo", run.Output);
		Assert.True(fixture.MarkerExists("BepInEx-play", Fixture.OtherMarkerPath), "the parked tree stays parked");
	}

	[Fact]
	public void EnsureCuo_RefusesWhenNoCuoTreeExists()
	{
		using var fixture = Fixture.WithoutACuoTree();

		var run = Run(fixture, "-Mode", "ensure-cuo", "-GameProcessName", NoSuchProcess);

		Assert.Equal(2, run.ExitCode);
		Assert.Contains("action=refused", run.Output);
		Assert.Contains("reason=cuo-tree-count=0", run.Output);
		Assert.True(fixture.MarkerExists("BepInEx", Fixture.OtherMarkerPath), "a refusal moves nothing");
	}

	[Fact]
	public void Usage_WhenTheGameDirDoesNotResolve()
	{
		var missing = Path.Combine(Path.GetTempPath(), "cuo-session-environment-tests-missing");

		var run = Run(missing, "-Mode", "status");

		Assert.Equal(64, run.ExitCode);
		Assert.Contains("-GameDir does not resolve", run.Output);
	}
}
