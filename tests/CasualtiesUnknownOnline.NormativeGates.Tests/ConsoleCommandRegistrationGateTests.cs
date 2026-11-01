using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The console's verb groups are DISCOVERED, never listed by hand. The rule
/// (<c>docs/development/agent-reference.md</c>): for large families of similar registration code — commands,
/// handlers, providers, packets — prefer discoverable attribute + reflection registration over hard-coded linear
/// registration.
///
/// <para>
/// The verbs were always attribute-declared; what was hard-coded until 2026-11-02 was which groups exist: thirteen
/// constructors inside <c>CommandConsoleService</c>, plus the locals they were fed. The refactor moved the list into
/// the composition block, where a family is announced once. This gate is what keeps it there: a new
/// <c>new SomethingCommands(…)</c> inside the console, or a group that stops carrying the marker, fails here rather
/// than being rediscovered by a reviewer weeks later.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class ConsoleCommandRegistrationGateTests
{
	private const string ServiceFile = "src/CasualtiesUnknownOnline.Runtime/Session/Commands/CommandConsoleService.cs";
	private const string CompositionFile = "src/CasualtiesUnknownOnline.Runtime/Composition/ConsoleComposition.cs";
	private const string MarkerFile = "src/CasualtiesUnknownOnline.Runtime/Session/Commands/IConsoleCommandGroup.cs";

	/// <summary>Every group class is constructed by the container, so the console must not build one itself.</summary>
	private static readonly Regex HandBuilt = new(@"new\s+\w+Commands\s*\(", RegexOptions.Compiled);

	[Fact]
	public void TheConsole_ConstructsNoVerbGroupItself()
	{
		var source = RepositoryPaths.ReadText(ServiceFile);
		Assert.True(source.Length > 4000, $"{ServiceFile} is only {source.Length} character(s); the check would pass by checking nothing");

		var built = HandBuilt.Matches(source).Select(match => match.Value).ToList();
		Assert.True(
			built.Count == 0,
			$"{ServiceFile} constructs {built.Count} verb group(s) by hand ({string.Join(", ", built)}); a verb group is "
			+ "registered in ConsoleComposition and discovered through IConsoleCommandGroup, so a new family is one registration there and never an edit here.");
	}

	[Fact]
	public void EveryGroupInTheComposition_CarriesTheMarkerAndIsRegisteredOnce()
	{
		var composition = RepositoryPaths.ReadText(CompositionFile);
		var marker = RepositoryPaths.ReadText(MarkerFile);
		Assert.Contains("interface IConsoleCommandGroup", marker, StringComparison.Ordinal);

		// Two groups take the spectator surface, which a build without a game scene does not register, so
		// they are announced through factories rather than by type; both spellings count as announced.
		var registrations = Regex.Matches(composition, @"IConsoleCommandGroup,\s*(\w+Commands)")
			.Concat(Regex.Matches(composition, @"new\s+(\w+Commands)\s*\("))
			.Select(match => match.Groups[1].Value)
			.Distinct(StringComparer.Ordinal)
			.ToList();
		Assert.True(registrations.Count >= 12, $"only {registrations.Count} group(s) registered; the console has more than that");

		foreach (var group in registrations)
		{
			var file = $"src/CasualtiesUnknownOnline.Runtime/Session/Commands/{group}.cs";
			var source = RepositoryPaths.ReadText(file);
			Assert.Contains("IConsoleCommandGroup", source, StringComparison.Ordinal);
		}
	}
}
