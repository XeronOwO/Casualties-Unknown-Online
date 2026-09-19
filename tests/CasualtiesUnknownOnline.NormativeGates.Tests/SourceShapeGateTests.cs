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

	/// <summary>
	/// A classic extension method declaration: the line carries a <c>static</c> modifier AND its parameter
	/// list carries the <c>this</c> parameter modifier. Matching on those two facts instead of on a guessed
	/// signature shape is what keeps generic (<c>M&lt;T&gt;(this …)</c>), attributed and multi-modifier
	/// declarations visible. Applied line by line, so a signature wrapped across lines would slip past —
	/// the formatter keeps these single-line (measured against the pre-migration revision) and a wrap
	/// fails loudly rather than silently missing anything else.
	/// </summary>
	private static readonly Regex ClassicExtensionMethodRegex = new(@"^\s*(?!//|\*)(?=.*\bstatic\b)[^=;]*\([^)]*\bthis\s+[A-Za-z_]\w*");

	/// <summary>Guards the scan against silently checking nothing — the tree carries roughly two thousand C# files under these roots.</summary>
	private const int ScannedFileFloor = 1500;

	/// <summary>Test-data attributes carry sample SOURCE TEXT as strings — this gate's own matcher contract among them — and a sample is not a declaration, so those lines are skipped rather than read as code.</summary>
	private static readonly Regex TestDataLineRegex = new(@"^\s*\[(?:InlineData|MemberData|TheoryData)\b");

	/// <summary>The inert-capture shape: a null test on the item's id component, i.e. "skip it because it is already bound".</summary>
	private static readonly Regex BoundItemSkipRegex = new(@"GetComponent<ItemInstanceId>\(\)\s*(!=\s*null|is\s+not\s+null)");

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

	[Fact]
	public void MicrosoftExtensionsPinnedToNet48CompatibleLine()
	{
		var failures = new List<string>();
		var centralPackagesPath = RepositoryPaths.File("Directory.Packages.props");
		var doc = XDocument.Load(centralPackagesPath);

		foreach (var package in doc.Descendants().Where(e => e.Name.LocalName == "PackageVersion"))
		{
			var id = package.Attribute("Include")?.Value;
			if (id is null || !id.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))
			{
				continue;
			}

			var version = package.Attribute("Version")?.Value;
			if (version is null || !version.StartsWith("3.1.", StringComparison.Ordinal))
			{
				failures.Add($"{Relative(centralPackagesPath)}: {id} {version} must use the net48-compatible Microsoft.Extensions 3.1.x line (architecture blueprint §5)");
			}
		}

		Assert.True(failures.Count == 0, "Microsoft.Extensions net48 compatibility gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	/// <summary>The matcher's own contract, so a later edit cannot narrow the scan silently (the reviewer probed exactly these shapes against the first version, which missed three of them).</summary>
	[Theory]
	[InlineData("public static void ApplyTo(this EnemyEntity entity) { }", true)]
	[InlineData("internal static bool IsNear(this EnemyEntity a, float r) => true;", true)]
	[InlineData("public static T First<T>(this IEnumerable<T> source) => source.First();", true)]
	[InlineData("[Obsolete] public static Foo Bar(this int x) => null;", true)]
	[InlineData("public unsafe static void Fill(this int[] xs) { }", true)]
	[InlineData("private static WireVector2 ToWireVector2(NetVector2 value) => default;", false)]
	[InlineData("// a helper that takes (this x) while talking about static state", false)]
	[InlineData("/// <summary>static state drives this.Model on the clone</summary>", false)]
	[InlineData("var text = \"static (this x)\";", false)]
	public void TheMatcher_SeesEveryClassicExtensionShapeAndIgnoresOrdinaryStatics(string line, bool expected) =>
		Assert.Equal(expected, ClassicExtensionMethodRegex.IsMatch(line));

	[Fact]
	public void ExtensionMethods_UseTheCsharp14ExtensionSyntax()
	{
		var failures = new List<string>();
		var scanned = 0;
		foreach (var root in new[] { Src, RepositoryPaths.File("tests") })
		{
			foreach (var file in EnumerateCSharpFiles(root))
			{
				scanned++;
				var lines = File.ReadAllLines(file);
				for (var i = 0; i < lines.Length; i++)
				{
					if (TestDataLineRegex.IsMatch(lines[i]))
					{
						continue;
					}

					if (ClassicExtensionMethodRegex.IsMatch(lines[i]))
					{
						failures.Add($"{Relative(file)}:{i + 1} : classic 'this X' extension method — write the C# 14 extension(receiver) block instead (the two forms compile to the same call sites, so there is no case for the old one)");
					}
				}
			}
		}

		Assert.True(
			scanned >= ScannedFileFloor,
			$"the scan only saw {scanned} C# file(s); the roots or the enumeration broke and this rule would pass by checking nothing");
		Assert.True(failures.Count == 0, "extension-method shape gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	/// <summary>
	/// The carried-inventory report is an ABSOLUTE set that is REPEATED, so its capture must
	/// state every authoritative item of the body — including one that already carries an
	/// instance id (a snapshot id, an id an earlier report stamped, a product CraftingSync
	/// stamped). Skipping bound items makes the repeat inert: the first capture stamps an id on
	/// every item it reports (<c>ItemIdAllocator.Allocate</c>), so every later capture would come
	/// back empty and send nothing at all — the registration would silently stop converging,
	/// which is exactly the gap the re-report exists to close (sync-coverage row I8; the
	/// 2026-09-19 adversarial review found the shipped version doing this). The capture itself
	/// needs Unity and cannot run in this suite, so this gate pins the SHAPE the inert capture was
	/// written in — a null test on the id component, directly in the filter. An equivalent skip
	/// written through a local (<c>var id = item.GetComponent&lt;ItemInstanceId&gt;(); if (id != null)
	/// continue;</c>) is deliberately NOT matched here: that one is caught by its EFFECT instead,
	/// through <c>ItemIdCoordinator</c>'s warning for a window that captures nothing after a
	/// non-empty registration.
	/// </summary>
	[Fact]
	public void CarriedInventoryCapture_DoesNotSkipItemsThatAlreadyCarryAnInstanceId()
	{
		var reporter = RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Items/CarriedInventoryReporter.cs");
		Assert.True(File.Exists(reporter), $"the carried-inventory reporter moved: {Relative(reporter)}");

		var text = File.ReadAllText(reporter);
		Assert.True(
			text.Contains("EnsureId", StringComparison.Ordinal),
			"the reporter no longer reads like a capture — this rule would pass by checking nothing");

		var failures = new List<string>();
		var lines = File.ReadAllLines(reporter);
		for (var i = 0; i < lines.Length; i++)
		{
			if (BoundItemSkipRegex.IsMatch(lines[i]))
			{
				failures.Add($"{Relative(reporter)}:{i + 1} : the capture skips an item because it already carries an instance id — the absolute registration states the CURRENT set, bound items included (a skip makes every repeat after the first empty)");
			}
		}

		Assert.True(failures.Count == 0, "carried-inventory capture gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	/// <summary>The matcher's own contract: a null test on the id component is the skip shape, reading the component is not.</summary>
	[Theory]
	[InlineData("if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==", true)]
	[InlineData("if (worn.GetComponent<ItemInstanceId>() is not null)", true)]
	[InlineData("if (worn.GetComponent<ItemInstanceId>() == null)", false)]
	[InlineData("if (_ids.EnsureId(item) == 0)", false)]
	[InlineData("var bound = item.GetComponent<ItemInstanceId>();", false)]
	public void TheBoundItemSkipMatcher_SeesTheSkipShapeAndIgnoresOrdinaryReads(string line, bool expected) =>
		Assert.Equal(expected, BoundItemSkipRegex.IsMatch(line));

	/// <summary>
	/// The entry repair's adapter half (sync-coverage rows R3/W7; the cadence review's finding 4):
	/// the two owners that re-fan-out their entry tables on the InWorld edge — the keypad codes in
	/// <c>WorldEventSync</c>, the geyser liquid types in <c>GeyserStateSync</c> — must also re-send
	/// them on <c>ISessionControl.EntryRepairRequested</c>. That subscription is the only thing that
	/// heals a swallowed entry send for those two tables, and neither owner is reachable from the
	/// test suite (both sends sit behind the Unity world being alive). The gate reads the bind pair
	/// INSIDE the two methods that own it (<c>BindToSession</c> / <c>Unbind</c>), so a rebind that
	/// dropped the signal, or a pair whose halves were swapped, fails here — its matcher contract is
	/// asserted by <see cref="TheEntryRepairBindMatcher_SeesThePairAndItsFailureShapes"/>.
	/// <para>
	/// Its reach, stated exactly: whole-file text of those two files, method bodies located by
	/// signature. A bind moved into a helper CALLED from those methods still passes at the call
	/// site, and one moved to another part-file of the same <c>partial</c> class is not seen at all.
	/// Those are covered by the runtime event assertion in
	/// <c>CasualtiesUnknownOnline.Tests/Session/EntryRepairConvergenceTests.cs</c> (the event fires
	/// once for the re-asserting member) and by the Information line each handler logs; neither net
	/// proves the adapter's Unity-guarded sends land in a real session.
	/// </para>
	/// </summary>
	[Fact]
	public void EntryRepair_IsBoundByBothAdapterEntryTableOwners()
	{
		foreach (var relative in new[]
		{
			"src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/World/GeyserStateSync.cs"
		})
		{
			var path = RepositoryPaths.File(relative);
			Assert.True(File.Exists(path), $"the adapter entry-table owner moved: {relative}");

			var failures = EntryRepairBindingFailures(File.ReadAllText(path));
			Assert.True(failures.Count == 0, $"{relative}: " + string.Join("; ", failures));
		}
	}

	/// <summary>The gate's own contract: a proper pair passes, and every shape it exists to catch fails.</summary>
	[Theory]
	[InlineData("internal void BindToSession() { _session.EntryRepairRequested += OnEntryRepairRequested; } internal void Unbind() { _session.EntryRepairRequested -= OnEntryRepairRequested; }", 0)]
	[InlineData("internal void BindToSession() { } internal void Unbind() { _session.EntryRepairRequested += OnEntryRepairRequested; }", 2)]
	[InlineData("internal void BindToSession() { } internal void Unbind() { } // EntryRepairRequested += OnEntryRepairRequested;", 2)]
	[InlineData("internal void BindToSession() { _session.EntryRepairRequested += OnEntryRepairRequested; } internal void Unbind() { }", 1)]
	[InlineData("// neither method exists", 2)]
	public void TheEntryRepairBindMatcher_SeesThePairAndItsFailureShapes(string source, int expectedFailures) =>
		Assert.Equal(expectedFailures, EntryRepairBindingFailures(source).Count);

	private static List<string> EntryRepairBindingFailures(string text)
	{
		var failures = new List<string>();
		var bind = MethodBody(text, "internal void BindToSession()");
		var unbind = MethodBody(text, "internal void Unbind()");

		if (bind.Length == 0)
		{
			failures.Add("BindToSession() not found — this rule would pass by checking nothing");
		}
		else if (!bind.Contains("EntryRepairRequested += ", StringComparison.Ordinal))
		{
			failures.Add("BindToSession() does not subscribe to ISessionControl.EntryRepairRequested — a swallowed entry send of this table would wait for the 60 s cycle again");
		}

		if (unbind.Length == 0)
		{
			failures.Add("Unbind() not found — this rule would pass by checking nothing");
		}
		else if (!unbind.Contains("EntryRepairRequested -= ", StringComparison.Ordinal))
		{
			failures.Add("Unbind() does not unsubscribe from EntryRepairRequested — a rebind would leave a stale handler on the session");
		}

		return failures;
	}

	/// <summary>The body of one method, located by its signature and closed by brace counting.</summary>
	private static string MethodBody(string text, string signature)
	{
		var start = text.IndexOf(signature, StringComparison.Ordinal);
		if (start < 0)
		{
			return string.Empty;
		}

		var open = text.IndexOf('{', start);
		if (open < 0)
		{
			return string.Empty;
		}

		var depth = 0;
		for (var i = open; i < text.Length; i++)
		{
			if (text[i] == '{')
			{
				depth++;
			}
			else if (text[i] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return text[open..(i + 1)];
				}
			}
		}

		return string.Empty;
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
