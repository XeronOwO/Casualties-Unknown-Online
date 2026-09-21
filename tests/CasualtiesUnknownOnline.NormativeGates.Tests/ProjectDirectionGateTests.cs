using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The project-dependency direction gate. The Runtime reaches the kernel through
/// the Application layer (Runtime -> Application -> GameState) and no project
/// above the kernel references GameState directly; the declared table is
/// <see cref="ProjectDirectionPolicy.AllowedReferences"/> and adding a reference
/// means declaring it there in the same change.
///
/// <para>
/// The synthetic cases exist because the tree half alone cannot fail: a checker
/// that scans nothing, or one whose rules were narrowed by accident, would still
/// report the tree green. They pin the rule in both directions, and the tree
/// half pins that the graph it reads is the real one (a census floor over the
/// solution, plus every declared project still present).
/// </para>
/// </summary>
public class ProjectDirectionGateTests
{
	[Fact]
	public void Tree_FollowsTheDeclaredProjectDirection()
	{
		var graph = ProjectDirectionPolicy.LoadSolutionGraph(RepositoryPaths.Root);

		// Census floor: the solution lists 13 projects (10 source + 2 test + 1 tool),
		// and the declared layers contribute at least the references below. A graph
		// that shrank means the loader broke, not that the direction got better.
		Assert.True(graph.Count >= 13, $"the gate read only {graph.Count} project(s) from the solution");
		Assert.True(graph.Values.Sum(references => references.Count) >= 10,
			$"the gate read only {graph.Values.Sum(references => references.Count)} project reference(s)");
		foreach (var declared in ProjectDirectionPolicy.AllowedReferences.Keys)
		{
			Assert.True(graph.ContainsKey(declared), $"the layer table declares {declared}, which the solution no longer contains");
		}

		// Classification census: the table and the consumer list must together
		// cover every project the solution lists, so a new project cannot be
		// exempt by omission.
		var unclassified = graph.Keys
			.Where(project => !ProjectDirectionPolicy.AllowedReferences.ContainsKey(project)
				&& !ProjectDirectionPolicy.ConsumerProjects.Contains(project, StringComparer.Ordinal))
			.ToList();
		Assert.True(unclassified.Count == 0, $"unclassified project(s): {string.Join(", ", unclassified)}");

		var violations = ProjectDirectionPolicy.Violations(graph);
		Assert.True(violations.Count == 0,
			"project direction gate failed" + Environment.NewLine + string.Join(Environment.NewLine, violations));
	}

	[Fact]
	public void GameStateReferencingUpward_IsRefused()
	{
		var violations = ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.GameState", ["CasualtiesUnknownOnline.Application"]),
			("CasualtiesUnknownOnline.Application", ["CasualtiesUnknownOnline.GameState", "CasualtiesUnknownOnline.Protocol"]),
			("CasualtiesUnknownOnline.Runtime", ["CasualtiesUnknownOnline.Application"])));

		Assert.Contains(violations, violation => violation.Contains("GameState references CasualtiesUnknownOnline.Application", StringComparison.Ordinal));
	}

	[Fact]
	public void RuntimeReachingGameStateWithoutTheLayer_IsRefused()
	{
		var violations = ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.GameState", []),
			("CasualtiesUnknownOnline.Runtime", ["CasualtiesUnknownOnline.GameState"])));

		Assert.Contains(violations, violation => violation.Contains("Runtime references CasualtiesUnknownOnline.GameState", StringComparison.Ordinal));
		Assert.Contains(violations, violation => violation.Contains("Runtime must reference CasualtiesUnknownOnline.Application", StringComparison.Ordinal));
	}

	[Fact]
	public void ApplicationReferencingUpward_IsRefused()
	{
		var violations = ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.Application", ["CasualtiesUnknownOnline.Runtime"])));

		Assert.Contains(violations, violation => violation.Contains("Application references CasualtiesUnknownOnline.Runtime", StringComparison.Ordinal));
	}

	[Fact]
	public void ApplicationReferencingItsLowerLayers_IsAccepted()
	{
		Assert.Empty(ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.Application", ["CasualtiesUnknownOnline.GameState", "CasualtiesUnknownOnline.Protocol"]))));
	}

	[Fact]
	public void PluginReachingGameStateDirectly_IsRefused()
	{
		var violations = ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.Plugin", ["CasualtiesUnknownOnline.GameState"])));

		Assert.Contains(violations, violation => violation.Contains("Plugin references CasualtiesUnknownOnline.GameState", StringComparison.Ordinal));
	}

	[Fact]
	public void ConsumersAreNotConstrained()
	{
		// Test and tool projects assert on the real types, so they are exempt on
		// purpose — and the tree half would otherwise flag every one of them.
		Assert.Empty(ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.Tests", ["CasualtiesUnknownOnline.GameState", "CasualtiesUnknownOnline.Runtime", "CasualtiesUnknownOnline.GameAdapter"]),
			("CasualtiesUnknownOnline.ContractTool", ["CasualtiesUnknownOnline.Protocol"]))));
	}

	[Fact]
	public void UndeclaredProject_IsRefusedInsteadOfSilentlyExempt()
	{
		var violations = ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.SomethingNew", ["CasualtiesUnknownOnline.GameState"])));

		Assert.Contains(violations, violation => violation.Contains("SomethingNew is neither declared in the layer table nor listed as a consumer project", StringComparison.Ordinal));
	}

	[Fact]
	public void AbstractionsReachingDown_IsRefused()
	{
		// The mod-facing assembly is a shipped project, not a consumer: it must
		// stay self-contained (it is the only package a mod may reference).
		var violations = ProjectDirectionPolicy.Violations(Graph(
			("CasualtiesUnknownOnline.Abstractions", ["CasualtiesUnknownOnline.GameState"])));

		Assert.Contains(violations, violation => violation.Contains("Abstractions references CasualtiesUnknownOnline.GameState", StringComparison.Ordinal));
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<string>> Graph(
		params (string Project, string[] References)[] entries) =>
		entries.ToDictionary(entry => entry.Project, entry => (IReadOnlyList<string>)entry.References, StringComparer.Ordinal);
}
