using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// The emitted fixture pair plus the two snapshots the CLI tests diff, taken
/// once through the real executable. A class fixture (not a static field)
/// because xUnit creates a new test-class instance per test: the snapshots must
/// exist before any test runs and must not depend on test order.
/// </summary>
public sealed class ContractToolCliFixture
{
	internal ContractFixtureAssemblies.FixtureSet Fixtures { get; } = ContractFixtureAssemblies.Build("cli");

	internal string PreviousSnapshot { get; }

	internal string CurrentSnapshot { get; }

	public ContractToolCliFixture()
	{
		PreviousSnapshot = Path.Combine(Fixtures.Directory, "cli-previous.json");
		CurrentSnapshot = Path.Combine(Fixtures.Directory, "cli-current.json");
		Assert.Equal(0, ContractToolProcess.Run("snapshot", "--assembly", Fixtures.PreviousGame, "--adapter", Fixtures.Adapter, "--out", PreviousSnapshot).ExitCode);
		Assert.Equal(0, ContractToolProcess.Run("snapshot", "--assembly", Fixtures.CurrentGame, "--adapter", Fixtures.Adapter, "--out", CurrentSnapshot).ExitCode);
	}
}
