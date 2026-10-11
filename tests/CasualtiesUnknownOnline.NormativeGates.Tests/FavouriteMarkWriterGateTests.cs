using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The display-proxy favourite gate (user ruling 2026-10-11, ticket
/// <c>favourite-marks-are-local-and-saved</c>).
///
/// <para>
/// A favourite mark belongs to the player who made it, so the star this client shows on ANOTHER
/// player's item is this player's own mark — never the owner's answer. Before the ruling the
/// projection carried the owner's <c>favourited</c> as a source value
/// (<c>RemoteItemPresentation.ApplySourceValues</c> wrote it onto every clone) and a keystroke in
/// somebody else's backpack was turned into a <c>ToggleFavourite</c> intent the owner replayed on its
/// own item. Both halves are gone; what keeps them gone is that the display domain has exactly one
/// writer of that field, <c>FavouriteMarkOverlay</c>, and it writes the LOCAL mark store's answer.
/// </para>
///
/// <para>
/// The scan surface is the clone/display domain directory
/// (<c>src/CasualtiesUnknownOnline.GameAdapter/Character</c>) rather than a hand-picked file list, so a
/// new display file lands inside the rule instead of beside it. The allowed writers are named with
/// their reason in <see cref="AllowedWriters"/>, the census holds a floor so a renamed or emptied scan
/// fails loudly, and the wiring half asserts that the two seams that must use the writer still do: the
/// clone renderer decides the star when it gives a proxy its authoritative identity, and the
/// while-dragging gesture writes the local set instead of sending anything to the owner.
/// </para>
///
/// <para>
/// What is NOT reached: whether the star is drawn correctly at runtime — the seams are Harmony-bound
/// to the game's own <c>Item</c>/<c>InvButton</c>, which no test can instantiate, so the three-client
/// acceptance batch is the half that reads the operator's star, the owner's untouched item and the
/// third peer's unchanged view. The matcher reads a source SHAPE (an assignment whose target member is
/// <c>favourited</c>) and is pinned here with positive and negative samples; it cannot read what the
/// right-hand side evaluates to beyond the writer sample below.
/// </para>
/// </summary>
public class FavouriteMarkWriterGateTests
{
	private const string DisplayDirectory = "src/CasualtiesUnknownOnline.GameAdapter/Character";

	private const string CloneRendererFile = DisplayDirectory + "/CloneInventoryRenderer.cs";

	private const string WhileDraggingPatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/PlayerCameraHandleWhileDraggingPatch.cs";

	/// <summary>
	/// The files of the display domain that may write <c>favourited</c>, each with the reason it is
	/// allowed: the overlay (a display proxy's ONLY writer, and the local mark store's answer is what it
	/// writes), the adoption (the item's own flag, written once when the marked item becomes the local
	/// player's — requirement 3), and the wearable restorer (a REAL worn item restored onto the local
	/// body from the authoritative snapshot, which is not a proxy at all). A fourth entry needs the same
	/// argument made here, which is the point.
	/// </summary>
	private static readonly Dictionary<string, string> AllowedWriters = new(StringComparer.Ordinal)
	{
		[DisplayDirectory + "/FavouriteMarkOverlay.cs"] = "the display proxy's one writer — it writes the local mark store's answer",
		[DisplayDirectory + "/FavouriteMarkAdoption.cs"] = "the marked item's own flag, written when the item becomes the local player's",
		[DisplayDirectory + "/WearableRestorer.cs"] = "a real worn item restored onto the local body, not a display proxy",
	};

	/// <summary>How many allowed writers must exist for the census to mean anything.</summary>
	private const int WriterFloor = 3;

	/// <summary>The remote-inventory intent vocabulary — the wire enum a favourite mark must never appear in again.</summary>
	private const string IntentVocabularyFile = "src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/RemoteInventoryIntentKind.cs";

	/// <summary>How many members the vocabulary must still declare for the scan to mean anything (sixteen at the ruling, with the hole at 16).</summary>
	private const int IntentVocabularyFloor = 10;

	[Fact]
	public void TheIntentVocabulary_NamesNoFavouriteMember()
	{
		var names = FavouriteMembers(RepositoryPaths.ReadText(IntentVocabularyFile));
		var vocabulary = Parse(RepositoryPaths.ReadText(IntentVocabularyFile)).DescendantNodes()
			.OfType<EnumMemberDeclarationSyntax>()
			.Count();

		Assert.True(
			vocabulary >= IntentVocabularyFloor,
			$"{IntentVocabularyFile} declares {vocabulary} member(s), below the floor of {IntentVocabularyFloor} — the vocabulary moved or the scan surface is stale, and a favourite kind could return unseen");

		Assert.True(
			names.Count == 0,
			$"{IntentVocabularyFile} declares {string.Join(", ", names)}: a favourite mark belongs to the player who made it (user ruling 2026-10-11, decision 258), so no intent kind may carry it to the item's owner again. The gesture writes the local set instead (`LocalFavouriteMarkStore.Current.Toggle`)");
	}

	[Theory]
	[InlineData("enum E { Drain = 10, ToggleFavourite = 16 }", 1)]
	[InlineData("enum E { Drain = 10, GiveToTrader = 17 }", 0)]
	[InlineData("// enum E { ToggleFavourite = 16 }", 0)]
	[InlineData("/* enum E { ToggleFavourite = 16 } */", 0)]
	[InlineData("enum E { Drain = 10 } // the favourite store is local", 0)]
	public void TheVocabularyMatcher_ReadsEnumMembersAndNotProse(string snippet, int expected) =>
		Assert.Equal(expected, FavouriteMembers(snippet).Count);

	/// <summary>The enum member names that mention a favourite, read from the syntax tree — prose is not a member.</summary>
	private static List<string> FavouriteMembers(string source) =>
		[.. Parse(source).DescendantNodes()
			.OfType<EnumMemberDeclarationSyntax>()
			.Select(member => member.Identifier.ValueText)
			.Where(name => name.Contains("Favourite", StringComparison.OrdinalIgnoreCase))];

	[Fact]
	public void OnlyTheNamedWriters_TouchAFavouriteFieldInTheDisplayDomain()
	{
		var writers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (var path in SourceScan.SourceFiles(RepositoryPaths.File(DisplayDirectory)))
		{
			var writes = FavouritedWrites(File.ReadAllText(path));
			if (writes.Count > 0)
			{
				writers[Relative(path)] = writes;
			}
		}

		Assert.True(
			writers.Count >= WriterFloor,
			$"the display domain declares {writers.Count} writer(s) of `favourited`, below the floor of {WriterFloor} — either the mark overlay moved out of this directory, or the scan surface is stale; both need this gate updated in the same change (found: {string.Join(", ", writers.Keys)})");

		var unexpected = writers.Keys.Where(path => !AllowedWriters.ContainsKey(path)).ToList();
		Assert.True(
			unexpected.Count == 0,
			$"{string.Join(", ", unexpected)} write(s) a `favourited` field in the display domain, and the star on another player's item is the LOCAL player's own mark (user ruling 2026-10-11): a display proxy's field has exactly one writer, `FavouriteMarkOverlay`, and the owner's own value must never reach it. If this write is legitimate, name its reason in this gate's allowed set");

		var missing = AllowedWriters.Keys.Where(path => !writers.ContainsKey(path)).ToList();
		Assert.True(
			missing.Count == 0,
			$"{string.Join(", ", missing)} no longer write(s) a `favourited` field — the allowed set is a claim about where the field is decided, so a writer that stopped writing means the mechanism moved and this gate must follow it");
	}

	[Fact]
	public void TheMarkOverlay_IsWhatWritesTheProxyAndTheLocalSet()
	{
		var overlay = RepositoryPaths.ReadText(DisplayDirectory + "/FavouriteMarkOverlay.cs");
		Assert.Contains("LocalFavouriteMarkStore", overlay, StringComparison.Ordinal);

		var renderer = RepositoryPaths.ReadText(CloneRendererFile);
		Assert.Contains("FavouriteMarkOverlay.Apply", renderer, StringComparison.Ordinal);

		var gesture = RepositoryPaths.ReadText(WhileDraggingPatchFile);
		Assert.Contains("LocalFavouriteMarkStore.Current.Toggle", gesture, StringComparison.Ordinal);
		Assert.DoesNotContain("CaptureFavourite", gesture, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("class C { void M(Item item, bool marked) { item.favourited = marked; } }", 1)]
	// The shape the ruling retired: the owner's own snapshot value written onto a display proxy.
	[InlineData("class C { void M(Item item, bool ownerValue) { item.favourited = ownerValue; } }", 1)]
	[InlineData("class C { void M(Item item) { item.favourited = !item.favourited; } }", 1)]
	[InlineData("class C { void M(Item item, Item other) { if (item.favourited == other.favourited) { } } }", 0)]
	[InlineData("class C { void M(Item item) { var favourited = item.favourited; } }", 0)]
	[InlineData("// item.favourited = data.Favourited;", 0)]
	[InlineData("/* item.favourited = data.Favourited; */", 0)]
	public void TheWriterMatcher_ReadsWritesAndNotComparisonsOrProse(string snippet, int expectedWrites) =>
		Assert.Equal(expectedWrites, FavouritedWrites(snippet).Count);

	/// <summary>Every assignment whose target is a member named <c>favourited</c> — read from the syntax tree, so a comparison, a read or a line of prose is not one.</summary>
	private static List<string> FavouritedWrites(string source) =>
		[.. Parse(source).DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Where(assignment => assignment.Left is MemberAccessExpressionSyntax member
				&& string.Equals(member.Name.Identifier.ValueText, "favourited", StringComparison.Ordinal))
			.Select(assignment => assignment.ToString())];

	private static string Relative(string path) =>
		Path.GetRelativePath(RepositoryPaths.Root, path).Replace(Path.DirectorySeparatorChar, '/');

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
