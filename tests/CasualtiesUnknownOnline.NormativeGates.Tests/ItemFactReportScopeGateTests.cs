using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The item-fact report scope gate (ticket
/// <c>backlog/review/container-move-snapshot-only-sync.md</c> — batch `20261005-c` read a
/// divergence warning on the OPERATOR's clone fact table for every remote container move it drove).
///
/// <para>
/// The item-fact carriers — <c>ItemWorldSync</c> (instantiate/destroy/drop/throw),
/// <c>PickupSync</c> (pickup) and <c>ContainerItemSync</c> (load/unload/spill) — stay silent
/// while a remote message is being applied, because a REPLAY of a peer's fact must not be
/// reported back as this client's own action: the character restore re-materializes items
/// through the game's own slot path, and reporting those made the host refuse the restored
/// items as <c>Conflict (item … is already carried)</c>
/// (<c>SourceShapeGateTests.CharacterRestore_MaterializesItemsInsideARemoteApplyScope</c>).
/// </para>
///
/// <para>
/// A peer's inventory INTENT is the other situation, and it shared that identity:
/// <c>RemoteIntentApplier</c> runs the game's own call on the OWNER's real items, so the
/// mutation is this client's own fact and its carriers must report it exactly as a local
/// gesture's would. Under the bare attribution query those hooks stayed silent, so a remote
/// container move reached the peers on the character snapshot alone — the divergence monitor's
/// Warn was right and the event was missing. The rule this gate encodes: an item-fact carrier
/// asks <c>CallContext.IsReplayedRemoteFact</c> (a replay, stay silent) — never the bare
/// <c>IsWithin(RemoteApply)</c>, which is true for both situations.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the scan surface is the item-fact carrier set
/// (<see cref="CarrierTypes"/>), each file located by the type declaration it owns; the carrier
/// census holds a floor so an emptied or renamed pin fails loudly instead of checking nothing.
/// The matchers are pinned with positive and negative samples. What is NOT reached: a carrier
/// that gates on a helper or a switch arm rather than on the query text, and the runtime
/// behaviour itself — this gate pins the source shape, and the three-client acceptance run is
/// what reads the monitor's output as a zero-warning row.
/// </para>
/// </summary>
public class ItemFactReportScopeGateTests
{
	private const string CallContextFile = "src/CasualtiesUnknownOnline.GameAdapter/CallContext.cs";

	/// <summary>The call-identity member a peer's intent execution declares — the distinction that lets a real local mutation report.</summary>
	private const string IntentOrigin = "RemoteIntentApply";

	/// <summary>The query an item-fact carrier asks: "a peer's fact is being replayed here, so the local report stays silent".</summary>
	private const string ReplayQuery = "CallContext.IsReplayedRemoteFact";

	/// <summary>The bare attribution query, true for a replay AND for a peer-intent execution — an item-fact carrier may not gate on it.</summary>
	private const string BareAttributionQuery = "CallContext.IsWithin(CallContext.Origin.RemoteApply)";

	/// <summary>The carrier census floor: every carrier of the set declares the distinction, so a renamed type or an emptied scan fails rather than passing by checking nothing. The floor sits below the declared count so a carrier that folds its guards into one helper does not turn the pin into a false red.</summary>
	private const int MinimumCarrierGuardSites = 4;

	/// <summary>
	/// The item-fact carrier set. Named rather than globbed because "which type reports an item
	/// fact" is the closed list of the item domain's report seams — the patch bridge routes every
	/// item callback into one of these three.
	/// </summary>
	private static readonly string[] CarrierTypes = ["ItemWorldSync", "PickupSync", "ContainerItemSync"];

	[Fact]
	public void CallContext_DeclaresThePeerIntentExecutionOrigin()
	{
		var origins = OriginMembers(RepositoryPaths.ReadText(CallContextFile));
		Assert.True(
			origins.Contains(IntentOrigin),
			$"{CallContextFile} must declare `{IntentOrigin}` in `Origin` — without it a peer's intent execution is indistinguishable from a replayed fact, and the item-fact carriers stay silent for both. Found [{string.Join(", ", origins)}]");
	}

	[Fact]
	public void CallContext_DeclaresTheReplayQueryThatExcludesAPeerIntentExecution()
	{
		var body = ReplayQueryBody(RepositoryPaths.ReadText(CallContextFile));
		Assert.True(
			body is not null,
			$"{CallContextFile} must declare `internal static bool {ReplayQuery.Split('.')[1]} => ...` — the query the item-fact carriers ask instead of the bare attribution");
		Assert.True(
			body!.Contains("IsWithin(Origin.RemoteApply)", StringComparison.Ordinal)
			&& body.Contains("!IsWithin(Origin.RemoteIntentApply)", StringComparison.Ordinal),
			$"`{ReplayQuery}` must be `IsWithin(Origin.RemoteApply) && !IsWithin(Origin.RemoteIntentApply)` — a replay is silent, a peer's intent executed on this client's own objects is not. Found: {body}");
	}

	[Fact]
	public void TheItemFactCarriers_AskTheReplayQueryAndNeverTheBareAttribution()
	{
		var failures = new List<string>();
		var guardSites = 0;
		foreach (var type in CarrierTypes)
		{
			var file = CarrierFile(type);
			if (file is null)
			{
				failures.Add($"{type}: no source file declares this carrier — the scan surface is stale, not clean");
				continue;
			}

			var text = File.ReadAllText(file);
			var bare = CountUses(text, BareAttributionQuery);
			if (bare > 0)
			{
				failures.Add(
					$"{Relative(file)}: {bare} `{BareAttributionQuery}` guard(s) — true for a replayed fact AND for a peer's intent this client executed on its own items, so a remote-driven move of the owner's item reports nothing and only the character snapshot carries it; ask `{ReplayQuery}` instead");
			}

			var replay = CountUses(text, ReplayQuery);
			if (replay == 0)
			{
				failures.Add($"{Relative(file)}: {type} declares no `{ReplayQuery}` guard at all — this carrier does not distinguish a replay from a peer's intent execution");
			}

			guardSites += replay;
		}

		Assert.True(failures.Count == 0, "item-fact report scope gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		Assert.True(
			guardSites >= MinimumCarrierGuardSites,
			$"found {guardSites} `{ReplayQuery}` guard(s) across the item-fact carriers — the census floor is {MinimumCarrierGuardSites}, so the matcher (or the guard sites) changed shape");
	}

	[Theory]
	[InlineData("if (CallContext.IsReplayedRemoteFact) { return; }", 1)]
	[InlineData("if (CallContext.IsReplayedRemoteFact || HarmonyTraverse.IsGenerating()) { return; }", 1)]
	[InlineData("private bool IsReplayed => CallContext.IsReplayedRemoteFact;", 1)]
	[InlineData("// if (CallContext.IsReplayedRemoteFact) { return; }", 0)]
	[InlineData("\"CallContext.IsReplayedRemoteFact\"", 0)]
	[InlineData("if (CallContext.IsWithin(CallContext.Origin.RemoteApply)) { return; }", 0)]
	public void TheReplayQueryCensus_CountsUsesAndIgnoresMentions(string source, int expected) =>
		Assert.Equal(expected, CountUses(source, ReplayQuery));

	[Theory]
	[InlineData("if (CallContext.IsWithin(CallContext.Origin.RemoteApply)) { return; }", 1)]
	[InlineData("private bool IsRemoteApply => CallContext.IsWithin(CallContext.Origin.RemoteApply);", 1)]
	[InlineData("// CallContext.IsWithin(CallContext.Origin.RemoteApply)", 0)]
	[InlineData("if (CallContext.IsReplayedRemoteFact) { return; }", 0)]
	[InlineData("if (CallContext.IsWithin(CallContext.Origin.RemoteIntentApply)) { return; }", 0)]
	public void TheBareAttributionCensus_CountsUsesAndIgnoresMentions(string source, int expected) =>
		Assert.Equal(expected, CountUses(source, BareAttributionQuery));

	[Theory]
	[InlineData("internal enum Origin { LocalAction, RemoteApply, RemoteIntentApply, Craft, }", true)]
	[InlineData("internal enum Origin { LocalAction, RemoteApply, Craft, }", false)]
	// A mention outside the Origin enum is not a declaration.
	[InlineData("internal enum Origin { LocalAction, RemoteApply, Craft, }\n// RemoteIntentApply", false)]
	public void TheOriginMatcher_ReadsTheEnumMembers(string source, bool expected)
	{
		var origins = OriginMembers(source);
		Assert.Equal(expected, origins.Contains(IntentOrigin));
		Assert.False(
			origins.Count == 0,
			"the origin census read no members at all — the matcher (or the enum) changed shape");
	}

	/// <summary>The <c>Origin</c> enum's member names, read from the syntax tree (a doc mention is not a member).</summary>
	private static List<string> OriginMembers(string source)
	{
		var declaration = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.FirstOrDefault(enumDeclaration => string.Equals(enumDeclaration.Identifier.ValueText, "Origin", StringComparison.Ordinal));

		return declaration is null
			? []
			: [.. declaration.Members.Select(member => member.Identifier.ValueText)];
	}

	/// <summary>The expression body of the replay query's declaration, or null when it is not declared — so the gate names "missing" and "wrong shape" separately.</summary>
	private static string? ReplayQueryBody(string source)
	{
		var name = ReplayQuery.Split('.')[1];
		var type = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, "CallContext", StringComparison.Ordinal));

		return type?.Members
			.OfType<PropertyDeclarationSyntax>()
			.FirstOrDefault(property => string.Equals(property.Identifier.ValueText, name, StringComparison.Ordinal))
			?.ExpressionBody?.Expression.ToString();
	}

	/// <summary>The file that declares a carrier type; null when the type moved or was renamed.</summary>
	private static string? CarrierFile(string typeName) =>
		Directory.EnumerateFiles(RepositoryPaths.File("src"), "*.cs", SearchOption.AllDirectories)
			.FirstOrDefault(file => DeclaresType(File.ReadAllText(file), typeName));

	private static bool DeclaresType(string source, string typeName) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.Any(declaration => string.Equals(declaration.Identifier.ValueText, typeName, StringComparison.Ordinal));

	/// <summary>Uses of a <c>CallContext</c> member — a method call or a property read; a doc mention or a string literal is neither.</summary>
	private static int CountUses(string source, string query)
	{
		var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
		var calls = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.Count(invocation => string.Equals(invocation.ToString(), query, StringComparison.Ordinal));
		var reads = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
			.Count(access => string.Equals(access.ToString(), query, StringComparison.Ordinal));
		return calls + reads;
	}

	private static string Relative(string file) => file.Substring(RepositoryPaths.Root.Length + 1);
}
