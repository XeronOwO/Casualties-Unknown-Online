using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The runbook's two commands, exercised as real processes against the emitted
/// fixture pair: the tool the update-day flow calls is the built executable, so
/// the exit codes (0 ran, 1 broke a contract under --fail-on-broken, 2 usage,
/// 3 unreadable input) and the artifacts it writes are pinned here rather than
/// only through its library.
/// </summary>
[Trait("Category", "Integration")]
public class ContractToolCliTests(ContractToolCliFixture fixture) : IClassFixture<ContractToolCliFixture>
{
	private readonly ContractToolCliFixture _fixture = fixture;

	[Fact]
	public void Diff_WithFailOnBroken_ExitsOneAndWritesBothArtifacts()
	{
		var report = Path.Combine(_fixture.Fixtures.Directory, "cli-report.md");
		var diff = Path.Combine(_fixture.Fixtures.Directory, "cli-diff.json");
		var run = ContractToolProcess.Run(
			"diff", "--previous", _fixture.PreviousSnapshot, "--current", _fixture.CurrentSnapshot,
			"--out", report, "--json", diff, "--fail-on-broken");

		Assert.Equal(1, run.ExitCode);
		Assert.Contains("Harmony target ambiguous", File.ReadAllText(report), StringComparison.Ordinal);
		using var document = JsonDocument.Parse(File.ReadAllText(diff));
		Assert.True(document.RootElement.GetProperty("summary").GetProperty("brokenContracts").GetBoolean());
	}

	[Fact]
	public void DiffWithoutFailOnBroken_ExitsZero()
	{
		var run = ContractToolProcess.Run(
			"diff", "--previous", _fixture.PreviousSnapshot, "--current", _fixture.CurrentSnapshot,
			"--out", Path.Combine(_fixture.Fixtures.Directory, "cli-report-2.md"));

		Assert.Equal(0, run.ExitCode);
	}

	[Fact]
	public void Help_PrintsBothCommands()
	{
		var run = ContractToolProcess.Run("--help");

		Assert.Equal(0, run.ExitCode);
		Assert.Contains("snapshot", run.StandardOutput, StringComparison.Ordinal);
		Assert.Contains("diff", run.StandardOutput, StringComparison.Ordinal);
	}

	[Fact]
	public void UnknownOption_IsRefusedWithTheUsageExitCode()
	{
		var run = ContractToolProcess.Run("snapshot", "--nope");

		Assert.Equal(2, run.ExitCode);
		Assert.Contains("unknown option --nope", run.StandardError, StringComparison.Ordinal);
		Assert.Contains("Usage:", run.StandardError, StringComparison.Ordinal);
	}

	[Fact]
	public void UnreadableInput_ExitsThree()
	{
		var run = ContractToolProcess.Run("diff", "--previous", Path.Combine(_fixture.Fixtures.Directory, "absent.json"), "--current", _fixture.PreviousSnapshot);

		Assert.Equal(3, run.ExitCode);
		Assert.Contains("not found", run.StandardError, StringComparison.Ordinal);
	}

	[Fact]
	public void MalformedSnapshot_ExitsThree()
	{
		var truncated = Path.Combine(_fixture.Fixtures.Directory, "cli-truncated.json");
		var json = File.ReadAllText(_fixture.PreviousSnapshot);
		File.WriteAllText(truncated, json.Substring(0, json.Length / 2));

		var run = ContractToolProcess.Run("diff", "--previous", truncated, "--current", _fixture.CurrentSnapshot);

		Assert.Equal(3, run.ExitCode);
	}

	[Fact]
	public void OutputDirectory_IsCreated()
	{
		// The runbook writes into artifacts/contract/, which a clean checkout does not have.
		var output = Path.Combine(_fixture.Fixtures.Directory, "fresh", "nested", "snapshot.json");
		var run = ContractToolProcess.Run("snapshot", "--assembly", _fixture.Fixtures.PreviousGame, "--adapter", _fixture.Fixtures.Adapter, "--out", output);

		Assert.Equal(0, run.ExitCode);
		Assert.True(File.Exists(output), $"{output} was not written");
	}
}
