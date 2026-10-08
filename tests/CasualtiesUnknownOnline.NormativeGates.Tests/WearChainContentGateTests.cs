using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The cross-player WEAR chain's content gate (ticket
/// <c>mod-cross-player-native-semantics</c>, Part B's wear chain).
///
/// <para>
/// The chain's whole point is that "is this a wearable, and where does it go" is the
/// game's own item data (<c>ItemInfo.wearable</c> / <c>desiredWearLimb</c> /
/// <c>wearSlotId</c>) read through the content seam — not a hand-written id table
/// transcribed out of <c>Item.SetupItems()</c>. The table that was deleted
/// (<c>RemoteWearCatalog</c>) carried 40 vanilla ids and hard-coded limb indices, so
/// every mod garment and every vanilla wearable it missed could be carried, dropped
/// and saved but never put on another player. This gate is the one that keeps a
/// successor table from growing back: the chain's OWN Runtime sources may hold no
/// id-keyed table at all, under any filename, and the directory census reports the
/// remaining chains' tables so a stale scan cannot pass by finding nothing.
/// </para>
///
/// <para>
/// Two things are checked, and the difference matters. The FIRST is a source shape
/// (a dictionary initializer whose keys are string literals IS an id table) with the
/// matcher pinned by samples below. The SECOND is the capability contract: the
/// adapter's answer must read <c>Item.GlobalItems</c>, because that dictionary is
/// what mod content registers into — a chain that answered from anywhere else would
/// silently re-create the ceiling the ticket exists to remove. What no source gate
/// can read is whether a live session admits the right garments; that is the
/// acceptance batch's row, and the self-check names it as a limit.
/// </para>
/// </summary>
public class WearChainContentGateTests
{
	private const string SemanticsFile = "src/CasualtiesUnknownOnline.GameAdapter/Content/GameWearFacts.cs";

	private const string RuntimeSeamFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/IWearSemantics.cs";

	/// <summary>
	/// The chain's OWN Runtime sources — the seam, its default and the one rule plus
	/// the placement. These are what must hold no id-keyed table, whatever they are
	/// named later, so a successor table cannot slip in under a new filename; the
	/// deleted <c>RemoteWearCatalog.cs</c> was simply where the old one lived.
	/// </summary>
	private static readonly string[] WearChainSources =
	[
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/IWearSemantics.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/NoWearSemantics.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/WearAdmission.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteWearApplication.cs",
	];

	/// <summary>The Runtime directory the census below scans: every family's catalog lived here, so the count of files still carrying one is the migration's own progress.</summary>
	private const string RuntimeInteractionDirectory = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction";

	/// <summary>
	/// An id table's shape: a collection initializer whose keys are string literals.
	/// Deliberately narrow — it reads the KEYED INITIALIZER, so a dictionary built at
	/// runtime (from mod content, from a file) is not a table and a single string
	/// constant is not a table. Samples below pin both directions.
	/// </summary>
	private static readonly Regex IdTableInitializer = new(
		@"\[\s*""[^""]+""\s*\]\s*=",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// The chains that still carry an id-keyed table, by filename — the migration's
	/// remaining work, listed EXACTLY rather than as a floor, so growth is visible
	/// too: a new catalog beside these fails this gate, and so does a migration that
	/// deletes one without updating the list. Measured 2026-10-08, the cycle the
	/// SOLID-FOOD chain's table left it (limb tool, heal profiles, the bandage
	/// minigame's items and the treatment-sound table).
	/// </summary>
	private static readonly string[] PendingCatalogFiles =
	[
		"RemoteBandageMinigameCatalog.cs",
		"RemoteHealProfiles.cs",
		"RemoteLimbToolCatalog.cs",
		"RemoteMedicalTreatmentSoundCatalog.cs",
	];
	[Fact]
	public void TheRuntimeWearChain_HoldsNoIdKeyedItemTable()
	{
		var withTables = WearChainSources
			.Where(file => IdTableInitializer.IsMatch(RepositoryPaths.ReadText(file)))
			.ToList();

		Assert.True(
			withTables.Count == 0,
			$"the wear chain's Runtime source(s) [{string.Join(", ", withTables)}] declare an id-keyed table initializer — the chain's answer must stay the game's own item data (`ItemInfo.wearable` / `desiredWearLimb` / `wearSlotId` through `GameWearPlacement`), never a transcribed id table; the deleted `RemoteWearCatalog` was that table, and its rows are what kept every mod garment and every vanilla wearable it missed off another player");

		// The scan must have READ something: a renamed source would make the loop above
		// check nothing at all and pass. Reading a missing file throws instead, and the
		// census below is an EXACT set, so a stale scan surface fails rather than
		// shrinking quietly.
		var files = Directory
			.GetFiles(RepositoryPaths.File(RuntimeInteractionDirectory), "*.cs", SearchOption.AllDirectories)
			.OrderBy(path => path, StringComparer.Ordinal)
			.ToList();

		Assert.True(files.Count > 0, $"{RuntimeInteractionDirectory} holds no .cs file — the scan surface is stale");

		var pendingCatalogs = files
			.Where(file => IdTableInitializer.IsMatch(File.ReadAllText(file)))
			.Select(Path.GetFileName)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToList();

		Assert.Equal(
			PendingCatalogFiles,
			pendingCatalogs);
	}

	/// <summary>
	/// The capability contract, read from the SYNTAX rather than the file text: the
	/// adapter's placement must resolve the item out of <c>Item.GlobalItems</c> and
	/// judge it on <c>ItemInfo.wearable</c>, and the seam must declare the placement
	/// question with both halves the Runtime needs. A file-text <c>Contains</c> would
	/// be satisfied by the doc comment alone — the first draft of this gate was, and
	/// the independent review said so — so the check walks member accesses and
	/// identifiers in CODE.
	/// </summary>
	[Fact]
	public void TheWearSeamsAnswer_IsTheGamesOwnItemRegistry()
	{
		var placement = CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(SemanticsFile), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();

		Assert.Contains(
			placement.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "Item.GlobalItems");
		Assert.Contains(
			placement.DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "wearable");
		Assert.Contains(
			placement.DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "desiredWearLimb");
		Assert.Contains(
			placement.DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "wearSlotId");

		var seam = CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(RuntimeSeamFile), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();
		var question = seam.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.Single(method => method.Identifier.ValueText == "TryGetWearPlacement");

		Assert.Contains(
			question.ParameterList.Parameters,
			parameter => parameter.Identifier.ValueText == "limbIndex");
		Assert.Contains(
			question.ParameterList.Parameters,
			parameter => parameter.Identifier.ValueText == "wearSlotId");
	}

	[Theory]
	[InlineData("private static readonly Dictionary<string, P> R = new(StringComparer.Ordinal) { [\"a\"] = new(1), [\"b\"] = new(2) };", true)]
	// The deleted table's own shape — one keyed initializer per row.
	[InlineData("[\"bikehelmet\"] = new(\"bikehelmet\", \"hat\", 0),", true)]
	// Not a table: a dictionary filled from content at runtime.
	[InlineData("foreach (var pair in registry) { map[pair.Key] = pair.Value; }", false)]
	// Not a table: a single constant, and the seam's own signature.
	[InlineData("public bool TryGetWearPlacement(string itemId, out int limbIndex, out string wearSlotId);", false)]
	[InlineData("private const string WearSlot = \"hat\";", false)]
	// Not a table: a constant subscript read.
	[InlineData("var first = known[0];", false)]
	public void TheIdTableMatcher_ReadsAKeyedInitializer(string snippet, bool expected) =>
		Assert.Equal(expected, IdTableInitializer.IsMatch(snippet));
}
