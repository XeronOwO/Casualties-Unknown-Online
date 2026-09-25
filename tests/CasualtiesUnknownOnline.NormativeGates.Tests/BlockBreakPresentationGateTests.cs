using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The block-break presentation gate (ticket
/// <c>backlog/review/guest-hears-only-some-block-break-sounds</c>): a block break
/// one side computed must be PRESENTED on every other side, and the presentation
/// must be the game's own damage roll rather than a re-implementation of its
/// clips.
///
/// <para>
/// Why a gate rather than only a unit test: the defect it guards is a ROUTING
/// defect. A break reaches the other sides as two facts of one break — the air
/// write that makes the cell air, and the drops-carrying break report one frame
/// later — and the air write always arrives first, so the receiving side's block
/// is already gone when the report lands and the report's own native roll never
/// runs there. Nothing about that is visible in a single pure function: it is the
/// wiring (every producer stamps the claim, every applied write consults the rule,
/// the rule's execution goes through <c>WorldGeneration.DamageBlock</c>) that
/// decides whether the guest hears the break. These pins are read from SOURCE by
/// Roslyn, so they run in the fast suite without the game assemblies; the rule
/// itself is covered behaviourally by
/// <c>CasualtiesUnknownOnline.Tests/World/RemoteBreakPresentationTests</c>, and
/// the audible result is the user's real-session acceptance.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the member census reads the properties of
/// <c>BlockPlacedMsg</c>; the producer census reads every
/// <c>new BlockPlacedMsg { ... }</c> initializer under <c>src/</c> and asks
/// whether it assigns <c>PlayerBreak</c>; the routing pins assert that the two
/// apply paths call the shared applier, that the applier consults the pure rule
/// and reaches the game's roll, and that the air write's claim is read from the
/// damage-roll scope. A claim passed to the wire by some OTHER construction shape
/// (a factory, a copy constructor) is out of reach — the message has none today,
/// and the census floor fails loudly if the constructed sites disappear.
/// </para>
/// </summary>
public class BlockBreakPresentationGateTests
{
	private const string AdapterWorldDir = "src/CasualtiesUnknownOnline.GameAdapter/World/";

	private const string MessageFile = "src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/BlockPlacedMsg.cs";

	private const string ApplierFile = AdapterWorldDir + "RemoteBlockWrite.cs";

	private const string WorldEventFile = AdapterWorldDir + "WorldEventSync.cs";

	private const string BlockBreakFile = AdapterWorldDir + "BlockBreakSync.cs";

	/// <summary>The whole wire surface this gate treats as pinned: a member added to the message (or removed) is a red until it is reviewed here.</summary>
	private static readonly string[] MessageMembers = ["Block", "Generation", "PlayerBreak", "X", "Y"];

	/// <summary>The member census floor — a pin emptied alongside its source would otherwise pass by checking nothing.</summary>
	private const int MinimumMessageMembers = 4;

	/// <summary>The construction-site floor: the live report, the host relay, the guest relay resend and the host correction are the sites today.</summary>
	private const int MinimumConstructionSites = 4;

	/// <summary>The apply-site floor: the host branch and the guest branch of the received-write path, plus the accepted break whose cell still stands.</summary>
	private const int MinimumApplySites = 3;

	[Fact]
	public void BlockPlacedMessage_DeclaresExactlyThePinnedMembers()
	{
		Assert.True(
			MessageMembers.Length >= MinimumMessageMembers,
			$"the pinned message census holds only {MessageMembers.Length} member(s) — the pin was emptied, not the message");

		Assert.Equal(Census(MessageMembers), Census(DeclaredMessageMembers(RepositoryPaths.ReadText(MessageFile))));
	}

	[Fact]
	public void EveryBlockPlacedConstructionSite_StampsThePresentationClaim()
	{
		var census = ConstructionSiteCensus(SourceFiles("src"));
		Assert.True(
			census.Sites >= MinimumConstructionSites,
			$"found only {census.Sites} BlockPlacedMsg construction site(s) — the census floor is {MinimumConstructionSites}, so the matcher (or the message's build sites) changed shape");

		Assert.True(
			census.Unstamped.Count == 0,
			"every BlockPlacedMsg must state its presentation claim explicitly (PlayerBreak) — a site that omits it silently turns a break back into a silent write:"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, census.Unstamped));
	}

	[Fact]
	public void TheReceivePaths_HandEveryAppliedWriteToTheSharedApplier()
	{
		// Per FILE, not a total: the received-write path's host branch and guest
		// branch must both go through the applier, and so must the accepted break
		// whose cell still stands — a total would let one branch cover for another.
		var hostSites = Count(RepositoryPaths.ReadText(WorldEventFile), "RemoteBlockWrite.Apply(");
		var breakSites = Count(RepositoryPaths.ReadText(BlockBreakFile), "RemoteBlockWrite.Apply(");

		Assert.True(hostSites >= 2, $"{WorldEventFile} calls the shared applier {hostSites} time(s) — its host branch and its guest branch must both go through it");
		Assert.True(breakSites >= 1, $"{BlockBreakFile} calls the shared applier {breakSites} time(s) — the accepted break whose cell still stands must go through it");
		Assert.True(
			hostSites + breakSites >= MinimumApplySites,
			$"the two receive paths call the shared applier {hostSites + breakSites} time(s) — expected at least {MinimumApplySites}");

		// And a receive path may not write the cell itself: that is exactly the
		// shape which cannot present a relayed break.
		foreach (var file in new[] { WorldEventFile, BlockBreakFile })
		{
			Assert.False(
				RepositoryPaths.ReadText(file).Contains("world.SetBlock(", StringComparison.Ordinal),
				$"{file} writes the cell directly — every received write must go through {ApplierFile}, or a relayed break cannot be presented");
		}
	}

	[Fact]
	public void TheApplier_ConsultsThePureRuleAndRunsTheGamesOwnRoll()
	{
		var applier = RepositoryPaths.ReadText(ApplierFile);

		Assert.Contains("RemoteBreakPresentation.Route(playerBreak, block, world.GetBlock(cell) != 0)", applier, StringComparison.Ordinal);

		// The two branches are the applier's WHOLE write surface, and the break
		// branch must be the game's own roll carrying the derived remainder: a
		// looser match would also pass on a mention in a comment, or on a call
		// someone commented out.
		Assert.Equal(1, Count(applier, "world.SetBlock(cell, block);"));
		Assert.Equal(1, Count(applier, "world.DamageBlock(cell, RemoteBreakPresentation.LethalDamage(health, currentDamage), true, false, true);"));
		Assert.DoesNotContain("Sound.Play(", applier, StringComparison.Ordinal);
	}

	[Fact]
	public void TheAirWritesClaim_IsReadFromTheDamageRollScope() =>
		Assert.Contains("CallContext.Origin.DamageBlockOrigin", RepositoryPaths.ReadText(WorldEventFile), StringComparison.Ordinal);

	[Theory]
	[InlineData("var m = new BlockPlacedMsg { X = 1, PlayerBreak = true };", 1, 0)]
	[InlineData("var m = new BlockPlacedMsg { X = 1 };", 1, 1)]
	[InlineData("var m = new BlockPlacedMsg { PlayerBreak = playerBreak, Block = block };", 1, 0)]
	[InlineData("var m = new BlockPlacedMsg(X, Y) { PlayerBreak = true };", 1, 0)]
	[InlineData("// a doc mention: new BlockPlacedMsg { X = 1 }\nvar other = 1;", 0, 0)]
	public void TheConstructionSiteCensus_ReadsInitializersAndIgnoresMentions(string source, int expectedSites, int expectedUnstamped)
	{
		var census = ConstructionSiteCensus([source]);

		Assert.Equal(expectedSites, census.Sites);
		Assert.Equal(expectedUnstamped, census.Unstamped.Count);
	}

	private static (int Sites, List<string> Unstamped) ConstructionSiteCensus(IReadOnlyList<string> sources)
	{
		var sites = 0;
		var unstamped = new List<string>();
		foreach (var source in sources)
		{
			var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
			foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
			{
				var name = creation.Type switch
				{
					GenericNameSyntax generic => generic.Identifier.ValueText,
					IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
					QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
					_ => creation.Type.ToString(),
				};
				if (!string.Equals(name, "BlockPlacedMsg", StringComparison.Ordinal))
				{
					continue;
				}

				sites++;
				var assignment = creation.Initializer?.Expressions
					.OfType<AssignmentExpressionSyntax>()
					.Select(expression => expression.Left.ToString())
					.FirstOrDefault(left => string.Equals(left, "PlayerBreak", StringComparison.Ordinal));
				if (assignment is null)
				{
					unstamped.Add(creation.ToString());
				}
			}
		}

		return (sites, unstamped);
	}

	private static IReadOnlyList<string> SourceFiles(string relativeRoot) =>
		[.. Directory.EnumerateFiles(RepositoryPaths.File(relativeRoot), "*.cs", SearchOption.AllDirectories)
			.Select(File.ReadAllText)];

	private static string[] DeclaredMessageMembers(string source) =>
		[.. CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.First(declaration => string.Equals(declaration.Identifier.ValueText, "BlockPlacedMsg", StringComparison.Ordinal))
			.Members
			.OfType<PropertyDeclarationSyntax>()
			.Select(property => property.Identifier.ValueText)];

	private static int Count(string source, string value)
	{
		var count = 0;
		var index = source.IndexOf(value, StringComparison.Ordinal);
		while (index >= 0)
		{
			count++;
			index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal);
		}

		return count;
	}

	private static string Census(IEnumerable<string> members) =>
		string.Join(",", members.OrderBy(name => name, StringComparer.Ordinal));
}
