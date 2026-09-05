using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// C# unit-test ports of the repository's source-shape PowerShell gates. These
/// tests make the checks part of the ordinary <c>dotnet test</c> run without
/// relying on a PowerShell subprocess.
/// </summary>
public class SourceShapeGateTests
{
	private static readonly string Src = RepositoryPaths.File("src");
	private static readonly string GameStateDir = RepositoryPaths.File("src/CasualtiesUnknownOnline.GameState");
	private static readonly string ItemsDir = RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Session/Items");

	private static readonly Regex NamespaceRegex = new(@"^namespace\s+([A-Za-z0-9_.]+)\s*(\{|;)", RegexOptions.Multiline);
	private static readonly Regex TopLevelTypeRegex = new(@"^(public\s+|internal\s+|sealed\s+|static\s+|abstract\s+|partial\s+)*(class|struct|interface|enum|record)\s+(\w+)");
	private static readonly Regex BoolStateFieldRegex = new(@"^\s*(private|internal|public|protected)?\s*(static\s+)?bool\s+_\w+\s*;");
	private static readonly Regex GameCommandBaseRegex = new(@":\s*GameCommand(?:\s|\()", RegexOptions.Multiline);
	private static readonly Regex StringKeyedDictionaryRegex = new(@"Dictionary\s*<\s*string\s*,");
	private static readonly Regex WorldTableMutationRegex = new(@"_worldTable\.(Set|Remove|Clear|RegisterIfAbsent)");
	private static readonly Regex TransferTableMutationRegex = new(@"_transferred\s*[\[]|_transferred\.");
	private static readonly Regex NoLegacyTypeRegex = new(@"^\s*(public\s+|internal\s+|private\s+|protected\s+|static\s+|sealed\s+|abstract\s+|partial\s+)*(class|record|struct|interface|enum)\s+(?<name>Shadow|Legacy|Compat|Dual)[A-Za-z0-9_]*", RegexOptions.IgnoreCase | RegexOptions.Multiline);

	private sealed record ArchitectureDebtEntry(int Lines, int BoolFlags);

