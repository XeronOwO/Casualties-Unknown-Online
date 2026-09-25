using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The consume-sound capture gate (ticket
/// <c>backlog/review/host-eating-sound-not-heard-on-guest</c>): a local body's
/// eating/meal one-shot sounds must be PRESENTED on every other side, and the
/// presentation must be the same one-shot capture chain the other character
/// sounds already ride — not a second sound path.
///
/// <para>
/// Why a gate rather than only a unit test: the defect is a ROUTING defect. The
/// native eat sounds are played inside <c>ItemInfo.useAction</c> delegates
/// (<c>Body.UseItem</c> / <c>Body.UseItemInHand</c>) and the meal-end <c>burp</c>
/// inside <c>Body.HandleVisuals</c>; CUO's capture is call-identity scoped, so a
/// sound whose call runs outside every scope is never reported and the remote
/// clone never plays it (user report 2026-09-21: the host eats and the guest
/// hears nothing). Nothing about that is visible in a single pure function: it
/// is the wiring (which native calls open a scope, which scopes the policy
/// classifies, which kind reaches the wire) that decides whether the guest
/// hears the meal. These pins are read from SOURCE by Roslyn, so they run in the
/// fast suite without the game assemblies; the classification itself is covered
/// behaviourally by
/// <c>CasualtiesUnknownOnline.Tests/Session/CharacterSoundPolicyTests</c>, the
/// wire by <c>CharacterSoundSyncTests</c>, and the audible result is the user's
/// real-session acceptance.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the kind census reads the members of
/// <c>CharacterSoundKind</c>; the scope pins read the two use-item hooks and the
/// meal-end patch; the origin census reads every <c>CallContext.Origin.Character*</c>
/// value referenced under <c>src/</c> and asks whether the two <c>Sound.Play</c>
/// patches classify it; the protocol pin derives the log entry the constant's own
/// doc comment must carry for its CURRENT value. A capture scope opened by some
/// other mechanism (a dynamic patch, a non-Character origin) is out of reach —
/// the census floor fails loudly if the referenced origins disappear.
/// </para>
/// </summary>
public class ConsumeSoundCaptureGateTests
{
	private const string PolicyFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/CharacterSoundPolicy.cs";

	private const string KindFile =
		"src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/CharacterSoundKind.cs";

	private const string ProtocolFile =
		"src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs";

	private const string UseHooksFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs";

	private const string BurpPatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/BurpSoundPatches.cs";

	private const string SoundPlayPatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/SoundPlayPatch.cs";

	private const string SoundPlayAudioClipPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/SoundPlayAudioClipPatch.cs";

	/// <summary>The whole wire surface this gate treats as pinned: a kind added to (or removed from) the enum is a red until it is reviewed here.</summary>
	private static readonly string[] WireKinds =
	[
		"AttackSwing", "Bark", "Consume", "Exert", "Footstep", "Growl", "GunFire",
		"ItemPlacement", "LandingImpact", "Pain", "ThrowSwing", "Yawn",
	];

	/// <summary>The kind-census floor — a pin emptied alongside its source would otherwise pass by checking nothing.</summary>
	private const int MinimumWireKinds = 11;

	/// <summary>The capture-origin floor: the character capture scopes referenced today (attack, throw, placement, exert, footstep, landing, vocalization, lockpick pain, bark, growl, item use, burp).</summary>
	private const int MinimumCharacterOrigins = 10;

	/// <summary>The native ingest clips the policy must classify (the edible use actions' clips plus the container drink's two clip arguments) — the meal-end <c>burp</c> is the second decision row of the same family.</summary>
	private static readonly string[] IngestClips = ["eatCrunch", "eatFlesh", "glass", "crystalenemylaugh", "drink", "pills"];

	[Fact]
	public void CharacterSoundKind_DeclaresExactlyThePinnedKinds()
	{
		Assert.True(
			WireKinds.Length >= MinimumWireKinds,
			$"the pinned kind census holds only {WireKinds.Length} member(s) — the pin was emptied, not the enum");

		var declared = DeclaredEnumMembers(RepositoryPaths.ReadText(KindFile), "CharacterSoundKind");

		Assert.Equal(Census(WireKinds), Census(declared));
	}

	[Fact]
	public void BothItemUseHooks_RouteThroughTheSharedCaptureScope()
	{
		var hooks = RepositoryPaths.ReadText(UseHooksFile);
		var prefixes = PrefixBodies(hooks, "DirectPlaceableUseItemPatch", "DirectPlaceableUseItemInHandPatch");

		// Per hook, not a total: Body.UseItem and Body.UseItemInHand are separate
		// native entry points (the LMB path calls Stats.useAction directly), so one
		// branch covering for the other would leave half the family silent.
		Assert.Equal(2, prefixes.Count);
		for (var hook = 0; hook < prefixes.Count; hook++)
		{
			Assert.Contains("OpenUseSoundScope(", prefixes[hook], StringComparison.Ordinal);
		}

		Assert.Contains("CallContext.Origin.CharacterItemUse", hooks, StringComparison.Ordinal);
		Assert.Contains("CallContext.Origin.CharacterItemPlacement", hooks, StringComparison.Ordinal);
	}

	[Fact]
	public void TheMealEndSound_IsCapturedByItsOwnScope()
	{
		Assert.True(
			File.Exists(RepositoryPaths.File(BurpPatchFile)),
			$"{BurpPatchFile} is missing — the meal-end burp plays inside Body.HandleVisuals, outside every capture scope, so the guest never hears it");

		var patch = RepositoryPaths.ReadText(BurpPatchFile);

		Assert.Contains("[HarmonyPatch(typeof(Body), \"HandleVisuals\")]", patch, StringComparison.Ordinal);
		Assert.Contains("CallContext.Origin.CharacterBurp", patch, StringComparison.Ordinal);
		Assert.Contains("CallContext.Enter(", patch, StringComparison.Ordinal);
	}

	[Fact]
	public void ThePolicy_ClassifiesTheIngestFamilyAndTheMealEnd()
	{
		var policy = RepositoryPaths.ReadText(PolicyFile);

		// The DECISION expressions themselves, not a clip name mentioned in a doc
		// comment: a row gutted to `null` must fail here.
		Assert.Contains(
			"Origin.ItemUse => clip is \"eatCrunch\" or \"eatFlesh\" or \"glass\" or \"crystalenemylaugh\" or \"drink\" or \"pills\" ? CharacterSoundKind.Consume : null,",
			policy,
			StringComparison.Ordinal);
		Assert.Contains(
			"Origin.Burp => clip == \"burp\" ? CharacterSoundKind.Consume : null,",
			policy,
			StringComparison.Ordinal);

		foreach (var clip in IngestClips)
		{
			Assert.Contains($"\"{clip}\"", policy, StringComparison.Ordinal);
		}
	}

	[Fact]
	public void EveryCharacterCaptureOrigin_IsClassifiedByASoundPlayPatch()
	{
		var referenced = ReferencedCharacterOrigins(SourceFiles("src"));

		Assert.True(
			referenced.Count >= MinimumCharacterOrigins,
			$"found only {referenced.Count} character capture origin(s) under src/ — the census floor is {MinimumCharacterOrigins}, so the matcher (or the capture scopes) changed shape: {string.Join(", ", referenced)}");

		var classified = RepositoryPaths.ReadText(SoundPlayPatchFile) + RepositoryPaths.ReadText(SoundPlayAudioClipPatchFile);
		var unclassified = referenced
			.Where(origin => !classified.Contains($"CallContext.Origin.{origin} =>", StringComparison.Ordinal))
			.ToList();

		Assert.True(
			unclassified.Count == 0,
			"every capture scope must state how its sounds are classified, in one of the two Sound.Play patches — a scope with no mapping captures nothing:"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, unclassified));
	}

	[Fact]
	public void EveryProtocolBump_CarriesItsPerNumberLogEntry()
	{
		var source = RepositoryPaths.ReadText(ProtocolFile);
		var match = Regex.Match(source, @"public const int Current = (?<value>\d+);");

		Assert.True(match.Success, $"{ProtocolFile} no longer declares `public const int Current = <n>;`");

		var current = match.Groups["value"].Value;
		Assert.Contains($"/// {current}:", source, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("var x = CallContext.Enter(CallContext.Origin.CharacterBurp);", "CharacterBurp", 1)]
	[InlineData("CallContext.Origin.CharacterAttack => CharacterSoundPolicy.Origin.Attack,", "CharacterAttack", 1)]
	[InlineData("CallContext.Origin.LocalAction", "", 0)]
	[InlineData("// a doc mention of CallContext.Origin.CharacterNope\nvar other = 1;", "", 0)]
	public void TheCaptureOriginCensus_ReadsEnumReferencesAndIgnoresMentions(
		string source, string expectedOrigin, int expectedCount)
	{
		var origins = ReferencedCharacterOrigins([source]);

		Assert.Equal(expectedCount, origins.Count);
		if (expectedCount > 0)
		{
			Assert.Contains(expectedOrigin, origins);
		}
	}

	[Theory]
	[InlineData("public enum K { A = 1, B = 2 }", 0)]
	[InlineData("public enum CharacterSoundKind : byte { AttackSwing = 1, Consume = 12, }", 2)]
	[InlineData("// a doc comment naming CharacterSoundKind\npublic static class Other { }", 0)]
	public void TheKindCensus_ReadsTheEnumsMembers(string source, int expectedCount) =>
		Assert.Equal(expectedCount, DeclaredEnumMembers(source, "CharacterSoundKind").Count);

	[Theory]
	[InlineData("internal static class P { private static void Prefix() { var s = OpenUseSoundScope(body, id); } }", true)]
	[InlineData("internal static class P { private static void Prefix() { } }", false)]
	[InlineData("// a doc mention of OpenUseSoundScope(body, id)\ninternal static class P { private static void Prefix() { } }", false)]
	public void ThePrefixBodyScan_ReadsTheHooksOwnBody(string source, bool expectedContains)
	{
		var body = Assert.Single(PrefixBodies(source, "P"));

		Assert.Equal(expectedContains, body.Contains("OpenUseSoundScope(", StringComparison.Ordinal));
	}

	private static IReadOnlyList<string> ReferencedCharacterOrigins(IReadOnlyList<string> sources)
	{
		var origins = new SortedSet<string>(StringComparer.Ordinal);
		foreach (var source in sources)
		{
			var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
			foreach (var member in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
			{
				if (member.Expression is not MemberAccessExpressionSyntax scope
					|| scope.Name.Identifier.ValueText != "Origin"
					|| scope.Expression.ToString() != "CallContext")
				{
					continue;
				}

				var name = member.Name.Identifier.ValueText;
				if (name.StartsWith("Character", StringComparison.Ordinal))
				{
					origins.Add(name);
				}
			}
		}

		return [.. origins];
	}

	private static IReadOnlyList<string> DeclaredEnumMembers(string source, string enumName)
	{
		var declaration = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, enumName, StringComparison.Ordinal));

		return declaration is null
			? []
			: [.. declaration.Members.Select(member => member.Identifier.ValueText)];
	}

	private static IReadOnlyList<string> SourceFiles(string relativeRoot) =>
		[.. Directory.EnumerateFiles(RepositoryPaths.File(relativeRoot), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(File.ReadAllText)];

	/// <summary>A named patch's Prefix body — the hook's OWN text, so a shared scope decision must actually be CALLED by each hook, not merely declared somewhere in the file.</summary>
	private static IReadOnlyList<string> PrefixBodies(string source, params string[] classNames)
	{
		var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
		var bodies = new List<string>();
		foreach (var name in classNames)
		{
			var declaration = root.DescendantNodes()
				.OfType<ClassDeclarationSyntax>()
				.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, name, StringComparison.Ordinal));
			var prefix = declaration?.Members
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(method => string.Equals(method.Identifier.ValueText, "Prefix", StringComparison.Ordinal));
			bodies.Add(prefix?.ToString() ?? "");
		}

		return bodies;
	}

	private static string Census(IEnumerable<string> members) =>
		string.Join(",", members.OrderBy(name => name, StringComparer.Ordinal));
}
