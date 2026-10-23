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
	/// <summary>
	/// A top-level type declaration, matched on the FACTS the language defines rather than on the
	/// modifier spellings this repository happens to use (the previous version spelled them by hand
	/// and could not see <c>readonly</c> or <c>file</c>, so two types sat in one file with a green
	/// gate): an optional attribute run, then any number of the modifiers a type declaration may
	/// carry in any order, then the type keyword — <c>record struct</c>/<c>record class</c> included,
	/// because that is one two-word keyword and the NAME follows it.
	///
	/// The rule counts DEPTH-0 declarations only, and that IS the rule (the test's own name says
	/// top-level): a nested type belongs to its owner — its lines count toward the owner's aggregate
	/// — and is not a second top-level type. 306 nested declarations across 130 files take that shape
	/// today, and the seven files the widening exposed were the entire depth-0 offender set.
	///
	/// Declared limits, both measured at the widening (2026-09-25): the scan is line-based, so a
	/// declaration wrapped across lines is missed (zero such declarations in <c>src/</c>), and a
	/// top-level <c>delegate</c> is not matched (its name does not follow its keyword; zero such
	/// declarations in <c>src/</c>). The keyword set is the five type keywords and their two-word
	/// records.
	/// </summary>
	private static readonly Regex TopLevelTypeRegex = new(
		@"^(?:\[[^\r\n]*\]\s*)*(?:(?:public|internal|private|protected|file|static|sealed|abstract|readonly|partial|unsafe|new|ref)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<name>\w+)");

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

	/// <summary>The same guard for the one-top-level-type scan: <c>src/</c> measured 1668 top-level type declarations across 1662 C# files when the matcher was widened (2026-09-25; 1670 files once the seven files it exposed were split); the floors sit near 60% of that and catch a scan that sees nothing or almost nothing. What catches a NARROWED pattern is the matcher's own samples — a dropped modifier fails a sample, not a floor.</summary>
	private const int SourceFileFloor = 1000;

	private const int TopLevelTypeFloor = 1000;

	/// <summary>Test-data attributes carry sample SOURCE TEXT as strings — this gate's own matcher contract among them — and a sample is not a declaration, so those lines are skipped rather than read as code.</summary>
	private static readonly Regex TestDataLineRegex = new(@"^\s*\[(?:InlineData|MemberData|TheoryData)\b");

	/// <summary>The inert-capture shape: a null test on the item's id component, i.e. "skip it because it is already bound".</summary>
	private static readonly Regex BoundItemSkipRegex = new(@"GetComponent<ItemInstanceId>\(\)\s*(!=\s*null|is\s+not\s+null)");

	private sealed record ArchitectureDebtEntry(int Lines, int BoolFlags);

	/// <summary>One top-level type per file, plus the aggregate line and boolean-flag ceilings keyed by the type's full name. Nested declarations are their owner's business and are counted there, not here.</summary>
	[Fact]
	public void Architecture_OneTopLevelTypePerFileAndAggregateLimits()
	{
		var failures = new List<string>();
		var types = new Dictionary<string, (int Lines, int BoolFlags)>(StringComparer.Ordinal);
		var scanned = 0;
		var topLevelTypes = 0;

		foreach (var file in EnumerateCSharpFiles(Src))
		{
			scanned++;
			var lines = File.ReadAllLines(file);
			var text = string.Join("\n", lines);
			var ns = NamespaceRegex.Match(text);
			var namespaceName = ns.Success ? ns.Groups[1].Value : "";

			var topLevel = new List<string>();
			var depth = 0;
			foreach (var line in lines)
			{
				var trimmed = line.TrimStart();
				if (TryReadTopLevelTypeName(trimmed, depth, out var typeName))
				{
					topLevel.Add(typeName);
				}

				depth += trimmed.Count(c => c == '{') - trimmed.Count(c => c == '}');
				if (depth < 0)
				{
					depth = 0;
				}
			}

			topLevelTypes += topLevel.Count;

			if (topLevel.Count > 1)
			{
				failures.Add($"{Relative(file)} : {topLevel.Count} top-level type declarations — {string.Join(", ", topLevel)} (rule: one top-level type per file; a nested declaration belongs to its owner and does not count)");
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

		Assert.True(
			scanned >= SourceFileFloor,
			$"the scan only saw {scanned} C# file(s) under src/; the root or the enumeration broke and this rule would pass by checking nothing");
		Assert.True(
			topLevelTypes >= TopLevelTypeFloor,
			$"the scan only saw {topLevelTypes} top-level type declaration(s); the matcher narrowed and this rule would pass by checking almost nothing");
		Assert.True(failures.Count == 0, "Architecture gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	/// <summary>The two facts a declaration must satisfy to be a top-level type of this file: the line IS a type declaration (the matcher above), and it is not nested — brace depth 0. Read once, so the name the scan counts cannot disagree with the test that admitted it.</summary>
	private static bool TryReadTopLevelTypeName(string trimmedLine, int depth, out string name)
	{
		name = "";
		if (depth != 0)
		{
			return false;
		}

		var match = TopLevelTypeRegex.Match(trimmedLine);
		if (!match.Success)
		{
			return false;
		}

		name = match.Groups["name"].Value;
		return true;
	}

	/// <summary>The matcher's own contract: every modifier the language allows on a type declaration, in any order, the attribute run, and the two-word record keyword — plus the shapes that must NOT count, so the widening cannot be "fixed" by matching anything that mentions a type. The declared boundaries are pinned here as negatives too: a mention in a doc comment (with or without declaration text after it) and a top-level <c>delegate</c>, whose name does not follow its keyword.</summary>
	[Theory]
	[InlineData("public class Foo", true)]
	[InlineData("internal sealed class Foo", true)]
	[InlineData("internal readonly record struct Foo(", true)]
	[InlineData("public sealed record Foo(", true)]
	[InlineData("public readonly record struct Point(int X, int Y);", true)]
	[InlineData("public sealed record struct Row(int X);", true)]
	[InlineData("public record class Node;", true)]
	[InlineData("file class Foo", true)]
	[InlineData("static partial class Foo", true)]
	[InlineData("public static class Foo", true)]
	[InlineData("private readonly struct Foo", true)]
	[InlineData("protected abstract class Foo", true)]
	[InlineData("public unsafe struct Foo", true)]
	[InlineData("internal ref struct Foo", true)]
	[InlineData("public new class Foo", true)]
	[InlineData("[Serializable] public sealed class Foo", true)]
	[InlineData("[Obsolete(\"x\")] internal readonly record struct Foo(", true)]
	[InlineData("public enum Foo", true)]
	[InlineData("public interface IFoo", true)]
	[InlineData("// internal readonly record struct Foo - the shape this gate used to miss", false)]
	[InlineData("/// <summary>sealed record Foo sits beside its owner here</summary>", false)]
	[InlineData("/// public class Foo", false)]
	[InlineData("var foo = new Foo();", false)]
	[InlineData("public void Record(int x) { }", false)]
	[InlineData("private static readonly int _foo = 0;", false)]
	[InlineData("using Foo = System.Collections.Generic.List<int>;", false)]
	[InlineData("[assembly: InternalsVisibleTo(\"X\")]", false)]
	[InlineData("[Serializable]", false)]
	[InlineData("public delegate void Handler(int x);", false)]
	public void TheMatcher_SeesEveryDeclarationShapeAndIgnoresMentions(string line, bool expected) =>
		Assert.Equal(expected, TopLevelTypeRegex.IsMatch(line));

	/// <summary>Two facts the samples above cannot state: a NESTED declaration (depth 1+) is not a top-level type, and the name after a two-word record keyword is the name — the hand-spelled matcher read the second keyword instead.</summary>
	[Theory]
	[InlineData("public class Foo", 0, "Foo")]
	[InlineData("internal readonly record struct Foo(", 0, "Foo")]
	[InlineData("public sealed record struct Point(int X, int Y);", 0, "Point")]
	[InlineData("public record class Node;", 0, "Node")]
	[InlineData("public sealed class Widget", 3, "")]
	[InlineData("private sealed record Nested", 1, "")]
	public void OnlyADepthZeroDeclarationCounts_AndTheNameFollowsTheKeyword(string line, int depth, string expected)
	{
		var found = TryReadTopLevelTypeName(line, depth, out var name);
		Assert.Equal(expected.Length > 0, found);
		Assert.Equal(expected, name);
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
	/// The character restore re-materializes a snapshot's items through the game's own slot
	/// path (<c>Body.PickUpItem</c> / <c>Container.LoadItem</c>), and those calls are item
	/// OPERATIONS: <c>PickupSync</c> and <c>ContainerItemSync</c> report them as pickups. A
	/// restore is not an operation — the host already holds the facts it re-states — and batch
	/// <c>20260930-g</c>'s host log shows what the reports bought: the items that reappeared
	/// were reported, and the kernel refused exactly them, <c>Conflict (item … is already
	/// carried)</c>, while the refusal path (<c>ItemApplication.OnItemRejected</c> →
	/// <c>RollbackPickup</c>) is the one that pulls a restored item back OUT of the body. The
	/// refusal is NOT claimed to be that batch's only loss — the control reconnect lost its dog
	/// food with no refusal at all, and that half is the flat transfer-table merge — so this gate
	/// guards the REPORT, which is wrong on its own terms, not the whole loss. The write half
	/// therefore runs inside the adapter's <c>CallContext.Origin.RemoteApply</c> scope, the same
	/// scope the item correction applies already use (<c>ItemApplication</c>), which is what
	/// keeps those hooks from reporting what a restore puts back.
	/// <para>
	/// Its reach, stated exactly: the whole file text of the applier, with the method body located
	/// by signature and the scope's own braced block walked. Unity is not reachable from this
	/// suite, so only the SCOPE and what it encloses are pinned here; its runtime half is the
	/// acceptance run's reading that a reconnect logs no <c>Conflict</c> for a restored id, which
	/// this gate does not claim to replace.
	/// </para>
	/// </summary>
	[Fact]
	public void CharacterRestore_MaterializesItemsInsideARemoteApplyScope()
	{
		var applier = RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterRestoreApplier.cs");
		Assert.True(File.Exists(applier), $"the character restore applier moved: {Relative(applier)}");

		var failures = CharacterRestoreScopeFailures(File.ReadAllText(applier));
		Assert.True(failures.Count == 0, "character restore scope gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	/// <summary>The gate's own contract: the scope must exist AND its own block must hold the materialization; a body that reports without it, one whose scope encloses nothing, and one that materializes nothing each fail.</summary>
	[Theory]
	[InlineData("internal void ApplyItems(Body body, CharacterDataMsg data) { using (CallContext.Enter(CallContext.Origin.RemoteApply)) { ItemStateCodec.RestoreItem(null, body); } }", 0)]
	[InlineData("internal void ApplyItems(Body body, CharacterDataMsg data) { _wearables.RestoreWearable(null, body); }", 1)]
	[InlineData("internal void ApplyItems(Body body, CharacterDataMsg data) { using (CallContext.Enter(CallContext.Origin.RemoteApply)) { } }", 2)]
	[InlineData("internal void ApplyItems(Body body, CharacterDataMsg data) { using (CallContext.Enter(CallContext.Origin.RemoteApply)) { } ItemStateCodec.RestoreItem(null, body); }", 1)]
	[InlineData("internal void ApplyItems(Body body, CharacterDataMsg data) {\n// using (CallContext.Enter(CallContext.Origin.RemoteApply))\nItemStateCodec.RestoreItem(null, body);\n}", 1)]
	[InlineData("internal void SomethingElse() { }", 1)]
	public void TheCharacterRestoreScopeMatcher_SeesTheScopeAndItsFailureShapes(string source, int expectedFailures) =>
		Assert.Equal(expectedFailures, CharacterRestoreScopeFailures(source).Count);

	private static List<string> CharacterRestoreScopeFailures(string text)
	{
		var failures = new List<string>();
		var body = MethodBody(text, "internal void ApplyItems(Body body, CharacterDataMsg data)");
		if (body.Length == 0)
		{
			failures.Add("ApplyItems() not found — this rule would pass by checking nothing");
			return failures;
		}

		var live = LiveLines(body);
		var scope = live.IndexOf("using (CallContext.Enter(CallContext.Origin.RemoteApply))", StringComparison.Ordinal);
		if (scope < 0)
		{
			failures.Add("ApplyItems() materializes the snapshot's items outside a RemoteApply scope — the native pickups it drives are reported as operations the host refuses (Conflict: already carried), and the refusal path rolls the restored item back out of the body");
		}
		else
		{
			var scopeBlock = BracedBlock(live, live.IndexOf('{', scope));
			if (!scopeBlock.Contains("RestoreItem(", StringComparison.Ordinal)
				&& !scopeBlock.Contains("RestoreWearable(", StringComparison.Ordinal))
			{
				failures.Add("the RemoteApply scope does not enclose the materialization — the scope's own block states no RestoreItem/RestoreWearable, so the restore still reports what it puts back");
			}
		}

		if (!live.Contains("RestoreItem(", StringComparison.Ordinal) && !live.Contains("RestoreWearable(", StringComparison.Ordinal))
		{
			failures.Add("ApplyItems() materializes no item — this rule would pass by checking nothing");
		}

		return failures;
	}

	/// <summary>The braced block that opens at <paramref name="openIndex"/> — the scope's own body, so a rule can say the materialization is INSIDE it rather than merely in the same method.</summary>
	private static string BracedBlock(string text, int openIndex)
	{
		if (openIndex < 0 || openIndex >= text.Length || text[openIndex] != '{')
		{
			return string.Empty;
		}

		var depth = 0;
		for (var i = openIndex; i < text.Length; i++)
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
					return text[openIndex..(i + 1)];
				}
			}
		}

		return string.Empty;
	}

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
	[InlineData("internal void BindToSession() { // _session.EntryRepairRequested += OnEntryRepairRequested; } internal void Unbind() { _session.EntryRepairRequested -= OnEntryRepairRequested; }", 1)]
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
		else if (!LiveLines(bind).Contains("EntryRepairRequested += ", StringComparison.Ordinal))
		{
			failures.Add("BindToSession() does not subscribe to ISessionControl.EntryRepairRequested — a swallowed entry send of this table would wait for the 60 s cycle again");
		}

		if (unbind.Length == 0)
		{
			failures.Add("Unbind() not found — this rule would pass by checking nothing");
		}
		else if (!LiveLines(unbind).Contains("EntryRepairRequested -= ", StringComparison.Ordinal))
		{
			failures.Add("Unbind() does not unsubscribe from EntryRepairRequested — a rebind would leave a stale handler on the session");
		}

		return failures;
	}

	/// <summary>The body's LIVE lines: a subscribe that was commented out is not wiring, so a line's comment half (own-line or trailing) is dropped before the rule looks at it.</summary>
	private static string LiveLines(string body) =>
		string.Join('\n', body.Split('\n').Select(line =>
		{
			var comment = line.IndexOf("//", StringComparison.Ordinal);
			return comment < 0 ? line : line[..comment];
		}));

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

	/// <summary>
	/// The give ladder's drop half is a chain that must stay wired together: the transfer's Drop branch drops the
	/// item on the receiving body, that placement delegates to the ONE world-item creation (shared with the console
	/// summon verb), and that creation REPORTS the object through the item domain. A created object nobody reported
	/// is invisible to the host's kernel and killed by the next world-item keyframe — the defect the independent
	/// review found — and the behaviour suites cannot host the adapter, so the wiring is pinned here by source.
	/// The report moved out of the placement when the summon verb needed the same creation; the pin moves with the
	/// claim, so a later edit that drops the report still fails here.
	/// </summary>
	[Fact]
	public void GiveLadderDrop_IsWiredFromTheTransferToTheSpawnReport()
	{
		var apply = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs"));
		var placement = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/PlayerInventoryPlacement.cs"));
		var creation = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Items/WorldItemCreation.cs"));

		Assert.Contains("msg.Destination == PlayerInventoryDestination.Drop", apply);
		Assert.Contains("_placement.DropCreatedItemAtLocalBody(body, dropped)", apply);
		Assert.Contains("_worldItems.TryCreateAt(body.transform.position, item, \"Give\", out _)", placement);
		Assert.Contains("_items.SendItemSpawned(", creation);
		Assert.Contains("ItemStateCodec.CaptureItem(created, -1)", creation);
	}

	/// <summary>
	/// The restore's layer decision belongs to the APPLY, not the receive. A reconnecting member's saved
	/// character is sent BEFORE the run-baseline checkpoint (the handshake's own order), so at arrival this
	/// side has no current generation and the comparison answers "unknown"; by the time the body exists the
	/// baseline names the layer the body was generated in. The adapter is compile-excluded, so the wiring is
	/// pinned here rather than by a behaviour test (the policy itself is pinned by
	/// <c>CharacterRestorePositionPolicyTests</c>): a later edit that moves the check back to the receive
	/// path would silently re-open the old-layer teleport this ticket exists to close.
	/// </summary>
	[Fact]
	public void CharacterRestoreLayer_IsDecidedWhenThePositionIsApplied()
	{
		const string check = "CharacterRestorePositionPolicy.IsFromAnotherLayer(pending.Generation, _world.CurrentGeneration)";
		var sync = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs"));
		var apply = MethodBody(sync, "private bool ApplyPendingPosition");
		var receive = MethodBody(sync, "private void OnCharacterDataReceived");

		Assert.False(string.IsNullOrEmpty(apply), "ApplyPendingPosition is gone — the position write has no pinned home");
		Assert.False(string.IsNullOrEmpty(receive), "OnCharacterDataReceived is gone");
		Assert.Contains(check, apply, StringComparison.Ordinal);
		Assert.DoesNotContain("CharacterRestorePositionPolicy.IsFromAnotherLayer", receive, StringComparison.Ordinal);
	}

	/// <summary>
	/// A container destination has ONE policy in both directions: the container's own rules on the RECEIVING client
	/// decide, and a refusal falls back to a slot there. The flag that used to send a created item to the give
	/// ladder's drop rung is gone — the host records the item's id as carried BEFORE it publishes the transfer (and
	/// `DeliverCreatedItem`'s carry does the same), so a spawn report from that rung is refused by the kernel as a
	/// conflict and a guest's object is then swept by the world-item keyframe. The independent review of batch
	/// `20261021-b` found that, and the rung was withdrawn rather than shipped.
	/// </summary>
	[Fact]
	public void ContainerDestination_TakesTheSlotFallbackInBothDirections()
	{
		var apply = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs"));
		var placement = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/PlayerInventoryPlacement.cs"));

		Assert.Contains("PlaceCarriedItem(body, item, msg.TargetParentItemId, msg.ExactDestination)", apply);
		Assert.Contains("_placement.AddCarriedItemToLocalContainer(body, item, parentItemId, suppressUnplacedEnd)", apply);
		Assert.DoesNotContain("dropWhenRefused", apply, StringComparison.Ordinal);
		Assert.Contains("TryPlaceInBodySlot(body, item, materialized", placement);
		Assert.DoesNotContain("dropWhenRefused", placement, StringComparison.Ordinal);
	}

	/// <summary>
	/// A container that refuses a handed-over item must hand on the object the restore already materialized.
	/// <c>ItemStateCodec.RestoreContent</c> used to destroy that child and answer null; the caller then
	/// materialized a SECOND object with the same instance id, Unity's deferred destroy reported the id dead at
	/// the end of the frame, the host's kernel stored it TERMINAL and swept the second object — the handed-over
	/// toolbox was gone from both players (batch `20261021-a`, `give-item-to-another-player` row 4). The
	/// behaviour suite cannot host the refusal (it needs real Unity objects and a container that really
	/// refuses), so the invariant is pinned here at the source: the codec never destroys the item it
	/// materialized, the refusal's own flag says "not loaded", the placement's container rung hands THAT object
	/// to the slot rung (never to a rung that creates one), the slot rung verifies its landing, and both the
	/// place and the materialize paths go through the one verified slot loop.
	/// <para>
	/// Its reach, stated exactly: three files' text, member bodies located by their DECLARATION (a call site's
	/// next brace is another member's, so a call is never a body). What it cannot see: a destroy introduced in a
	/// helper the codec calls, and <c>RestoreContents</c>' own discard policy — that bulk path has no destination
	/// to hand a refused child to, and its ticket is separate. The materialized item's own destroy is what is
	/// pinned; the componentless prefab object the codec discards before it becomes an item carries no instance
	/// id and is allowed to stay. The live proof of the fix is the row-4 rerun on the deployed artifact, not this
	/// pin.
	/// </para>
	/// </summary>
	[Fact]
	public void ContainerRefusal_HandsTheMaterializedItemToTheVerifiedSlotRung()
	{
		var failures = ContainerRefusalHandoverFailures(
			File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Items/ItemStateCodec.cs")),
			File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/LocalBodyPlacement.cs"))
				+ File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/PlayerInventoryPlacement.cs")),
			File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Items/RestoredContent.cs")));
		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	/// <summary>The gate's own contract: the fixed shape passes, and every shape it exists to catch fails.</summary>
	[Theory]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { LogWarning(message); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { var item = go.GetComponent<Item>(); item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId; return item; }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return TryPlaceInBodySlot(body, item, materialized); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { foreach (var slot in SlotCandidates(body, item)) { body.PickUpItem(placed, slot, force: true); if (body.GetItem(slot) != placed) { continue; } item.SlotIndex = slot; return true; } return false; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created) { return false; } return TryPlaceInBodySlot(body, item, created); }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, false, why); }",
		0)]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { Object.Destroy(child.gameObject); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { var item = go.GetComponent<Item>(); item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId; return item; }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return TryPlaceInBodySlot(body, item, materialized); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { foreach (var slot in SlotCandidates(body, item)) { body.PickUpItem(placed, slot, force: true); if (body.GetItem(slot) != placed) { continue; } item.SlotIndex = slot; return true; } return false; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created) { return false; } return TryPlaceInBodySlot(body, item, created); }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, false, why); }",
		1)]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { LogWarning(message); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { var item = go.GetComponent<Item>(); item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId; return item; }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return TryPlaceInBodySlot(body, item, materialized); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { foreach (var slot in SlotCandidates(body, item)) { body.PickUpItem(placed, slot, force: true); if (body.GetItem(slot) != placed) { continue; } item.SlotIndex = slot; return true; } return false; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created) { return false; } return TryPlaceInBodySlot(body, item, created); }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, true, why); }",
		1)]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { LogWarning(message); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { var item = go.GetComponent<Item>(); item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId; return item; }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return DropCreatedItemAtLocalBody(body, item); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { foreach (var slot in SlotCandidates(body, item)) { body.PickUpItem(placed, slot, force: true); if (body.GetItem(slot) != placed) { continue; } item.SlotIndex = slot; return true; } return false; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created) { return false; } return TryPlaceInBodySlot(body, item, created); }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, false, why); }",
		2)]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { LogWarning(message); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { var item = go.GetComponent<Item>(); item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId; return item; }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return TryPlaceInBodySlot(body, item, materialized); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { var created = ItemStateCodec.MaterializeItem(item, body.transform.position); return created != null; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created) { return false; } return TryPlaceInBodySlot(body, item, created); }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, false, why); }",
		3)]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { LogWarning(message); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { var item = go.GetComponent<Item>(); item.gameObject.AddComponent<ItemInstanceId>().Id = itemData.InstanceId; return item; }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return TryPlaceInBodySlot(body, item, materialized); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { foreach (var slot in SlotCandidates(body, item)) { body.PickUpItem(placed, slot, force: true); if (body.GetItem(slot) != placed) { continue; } item.SlotIndex = slot; return true; } return false; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { ItemStateCodec.RestoreItem(item, body); return true; }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, false, why); }",
		1)]
	[InlineData(
		"internal static RestoredContent RestoreContent(Item containerItem, Container container, CharacterItemMsg childData) { if (!container.CanHoldItem(child)) { LogWarning(message); return RestoredContent.Refused(child, refused); } return RestoredContent.Landed(child); } internal static Item? MaterializeItem(CharacterItemMsg itemData, Vector3 position) { return go.GetComponent<Item>(); }",
		"internal LocalPlacement AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId) { if (container == null) { return AddCarriedItemToLocalBody(body, item); } var restored = ItemStateCodec.RestoreContent(parent, container, item); if (restored.Item is not { } materialized) { return false; } return TryPlaceInBodySlot(body, item, materialized); } private bool TryPlaceInBodySlot(Body body, CharacterItemMsg item, Item placed) { foreach (var slot in SlotCandidates(body, item)) { body.PickUpItem(placed, slot, force: true); if (body.GetItem(slot) != placed) { continue; } item.SlotIndex = slot; return true; } return false; } internal LocalPlacement AddCarriedItemToLocalBody(Body body, CharacterItemMsg item) { if (ItemStateCodec.MaterializeItem(item, body.transform.position) is not { } created) { return false; } return TryPlaceInBodySlot(body, item, created); }",
		"internal readonly record struct RestoredContent(Item? Item, bool Loaded, string? Refusal) { internal static RestoredContent Refused(Item item, string why) => new(item, false, why); }",
		1)]
	[InlineData("", "", "", 6)]
	public void TheContainerRefusalMatcher_SeesEveryDiscardShapeAndItsPositiveSample(
		string codec, string placement, string restoredContent, int expectedFailures) =>
		Assert.Equal(expectedFailures, ContainerRefusalHandoverFailures(codec, placement, restoredContent).Count);

	private static List<string> ContainerRefusalHandoverFailures(string codec, string placement, string restoredContent)
	{
		var failures = new List<string>();

		var restore = SourceScan.MethodBody(codec, "internal static RestoredContent RestoreContent(");
		if (restore.Length == 0)
		{
			failures.Add("ItemStateCodec.RestoreContent's body was not found — this rule would pass by checking nothing");
		}
		else if (restore.Contains("Destroy(child", StringComparison.Ordinal))
		{
			failures.Add("ItemStateCodec.RestoreContent destroys the content item it materialized: the deferred destroy reports its instance id dead at the end of the frame, and the object a fallback then builds with the same id is swept (batch 20261021-a's handed-over toolbox). The one destroy the method may keep is the componentless prefab object, which carries no Item and therefore no instance id");
		}
		else if (!restore.Contains("RestoredContent.Refused(", StringComparison.Ordinal))
		{
			failures.Add("ItemStateCodec.RestoreContent answers a container refusal without handing the materialized item back — the caller can only rebuild it, which is the same id twice");
		}

		var refusedStart = restoredContent.IndexOf("Refused(Item item, string why)", StringComparison.Ordinal);
		if (refusedStart < 0)
		{
			failures.Add("RestoredContent.Refused was not found — the refusal's own flag is nobody's check");
		}
		else
		{
			var lineEnd = restoredContent.IndexOf('\n', refusedStart);
			var refused = lineEnd < 0 ? restoredContent[refusedStart..] : restoredContent[refusedStart..lineEnd];
			if (!refused.Contains("false", StringComparison.Ordinal) || refused.Contains("true", StringComparison.Ordinal))
			{
				failures.Add("RestoredContent.Refused does not report the refusal as not loaded — a placement that trusts the flag would treat a refused item as already in the container and never place it");
			}
		}

		var container = SourceScan.MethodBody(placement, "internal LocalPlacement AddCarriedItemToLocalContainer(");
		if (container.Length == 0)
		{
			failures.Add("PlayerInventoryPlacement.AddCarriedItemToLocalContainer's body was not found — this rule would pass by checking nothing");
		}
		else
		{
			if (!container.Contains("ItemStateCodec.RestoreContent(", StringComparison.Ordinal))
			{
				failures.Add("AddCarriedItemToLocalContainer no longer loads the container through ItemStateCodec.RestoreContent — the container's own rules are the judge, and this rule cannot see what replaced them");
			}

			if (!container.Contains("TryPlaceInBodySlot(body, item, materialized", StringComparison.Ordinal))
			{
				failures.Add("AddCarriedItemToLocalContainer's slot fallback does not place the object RestoreContent materialized — a rung that materializes from the message builds the same instance id twice");
			}

			if (container.Contains("DropCreatedItemAtLocalBody(", StringComparison.Ordinal))
			{
				failures.Add("AddCarriedItemToLocalContainer answers a refusal with a world drop the kernel refuses: the host has already recorded the id as carried, so the spawn is a conflict and a guest's copy is swept by the keyframe");
			}
		}

		// The loop's single home is LocalBodyPlacement (the restore's unplaced row uses it too, so the
		// give ladder and the restore cannot disagree about what a body accepts); the scan reads the
		// placement sources with that file first, and the method it names is the one carrying the loop.
		var slot = SourceScan.MethodBody(placement, "bool TryPlaceInBodySlot(");
		if (slot.Length == 0)
		{
			failures.Add("the placement's TryPlaceInBodySlot body was not found — this rule would pass by checking nothing");
		}
		else
		{
			if (!slot.Contains("body.PickUpItem(placed", StringComparison.Ordinal))
			{
				failures.Add("TryPlaceInBodySlot does not hand the object it was given to the body — it has to place that object, never create one");
			}

			if (!slot.Contains("body.GetItem(slot) != placed", StringComparison.Ordinal))
			{
				failures.Add("TryPlaceInBodySlot does not read the slot back: the game's own placement refuses an onlyHoldInHands item outside a hand slot even with force, so an unverified attempt logs a slot the item never entered");
			}

			if (slot.Contains("MaterializeItem(", StringComparison.Ordinal)
				|| slot.Contains("Instantiate(", StringComparison.Ordinal)
				|| slot.Contains("Destroy(", StringComparison.Ordinal))
			{
				failures.Add("TryPlaceInBodySlot creates or destroys an object instead of placing the one it was given — one instance id, one object");
			}
		}

		var carried = SourceScan.MethodBody(placement, "internal LocalPlacement AddCarriedItemToLocalBody(");
		if (carried.Length == 0)
		{
			failures.Add("PlayerInventoryPlacement.AddCarriedItemToLocalBody's body was not found — this rule would pass by checking nothing");
		}
		else if (carried.Contains("ItemStateCodec.RestoreItem(", StringComparison.Ordinal))
		{
			failures.Add("AddCarriedItemToLocalBody hands the placement back to RestoreItem, whose hand-off has no landing check — the shape the refusal fallback used to rebuild the same id through");
		}
		else if (!carried.Contains("ItemStateCodec.MaterializeItem(", StringComparison.Ordinal)
			|| !carried.Contains("TryPlaceInBodySlot(body, item, created", StringComparison.Ordinal))
		{
			failures.Add("AddCarriedItemToLocalBody no longer materializes and then places through the verified slot path");
		}

		var materialize = SourceScan.MethodBody(codec, "internal static Item? MaterializeItem(");
		if (materialize.Length == 0)
		{
			failures.Add("ItemStateCodec.MaterializeItem was not found — the placement builds its object through it, and an id-less materialization is reported as a new spawn");
		}
		else if (!materialize.Contains("AddComponent<ItemInstanceId>()", StringComparison.Ordinal))
		{
			failures.Add("ItemStateCodec.MaterializeItem does not stamp the instance id: the object's own Start would report it as a runtime spawn with a NEW id");
		}

		return failures;
	}

	/// <summary>
	/// The enemy roster adoption's adapter wiring: the host builds its published
	/// states from the BOUND table (a scan-only list drops a live enemy's id when
	/// the scan misses it), the guest's adoption goes through the pure judgment and
	/// destroys surplus only against a COMPLETE roster, and an EMPTY roster is
	/// authoritative (the old <c>if (_enemies.Count == 0) return;</c> kept every
	/// member's divergent local set). The test assembly cannot compile against the
	/// adapter (<c>ExcludeAssets="compile"</c>), so the wiring is pinned here by
	/// source; the per-row judgment is pinned by <c>EnemyRosterAdoptionTests</c>
	/// and the snapshot semantics by <c>EnemySnapshotRecoveryTests</c>.
	/// </summary>
	[Fact]
	public void EnemyRosterAdoption_IsTheOnlyMatch_AndTheRosterGatesArePinned()
	{
		var binder = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyRosterBinder.cs"));
		var coordinator = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs"))
			+ binder;
		var bridge = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs"));
		var service = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySyncService.cs"));
		var publisher = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemyRosterPublisher.cs"));
		var handler = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Session/Handlers/EnemySnapshotHandler.cs"));
		var message = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/EnemySnapshotMsg.cs"));
		var stateMessage = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/EnemyStateMsg.cs"));

		Assert.True(coordinator.Contains("EnemyRosterAdoption.Plan(", StringComparison.Ordinal), "the guest's adoption must go through the pure EnemyRosterAdoption.Plan — a second matching rule is the rejected design");
		Assert.True(coordinator.Contains("EnemyRosterAdoption.FindLiveCopy(", StringComparison.Ordinal), "the live 20 Hz adoption rule must be the pure FindLiveCopy, not an untested second loop");
		Assert.True(binder.Contains("TryGetEnemyFacts(state.EntityId, out var prefab, out var runtimeSpawned)", StringComparison.Ordinal), "the live pass must take BOTH of its identity inputs — the prefab and the runtime marker — from the same kernel fact: the 20 Hz frame carries presentation only, so a buffer row for an id no snapshot has introduced reads RuntimeSpawned=false, and `FindLiveCopy` would then refuse the creation-channel copy that is the one bindable copy (batch 20261021-e, row 4)");
		Assert.True(service.Contains("public bool TryGetEnemyFacts(NetworkEntityId id, out string prefabId, out bool runtimeSpawned)", StringComparison.Ordinal), "the service must expose the kernel fact's two identity columns TOGETHER — a prefab-only accessor is the shape that let the live pass take the runtime marker from the presentation buffer");
		Assert.True(binder.Contains("EnemyRosterAdoption.FindLiveCopy(state.Position, prefab, key, runtimeSpawned, copies, used)", StringComparison.Ordinal), "the fact's runtime marker must be the judgement's allowKeyedCopy argument — a call kept intact with the marker dropped would leave the old bug's shape in place");
		Assert.False(binder.Contains("state.RuntimeSpawned", StringComparison.Ordinal), "the live pass must not read the runtime marker off the stream buffer row: that field is a snapshot-only column, default-false for a row the stream announced first");
		Assert.False(coordinator.Contains("TryPairGeneratedCopies", StringComparison.Ordinal), "the all-or-nothing generated-set pairing is deleted; its return would restore the rejected scheme");
		Assert.False(coordinator.Contains("EnemySpawnArbitration", StringComparison.Ordinal), "EnemySpawnArbitration is deleted; a reference here means the position-as-identity scheme came back");
		Assert.False(coordinator.Contains("EnemyRuntimeSpawnArbitration", StringComparison.Ordinal), "the runtime-spawn positional arbitration is deleted; a reference here means the second identity scheme came back");
		Assert.True(coordinator.Contains("foreach (var (id, entity) in _bindings.Bound)", StringComparison.Ordinal), "the host's states must come from the bound table, not a one-frame scene scan: a scan that misses a live enemy would drop its id and its row");
		Assert.True(coordinator.Contains("var unresolvedCount = plan.Unresolved.Count + failed;", StringComparison.Ordinal), "the destroy gate must count BOTH unresolved rows and rows whose materialization failed — a failed row may be the only local copy of the enemy it could not create");
		Assert.True(coordinator.Contains("if (unresolvedCount > 0)", StringComparison.Ordinal), "the destroy gate must use the combined completeness condition, not the plan's unresolved set alone");
		Assert.True(coordinator.Contains("EnemyRosterReset +=", StringComparison.Ordinal), "the adapter must drop its bindings when the host resets the roster for a new layer");
		Assert.True(coordinator.Contains("CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating())", StringComparison.Ordinal), "the adoption must be gated on a SETTLED world: the regeneration fade is not IsGenerating and a copy of the scene being replaced must not be adopted");
		Assert.True(bridge.Contains("domains.EnemySync.OnWorldGenerationStarted()", StringComparison.Ordinal), "the generation boundary must reset the roster state on both roles");
		Assert.False(publisher.Contains("if (_enemies.Count == 0)", StringComparison.Ordinal), "the empty roster is authoritative — suppressing it kept every member's divergent local set");
		Assert.True(publisher.Contains("Generation = _generations.Stamp()", StringComparison.Ordinal), "a roster must carry the world/layer generation it was derived in");
		Assert.True(publisher.Contains("FlushHeld();", StringComparison.Ordinal), "a held snapshot must be flushed at the ready transition — its member's entry window may already be closed");
		Assert.True(publisher.Contains("_heldPeers.Add(steamId)", StringComparison.Ordinal), "the held peers must be recorded for that flush");
		Assert.True(handler.Contains("ApplyEnemySnapshot(sender, msg)", StringComparison.Ordinal), "the handler must pass the sender so a stale roster's refusal names it");
		Assert.True(message.Contains("WorldGenerationMsg? Generation", StringComparison.Ordinal), "the roster snapshot must carry its generation stamp");
		Assert.True(stateMessage.Contains("public string PrefabId { get; set; }", StringComparison.Ordinal), "each roster row must carry its prefab: a deferred adoption cannot rely on a stream frame keeping the kernel-restored prefab");
	}

	/// <summary>
	/// The host's capture waits for a SETTLED world, and the runtime-spawn classification is the
	/// generation's finished EDGE rather than the generating flag read at an animal's <c>Start</c>.
	///
	/// Batch `20261021-f`, session A: the host's capture ran during generation, so 83 rows existed
	/// when the world-entry layer reset fired (<c>[LayerReset] dropped the previous layer's 83 enemy
	/// row(s)</c>), and that reset cleared both members' tracked adoption candidates after their own
	/// generation — the entry adoption built 946 copies of a 956-row world, left ten traders
	/// unresolved and read <c>aligned=False</c> until the next repair. The same run reported 291 →
	/// 873 <c>runtime spawns</c> for a roster of generation output: a dense world's generator leaves
	/// the <c>Start</c> calls to run over the seconds after <c>generatingWorld</c> flips, so the flag
	/// read at <c>Start</c> is not the creation fact. The adapter is not compilable in this suite, so
	/// the wiring is pinned by source; the pure classification matrix is in
	/// <c>EnemyRosterAdoptionTests</c> and the behaviour on a real world is the batch's job.
	/// </summary>
	[Fact]
	public void EnemyRosterCapture_WaitsForASettledWorld_AndTheGenerationEdgeOwnsTheRuntimeClassification()
	{
		var coordinator = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs"));
		var binder = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyRosterBinder.cs"));

		var host = SourceScan.MethodBody(coordinator, "private void UpdateHost(");
		Assert.False(string.IsNullOrEmpty(host), "the host's frame pump (UpdateHost) was not found — this rule would pass by checking nothing");
		Assert.True(host.Contains("WorldIsSettled()", StringComparison.Ordinal),
			"the host must ask the settled-world verdict before it captures");
		var unstable = host.IndexOf("MarkRosterUnstable();", StringComparison.Ordinal);
		var earlyReturn = host.IndexOf("return;", StringComparison.Ordinal);
		var capture = host.IndexOf("CaptureHostEnemies();", StringComparison.Ordinal);
		Assert.True(unstable >= 0 && earlyReturn > unstable,
			"the not-settled branch must mark the roster unstable and RETURN: without the early return the capture still runs while the layer generates");
		Assert.True(capture > earlyReturn,
			"the host must NOT capture while its world is generating or regenerating — a row bound during generation is a row the world-entry layer reset must drop (batch 20261021-f, row 1: 83 rows dropped at entry, both members' candidates cleared, 946 copies built, aligned=False)");

		var verdict = coordinator.IndexOf("private static bool WorldIsSettled() =>", StringComparison.Ordinal);
		Assert.True(verdict >= 0, "the settled-world verdict method was not found");
		var expression = coordinator.IndexOf(
			"EnemyRosterAdoption.CanPublishRoster(HarmonyTraverse.HasWorld, HarmonyTraverse.IsGenerating(), HarmonyTraverse.IsRegenerating())",
			verdict,
			StringComparison.Ordinal);
		Assert.True(expression > verdict && expression - verdict < 200,
			"the capture gate must be the SAME settled-world verdict the publication uses (CanPublishRoster), not a second notion of readiness");

		var edge = SourceScan.MethodBody(coordinator, "private void MarkAnimalsCreatedByGeneration(");
		Assert.False(string.IsNullOrEmpty(edge), "the generation-finished edge pass was not found — nothing marks the animals that already existed when the world settled");
		Assert.True(edge.Contains("LocalAnimals()", StringComparison.Ordinal) && edge.Contains("_generationEdgeObserved = true", StringComparison.Ordinal),
			"the edge pass must record every animal the scene holds at the settled edge (LocalAnimals: the tracked set ∪ the scan) and that the edge was seen");
		Assert.True(host.Contains("MarkAnimalsCreatedByGeneration();", StringComparison.Ordinal),
			"the host must run the edge pass over the frame that binds the layer, so its capture classifies the generation correctly");

		var guest = SourceScan.MethodBody(coordinator, "private void UpdateGuest(");
		Assert.False(string.IsNullOrEmpty(guest), "the guest's frame pump (UpdateGuest) was not found");
		Assert.True(guest.Contains("MarkAnimalsCreatedByGeneration();", StringComparison.Ordinal),
			"the guest must run the edge pass at its first settled frame too — it freezes runtime copies and adopts rows through the same facts");

		var started = SourceScan.MethodBody(coordinator, "internal bool OnAnimalInstantiated(");
		Assert.False(string.IsNullOrEmpty(started), "OnAnimalInstantiated's body was not found with its generation verdict");
		Assert.False(started.Contains("IsGenerating()", StringComparison.Ordinal),
			"the runtime-spawn classification must NOT read the generating flag at the animal's Start: a dense world's generator leaves hundreds of Start calls to run after the flag flips, and every one of them read as a runtime spawn (batch 20261021-f: 291 → 873 on a 956-animal layer)");
		Assert.True(started.Contains("EnemyRosterAdoption.IsRuntimeSpawn(_session.SessionActive, _generationEdgeObserved, _generationAnimals.Contains(entity))", StringComparison.Ordinal),
			"the classification must be the pure IsRuntimeSpawn over the edge facts (session active, edge observed, existed at the edge) — an animal that appears after the edge is a runtime spawn, one that existed at or before it is generation output");

		var adopt = SourceScan.MethodBody(binder, "internal void AdoptRoster(");
		Assert.False(string.IsNullOrEmpty(adopt), "AdoptRoster's body was not found");
		Assert.True(adopt.Contains("EnemyAnimalScan.Union(_localAnimals)", StringComparison.Ordinal),
			"the adoption's candidate list must be the tracked set ∪ a LIVE scene scan: a roster reset clears the tracked set while the copies are still in the scene, and reading only the tracked set built 946 copies on a 956-row world (batch 20261021-f, row 1)");

		var scanPath = RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyAnimalScan.cs");
		Assert.True(File.Exists(scanPath),
			"the one local-animal union (tracked set ∪ scene scan) must live in EnemyAnimalScan — a second copy of the scan is how the host's capture and the guest's adoption drift apart");
		Assert.True(File.ReadAllText(scanPath).Contains("FindObjectsOfType<BuildingEntity>()", StringComparison.Ordinal),
			"EnemyAnimalScan must actually scan the scene: without the scan a reset-empty tracked set stays empty");

		var update = SourceScan.MethodBody(coordinator, "internal void Update(");
		Assert.False(string.IsNullOrEmpty(update), "Update's body was not found");
		Assert.True(update.Contains("UpdateHost();", StringComparison.Ordinal) && update.Contains("UpdateGuest();", StringComparison.Ordinal),
			"the frame pump must dispatch to the two role pumps — a capture left inline in Update could run before the settled gate again");
		var edgeCall = host.IndexOf("MarkAnimalsCreatedByGeneration();", StringComparison.Ordinal);
		Assert.True(edgeCall >= 0 && edgeCall < capture,
			"the edge pass must run BEFORE the capture in the same frame: the capture classifies the animals it binds from that set");

		var destroy = SourceScan.MethodBody(binder, "internal void DestroyCopy(");
		Assert.False(string.IsNullOrEmpty(destroy), "the one copy-destruction path (DestroyCopy) was not found");
		Assert.True(destroy.Contains("entity.gameObject.SetActive(false);", StringComparison.Ordinal),
			"a copy being destroyed must be DEACTIVATED first: Unity destroys at the end of the frame, and the adoption's scene scan would otherwise adopt a copy this same pass unbound as a different row, logging aligned while the enemy vanishes at frame end");
		foreach (var path in new[] { "private int RemoveBoundEnemiesNotInRoster(", "private void DestroySurplus(" })
		{
			var body = SourceScan.MethodBody(binder, path);
			Assert.False(string.IsNullOrEmpty(body), $"{path}'s body was not found");
			Assert.True(body.Contains("DestroyCopy(entity);", StringComparison.Ordinal),
				$"{path} must destroy through DestroyCopy — a raw Object.Destroy leaves the doomed copy visible to the same frame's scene scan");
			Assert.False(body.Contains("Object.Destroy(", StringComparison.Ordinal),
				$"{path} must not call Object.Destroy itself: the deactivation is the part that hides the copy from the scan");
		}

		Assert.True(coordinator.Contains("_binder.DestroyCopy(entity);", StringComparison.Ordinal),
			"the removed-enemy path must destroy through the same deactivating call");

		var bridge = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs"));
		Assert.True(bridge.Contains("var generationAnimal = entity.animal && domains.EnemySync.OnAnimalInstantiated(entity);", StringComparison.Ordinal)
			&& bridge.Contains("domains.EntitySpawnSync.OnEntityInstantiated(entity, generationAnimal)", StringComparison.Ordinal),
			"the creation channel must receive the enemy domain's generation verdict: a generation animal's deferred Start was otherwise reported as a runtime creation and materialized a second time on every member");
		var spawn = SourceScan.MethodBody(
			File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs")),
			"internal void OnEntityInstantiated(BuildingEntity entity, bool createdByGeneration)");
		Assert.False(string.IsNullOrEmpty(spawn), "EntitySpawnSync.OnEntityInstantiated's body was not found with the generation fact parameter");
		Assert.True(spawn.Contains("if (createdByGeneration)", StringComparison.Ordinal),
			"the creation channel must skip a generation copy by the passed fact — its own generating-flag read is the shape that misreported deferred Starts");
	}

	/// <summary>
	/// The host's run phase around a session that rises LATE: a host that played solo and opened the
	/// lobby from inside that running world must be marked playing. Otherwise the loading-screen
	/// keeper (<c>ShouldKeep = SessionActive &amp;&amp; !IsPlaying</c>) pulls the native loading screen up
	/// and pins it over the running game for the rest of the session — the host plays blind while the
	/// members enter the same world (batch `20261021-e`, session B: the host log shows
	/// <c>[Gate] keeping the loading screen up while waiting for the host</c> at 19:28:45.502, half a
	/// second after the lobby was created, and no line ever releases it). Both existing
	/// <c>MarkPlayingForHost</c> call sites are unreachable for that run: the world-entry edge was
	/// taken with no session (a solo run), and the gate-release branch requires an armed gate. The
	/// adapter is not compilable in this suite, so the pump is pinned by source; the batch reads the
	/// line and the keeper's absence.
	/// </summary>
	[Fact]
	public void RunLifecycle_ResumesAHostWhoseSessionRoseAroundARunningWorld()
	{
		var gate = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Run/StartGateCoordinator.cs"));
		var run = File.ReadAllText(RepositoryPaths.File("src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs"));

		Assert.True(gate.Contains("ResumeHostPlayingIfTheSessionRoseAroundTheRun();", StringComparison.Ordinal),
			"the gate presentation pump must ask every frame whether a session rose around an already-running world — without it the host's phase stays Idle and the keeper never lets go");

		var body = SourceScan.MethodBody(gate, "private void ResumeHostPlayingIfTheSessionRoseAroundTheRun(");
		Assert.False(string.IsNullOrEmpty(body), "the host-resume pump's body was not found — this rule would pass by checking nothing");
		foreach (var guard in new[] { "_session.Role != SessionRole.Host", "!_session.SessionActive", "!_session.LocalInWorld", "_run.IsPlaying", "_world.StartGateActive" })
		{
			Assert.True(body!.Contains(guard, StringComparison.Ordinal),
				$"the host-resume pump must only fire for a host already in the world, not yet playing and with no armed gate (missing `{guard}`): an armed gate's release owns the phase");
		}

		Assert.True(body!.Contains("_run.MarkPlayingForHost();", StringComparison.Ordinal),
			"the pump must mark the host playing — that is the fact the loading-screen keeper reads, not a presentation call of its own");

		Assert.True(run.Contains("internal void MarkPlayingForHost()", StringComparison.Ordinal),
			"the phase transition the pump calls must stay the run's own (MarkPlayingForHost), not a second write of the phase from the presentation layer");
		Assert.True(gate.Contains("keeper.ShouldKeep = () => _session.SessionActive && !_run.IsPlaying;", StringComparison.Ordinal),
			"the keeper's release condition changed shape — re-judge the host-resume pump against whatever it reads now");
	}
}
