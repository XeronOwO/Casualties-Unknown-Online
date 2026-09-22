using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The declared project-dependency direction, as data, plus a pure checker over
/// a project-reference graph. The tree half is <c>ProjectDirectionGateTests</c>;
/// the synthetic halves feed this same checker so the gate cannot pass by
/// scanning nothing.
///
/// <para>
/// The shape it encodes: GameState is the bottom and references nothing;
/// Application is the ONLY way up from the kernel (Runtime -> Application ->
/// GameState), so the Runtime reaches the kernel through one layer instead of
/// declaring its own seam; Plugin/GameAdapter sit above Runtime. Tests and tools
/// are consumers — they are not constrained (they need the real types to assert
/// on). Exemption is a DECLARED fact, not an omission: every project the solution
/// lists must appear either in <see cref="AllowedReferences"/> or in
/// <see cref="ConsumerProjects"/>, so a new project cannot slip past the gate
/// unclassified.
/// </para>
/// </summary>
internal static class ProjectDirectionPolicy
{
	/// <summary>
	/// Project name -> the projects it may reference DIRECTLY. Names are the
	/// csproj file names from <c>CasualtiesUnknownOnline.slnx</c>.
	/// </summary>
	internal static IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedReferences { get; } =
		new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
		{
			["CasualtiesUnknownOnline.GameState"] = [],
			["CasualtiesUnknownOnline.Protocol"] = [],
			["CasualtiesUnknownOnline.Abstractions"] = [],
			["CasualtiesUnknownOnline.Application"] =
			[
				"CasualtiesUnknownOnline.GameState",
				"CasualtiesUnknownOnline.Protocol",
			],
			["CasualtiesUnknownOnline.Runtime"] =
			[
				"CasualtiesUnknownOnline.Abstractions",
				"CasualtiesUnknownOnline.Protocol",
				"CasualtiesUnknownOnline.Application",
			],
			["CasualtiesUnknownOnline.GameAdapter"] = ["CasualtiesUnknownOnline.Runtime"],
			["CasualtiesUnknownOnline.Plugin"] =
			[
				"CasualtiesUnknownOnline.Runtime",
				"CasualtiesUnknownOnline.GameAdapter",
			],
			["CasualtiesUnknownOnline.ModExample"] = ["CasualtiesUnknownOnline.Abstractions"],
			["CasualtiesUnknownOnline.PinyinSearch.Core"] = ["CasualtiesUnknownOnline.Abstractions"],
			["CasualtiesUnknownOnline.PinyinSearch"] = ["CasualtiesUnknownOnline.PinyinSearch.Core"],
		};

	/// <summary>
	/// Projects that are deliberately NOT constrained: the test assemblies and the
	/// development tool assert on the real types, so they may reference whatever
	/// they need. Listed explicitly so the exemption is a decision, and asserted
	/// against the solution so neither side can drift silently.
	/// </summary>
	internal static IReadOnlyList<string> ConsumerProjects { get; } =
	[
		"CasualtiesUnknownOnline.Tests",
		"CasualtiesUnknownOnline.NormativeGates.Tests",
		"CasualtiesUnknownOnline.ContractTool",
	];

	/// <summary>
	/// Projects that must reference a project to keep the layer on their path:
	/// the Runtime observes the kernel, so it must go through Application rather
	/// than being allowed to drop the reference and stop compiling when it next
	/// touches a kernel type.
	/// </summary>
	internal static IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredReferences { get; } =
		new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
		{
			["CasualtiesUnknownOnline.Runtime"] = ["CasualtiesUnknownOnline.Application"],
		};

	/// <summary>
	/// Every way the graph breaks the declared direction, as human-readable
	/// lines. An empty list means the graph follows it.
	/// </summary>
	internal static IReadOnlyList<string> Violations(IReadOnlyDictionary<string, IReadOnlyList<string>> graph)
	{
		var violations = new List<string>();
		foreach (var (project, references) in graph.OrderBy(pair => pair.Key, StringComparer.Ordinal))
		{
			if (ConsumerProjects.Contains(project, StringComparer.Ordinal))
			{
				continue;
			}

			if (!AllowedReferences.TryGetValue(project, out var allowed))
			{
				violations.Add($"{project} is neither declared in the layer table nor listed as a consumer project — classify it");
				continue;
			}

			foreach (var reference in references)
			{
				if (!allowed.Contains(reference, StringComparer.Ordinal))
				{
					violations.Add($"{project} references {reference}, which its declared layer does not allow");
				}
			}

			if (RequiredReferences.TryGetValue(project, out var required))
			{
				foreach (var needed in required.Where(needed => !references.Contains(needed, StringComparer.Ordinal)))
				{
					violations.Add($"{project} must reference {needed} — reaching the kernel without the layer is exactly what the gate forbids");
				}
			}
		}

		return violations;
	}

	/// <summary>
	/// Every project <c>CasualtiesUnknownOnline.slnx</c> lists, as its name and
	/// csproj path. This is the ONE reader of the solution file: the direction gate
	/// and the game-assembly gate both derive their scan surface from it, so a new
	/// project cannot slip past either by being absent from a hand-written list.
	/// </summary>
	internal static IReadOnlyList<(string Name, string Path)> SolutionProjects(string root)
	{
		var solution = XDocument.Load(Path.Combine(root, "CasualtiesUnknownOnline.slnx"));
		return [.. solution.Descendants()
			.Where(element => element.Name.LocalName == "Project")
			.Select(element => element.Attribute("Path")?.Value)
			.Where(value => !string.IsNullOrEmpty(value))
			.Select(value => value!.Replace('/', Path.DirectorySeparatorChar))
			.Select(relative => (Name: Path.GetFileNameWithoutExtension(relative), Path: Path.Combine(root, relative)))];
	}

	/// <summary>
	/// Reads the real graph: every project the solution lists, with the CUO
	/// references each csproj declares — a <c>ProjectReference</c> or a raw
	/// <c>Reference</c> to a CUO assembly, so an assembly-reference bypass is not
	/// invisible to the gate. A project that declares none still gets an entry, so
	/// "GameState references nothing" is a fact this reads rather than an absence
	/// it mistakes for agreement.
	/// </summary>
	internal static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadSolutionGraph(string root)
	{
		var graph = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
		foreach (var (name, path) in SolutionProjects(root))
		{
			var document = XDocument.Load(path);
			graph[name] = [.. document.Descendants()
				.Where(element => element.Name.LocalName == "ProjectReference"
					|| (element.Name.LocalName == "Reference"
						&& (element.Attribute("Include")?.Value ?? string.Empty).StartsWith("CasualtiesUnknownOnline.", StringComparison.Ordinal)))
				.Select(element => element.Attribute("Include")?.Value ?? string.Empty)
				.Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)))];
		}

		return graph;
	}
}