	[Fact]
	public void Architecture_OneTopLevelTypePerFileAndAggregateLimits()
	{
		var failures = new List<string>();
		var types = new Dictionary<string, (int Lines, int BoolFlags)>(StringComparer.Ordinal);

		foreach (var file in EnumerateCSharpFiles(Src))
		{
			var lines = File.ReadAllLines(file);
			var text = string.Join("\n", lines);
			var ns = NamespaceRegex.Match(text);
			var namespaceName = ns.Success ? ns.Groups[1].Value : "";

			var topLevel = new List<string>();
			var depth = 0;
			foreach (var line in lines)
			{
				var trimmed = line.TrimStart();
				if (depth == 0 && TopLevelTypeRegex.IsMatch(trimmed))
				{
					topLevel.Add(TopLevelTypeRegex.Match(trimmed).Groups[3].Value);
				}

				depth += trimmed.Count(c => c == '{') - trimmed.Count(c => c == '}');
				if (depth < 0)
				{
					depth = 0;
				}
			}

			if (topLevel.Count > 1)
			{
				failures.Add($"{Relative(file)} : {topLevel.Count} top-level types (rule: one per file)");
				continue;
			}

			if (topLevel.Count != 1)
			{
				continue;
			}

			var fullName = namespaceName.Length == 0 ? topLevel[0] : $"{namespaceName}.{topLevel[0]}";
			var boolFlags = lines.Count(l => BoolStateFieldRegex.IsMatch(l));
			if (!types.TryGetValue(fullName, out var current))
			{
				current = (0, 0);
			}

			types[fullName] = (current.Lines + lines.Length, current.BoolFlags + boolFlags);
		}

		var debt = LoadArchitectureDebt();
		foreach (var pair in types)
		{
			var recorded = debt.GetValueOrDefault(pair.Key);
			if (pair.Value.Lines > 600 && (recorded is null || pair.Value.Lines > recorded.Lines))
			{
				failures.Add($"{pair.Key} : {pair.Value.Lines} aggregate lines (max 600; either split or record in docs/architecture-debt.json)");
			}

			if (pair.Value.BoolFlags > 5 && (recorded is null || pair.Value.BoolFlags > recorded.BoolFlags))
			{
				failures.Add($"{pair.Key} : {pair.Value.BoolFlags} boolean state fields (max 5; model a state machine instead)");
			}
		}

		Assert.True(failures.Count == 0, "Architecture gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void GameStateIsolation_NoForbiddenReferencesOrTokens()
	{
		var failures = new List<string>();
		var csprojPath = RepositoryPaths.File("src/CasualtiesUnknownOnline.GameState/CasualtiesUnknownOnline.GameState.csproj");
		var doc = XDocument.Load(csprojPath);

		foreach (var reference in doc.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
		{
			failures.Add($"GameState.csproj has forbidden ProjectReference: {reference.Attribute("Include")?.Value}");
		}

		foreach (var package in doc.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
		{
			var name = package.Attribute("Include")?.Value;
			if (name is not "Microsoft.NETFramework.ReferenceAssemblies")
			{
				failures.Add($"GameState.csproj has forbidden PackageReference: {name}");
			}
		}

		foreach (var reference in doc.Descendants().Where(e => e.Name.LocalName == "Reference"))
		{
			failures.Add($"GameState.csproj has forbidden Reference: {reference.Attribute("Include")?.Value}");
		}

		string[] forbiddenTokens =
		[
			"UnityEngine",
			"BepInEx",
			"Steamworks",
			"CasualtiesUnknownOnline.Runtime",
			"CasualtiesUnknownOnline.Protocol",
			"CasualtiesUnknownOnline.GameAdapter",
			"CasualtiesUnknownOnline.Plugin",
			"CasualtiesUnknownOnline.Abstractions",
			"CasualtiesUnknownOnline.Application",
			"Microsoft.Extensions",
			"System.Net",
			"System.IO",
			"System.Threading",
			"System.Random",
			"ProtoContract",
			"CharacterItemMsg",
			"ComponentStateMsg",
			"LiquidStackMsg",
			"NetVector",
			"protobuf"
		];

		foreach (var file in EnumerateCSharpFiles(GameStateDir))
		{
			var text = File.ReadAllText(file);
			foreach (var token in forbiddenTokens)
			{
				if (text.Contains(token, StringComparison.Ordinal))
				{
					failures.Add($"{Relative(file)} contains forbidden token '{token}'");
				}
			}
		}

		Assert.True(failures.Count == 0, "GameState isolation gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void ItemAuthority_NoDirectProjectionMutation()
	{
		string[] allowed =
		[
			"ItemProjection.cs",
			"ItemArbitration.cs",
			"KernelBatchItemProjection.cs",
			"WorldItemTable.cs"
		];

		var failures = new List<string>();
		foreach (var file in Directory.EnumerateFiles(ItemsDir, "*.cs"))
		{
			if (allowed.Contains(Path.GetFileName(file), StringComparer.Ordinal))
			{
				continue;
			}

			var text = File.ReadAllText(file);
			if (WorldTableMutationRegex.IsMatch(text))
			{
				failures.Add($"{Path.GetFileName(file)} mutates WorldItemTable directly; route through ItemProjection");
			}

			if (TransferTableMutationRegex.IsMatch(text))
			{
				failures.Add($"{Path.GetFileName(file)} mutates the transfer table directly; route through ItemArbitration");
			}
		}

		Assert.True(failures.Count == 0, "Item authority gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void NoLegacy_NoRemovedDualArchitectureMarkers()
	{
		string[] removedWireMarkers =
		[
			"NetMsg.PlayerState",
			"NetMsg.PlayerStateReport",
			"NetMsg.EnemyState",
			"NetMsg.PlayerCarryState",
			"NetMsg.PlayerInventoryTransfer",
			"NetMsg.PlayerHealResult",
			"NetMsg.PlayerItemUseResult",
			"NetMsg.EnemyBite",
			"NetMsg.EnemyLunge",
			"NetMsg.EnemyEffect",
			"NetMsg.EnemyRemoved",
			"NetMsg.WorldStartParams",
			"NetMsg.TrapStateSnapshot",
			"NetMsg.OpenedEntitiesSnapshot",
			"NetMsg.BuildingEntityHealthSnapshot",
			"ItemCheckpointStore",
			"KernelShadow",
			"KernelForDiagnostics",
			"ItemDiagnosticsProjection",
			"NetMsg.ItemReject"
		];

		var failures = new List<string>();
		foreach (var file in EnumerateCSharpFiles(Src))
		{
			var text = File.ReadAllText(file);
			if (NoLegacyTypeRegex.IsMatch(text))
			{
				failures.Add($"{Relative(file)} contains dual-architecture type declaration");
			}

			foreach (var marker in removedWireMarkers)
			{
				if (text.Contains(marker, StringComparison.Ordinal))
				{
					failures.Add($"{Relative(file)} contains removed wire marker '{marker}'");
				}
			}
		}

		Assert.True(failures.Count == 0, "No-legacy gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void CommandAuthority_EveryGameCommandDeclaresAuthority()
	{
		var failures = new List<string>();
		foreach (var file in EnumerateCSharpFiles(GameStateDir))
		{
			var text = File.ReadAllText(file);
			if (GameCommandBaseRegex.IsMatch(text) && !text.Contains("AuthorityKind", StringComparison.Ordinal) && !text.Contains("Authority", StringComparison.Ordinal))
			{
				failures.Add($"{Relative(file)} defines a GameCommand without an Authority policy");
			}
		}

		Assert.True(failures.Count == 0, "Command authority gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void KernelShape_NoStringKeyedStateOrHashtable()
	{
		var failures = new List<string>();
		foreach (var file in EnumerateCSharpFiles(GameStateDir))
		{
			var text = File.ReadAllText(file);
			if (StringKeyedDictionaryRegex.IsMatch(text))
			{
				failures.Add($"{Relative(file)} uses a string-keyed dictionary; kernel state must be typed");
			}

			if (text.Contains("Hashtable", StringComparison.Ordinal))
			{
				failures.Add($"{Relative(file)} uses Hashtable; kernel state must be typed");
			}
		}

		Assert.True(failures.Count == 0, "Kernel shape gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	private static IEnumerable<string> EnumerateCSharpFiles(string root)
	{
		return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
	}

	private static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path);

	private static Dictionary<string, ArchitectureDebtEntry> LoadArchitectureDebt()
	{
		var path = RepositoryPaths.File("docs/architecture-debt.json");
		if (!File.Exists(path))
		{
			return [];
		}

		var raw = JsonSerializer.Deserialize<Dictionary<string, ArchitectureDebtEntry>>(File.ReadAllText(path));
		return raw ?? [];
	}
}
