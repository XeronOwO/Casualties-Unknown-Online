using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The game-assembly binding gate. The Game Adapter is the framework's only
/// game-binding layer and the satellite pinyin mod's game-binding half is the one
/// declared exception (<c>docs/development/agent-reference.md</c>, Repository
/// Layout; <c>docs/en/contributing/repository-map-and-pitfalls.md</c>), so every other
/// framework project compiles against the adapter's ports, the Runtime and the
/// engine modules instead of the game's own code. Tests and tools are consumers
/// and stay unconstrained (<see cref="ProjectDirectionPolicy.ConsumerProjects"/>).
///
/// <para>
/// The scan is derived from the solution file, never from a hand-written list, so
/// a new project is covered the moment the solution lists it. The census floor
/// and the "declared binder still binds" half keep the gate from passing because
/// the scan read nothing, and the synthetic cases pin the matcher in both
/// directions.
///
/// <para>
/// Scope, stated rather than implied: the scan reads each solution project's OWN
/// project file. A reference that arrives through an imported
/// <c>Directory.Build.props</c> / <c>Directory.Build.targets</c> or any
/// <c>&lt;Import&gt;</c>ed props/targets file is outside it — the tree carries no such file
/// today, and following the MSBuild import closure is the fix if one appears.
/// </para>
/// </para>
/// </summary>
public class GameAssemblyReferenceGateTests
{
	/// <summary>The game's own assembly — the code the layout rule reserves for the adapter.</summary>
	private static readonly string[] GameAssemblies = ["Assembly-CSharp"];

	/// <summary>
	/// Projects allowed to bind the game's own code, each with the rule that allows
	/// it. A DECLARED fact rather than an omission: the declaration is asserted
	/// against the solution in both directions below.
	/// </summary>
	private static readonly Dictionary<string, string> DeclaredGameBinding = new(StringComparer.Ordinal)
	{
		["CasualtiesUnknownOnline.GameAdapter"] = "the framework's only game-binding layer (architecture.md §4)",
		["CasualtiesUnknownOnline.PinyinSearch"] = "the satellite mod's game-binding half (docs/en/contributing/repository-map-and-pitfalls.md, Where a new system belongs)",
	};

	/// <summary>The solution lists 13 projects; a scan that read fewer has broken, not improved.</summary>
	private const int ProjectFloor = 13;

	[Fact]
	public void Tree_OnlyTheDeclaredProjectsBindTheGameAssembly()
	{
		var projects = ProjectDirectionPolicy.SolutionProjects(RepositoryPaths.Root);
		Assert.True(projects.Count >= ProjectFloor,
			$"the gate read only {projects.Count} project(s) from the solution");

		var problems = projects
			.Where(project => !DeclaredGameBinding.ContainsKey(project.Name)
				&& !ProjectDirectionPolicy.ConsumerProjects.Contains(project.Name, StringComparer.Ordinal))
			.SelectMany(project => GameAssemblyReferences(project.Path)
				.Select(reference => $"{project.Name} references {reference}, which only a declared game-binding project may"))
			.ToArray();

		Assert.Empty(problems);
	}

	[Fact]
	public void Tree_EveryDeclaredBinder_IsStillInTheSolutionAndStillBinds()
	{
		var projects = ProjectDirectionPolicy.SolutionProjects(RepositoryPaths.Root)
			.ToDictionary(project => project.Name, project => project.Path, StringComparer.Ordinal);

		var problems = DeclaredGameBinding.Keys
			.Select(name => !projects.TryGetValue(name, out var path)
				? $"{name} is declared a game-binding project but the solution no longer lists it"
				: GameAssemblyReferences(path).Count == 0
					? $"{name} is declared a game-binding project but references no game assembly"
					: null)
			.Where(problem => problem is not null)
			.ToArray();

		Assert.Empty(problems);
	}

	[Theory]
	[InlineData("<Project><ItemGroup><Reference Include=\"Assembly-CSharp\"><HintPath>..\\..\\references\\Assembly-CSharp.dll</HintPath><Private>False</Private></Reference></ItemGroup></Project>", 1)]
	[InlineData("<Project><ItemGroup><Reference Include=\"Assembly-CSharp\"><HintPath>x</HintPath></Reference><Reference Include=\"Assembly-CSharp\"><HintPath>y</HintPath></Reference></ItemGroup></Project>", 2)]
	[InlineData("<Project><ItemGroup><Reference Include=\"UnityEngine\"><HintPath>..\\..\\references\\UnityEngine.dll</HintPath></Reference><Reference Include=\"UnityEngine.IMGUIModule\"><HintPath>..\\..\\references\\UnityEngine.IMGUIModule.dll</HintPath></Reference></ItemGroup></Project>", 0)]
	[InlineData("<Project><ItemGroup><ProjectReference Include=\"..\\CasualtiesUnknownOnline.Runtime\\CasualtiesUnknownOnline.Runtime.csproj\" /><PackageReference Include=\"Mapster\" /></ItemGroup></Project>", 0)]
	[InlineData("<Project><ItemGroup><Reference Include=\"Assembly-CSharp-firstpass\"><HintPath>x</HintPath></Reference></ItemGroup></Project>", 0)]
	public void TheGameAssemblyMatcher_SeesTheReferenceAndIgnoresItsNeighbours(string projectXml, int expected) =>
		Assert.Equal(expected, GameAssemblyReferences(XDocument.Parse(projectXml)).Count);

	/// <summary>The game-assembly references one project csproj declares, in declaration order.</summary>
	private static IReadOnlyList<string> GameAssemblyReferences(string projectPath) =>
		GameAssemblyReferences(XDocument.Load(projectPath));

	/// <summary>
	/// A game assembly is a <c>Reference</c> whose Include names it exactly: the
	/// engine modules sit in the same item group but are not the game's own code,
	/// and a prefix match would drag <c>Assembly-CSharp-firstpass</c> in.
	/// </summary>
	private static IReadOnlyList<string> GameAssemblyReferences(XDocument document) =>
		[.. document.Descendants()
			.Where(element => element.Name.LocalName == "Reference")
			.Select(element => element.Attribute("Include")?.Value ?? string.Empty)
			.Where(name => GameAssemblies.Contains(name, StringComparer.Ordinal))];
}
