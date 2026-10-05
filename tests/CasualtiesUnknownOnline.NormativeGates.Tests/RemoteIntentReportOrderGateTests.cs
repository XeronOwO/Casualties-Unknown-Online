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
/// The owner-side re-report ORDER gate (ticket <c>container-move-snapshot-only-sync</c> — batch
/// `20261005-e` read <c>[CharSync] divergence … left the inventory without an event sync</c> on the
/// operator and on the third peer for every remote-driven drop, in both owner directions).
///
/// <para>
/// An owner announces an inventory change twice by design: the native call's own item-fact carrier
/// reports the change, and the immediate clone re-report
/// (<c>CharacterDataSync.ReportInventoryChanged</c>) makes every viewer converge without waiting for the
/// 1 Hz character snapshot. That pair is ordered whenever the carrier sends INSIDE the apply scope — the
/// container, pickup, slot and use carriers all do. A drop does not: its carrier holds the report for one
/// frame so the game's own <c>DropItem</c> → <c>ThrowItem</c> pair can set the final velocity
/// (<c>DropPendingState.TryFlush</c> refuses a same-frame flush on purpose). A snapshot sent while that
/// report is still pending reaches the peers FIRST and announces a change whose event is still in flight —
/// the monitor's wording is exactly that trace, and it is the defect this gate keeps out.
/// </para>
///
/// <para>
/// The rule is asked of the item domain's PENDING STATE, not of the intent kind: while
/// <c>ItemWorldSync.HasPendingDropReport</c> is true, no inventory re-report may go out. That covers the
/// drop kinds, and it also covers a kind whose native call left a drop behind on a path that did not land
/// (R9's slot release drops the two slot occupants before its own pickup may refuse). It is asked in BOTH
/// places an owner can send that snapshot from: the intent applier, and
/// <c>GameAdapterBridge.OnInventoryChanged</c> — the patch layer's ONE entry point, which is where the
/// unconditional <c>Body.DropWearable</c> postfix re-reported a wearable drop and defeated a kind-shaped
/// rule (found by this cycle's adversarial review).
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the scan surface is the two files that can send the snapshot, the
/// item-domain file that owns the pending state, and the two call sites' own source text; the call census
/// holds a floor so a renamed file or an emptied scan fails loudly instead of checking nothing. The guard
/// matcher is pinned with positive and negative samples, including the reversed polarity and a condition
/// that names the query without guarding on it. What is NOT reached: whether the runtime order is really
/// what the source says — the three-client acceptance run is what reads the monitor as a zero-warning row —
/// and the re-report call sites outside these two (the cross-player applies in <c>PlayerInteractionApply</c>,
/// <c>PlayerPushApply</c>, <c>TraderRecruitCoordinator</c> and <c>MedicalOperationApply</c> are named in the
/// ticket and in the self-check's limits rather than pinned here).
/// </para>
/// </summary>
public class RemoteIntentReportOrderGateTests
{
	private const string ApplierFile = "src/CasualtiesUnknownOnline.GameAdapter/RemoteIntentApplier.cs";

	/// <summary>The patch layer's one entry point for the immediate re-report — a patch asks the bridge, never the character domain.</summary>
	private const string BridgeFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs";

	/// <summary>The item domain's own file: the pending state belongs to the owner of the drop machine.</summary>
	private const string ItemWorldSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs";

	/// <summary>The query both re-report entry points must ask before they send an inventory snapshot.</summary>
	private const string PendingDropQuery = "HasPendingDropReport";

	/// <summary>The owner's immediate clone re-report, as the receiver of the call reads it (the two entry points pass different bodies).</summary>
	private const string ReReportCall = "domains.CharacterDataSync.ReportInventoryChanged(";

	/// <summary>The state the query must read: a drop report registered and not yet sent.</summary>
	private const string PendingDropState = "ItemDropState.Phase.Dropped";

	[Theory]
	[InlineData(ApplierFile)]
	[InlineData(BridgeFile)]
	public void EveryReReportEntryPoint_AsksThePendingDropQueryFirst(string file)
	{
		var source = RepositoryPaths.ReadText(file);

		var calls = ReReportCalls(source);
		Assert.True(
			calls.Count > 0,
			$"{file} no longer sends `{ReReportCall}…)` at all — either the re-report moved and this gate's scan surface is stale, or the immediate convergence half was dropped; both need the gate updated in the same change");

		Assert.True(
			source.Contains(PendingDropQuery, StringComparison.Ordinal),
			$"{file} never asks `{PendingDropQuery}` — a snapshot sent while a drop report is still pending reaches the peers ahead of the event that explains it, and their clone fact table reads exactly that as `left the inventory without an event sync` (batch `20261005-e`)");

		var unguarded = calls.Where(call => !IsGuarded(call, PendingDropQuery)).ToList();
		Assert.True(
			unguarded.Count == 0,
			$"{file}: {unguarded.Count} of {calls.Count} `{ReReportCall}…)` call(s) are not guarded by `{PendingDropQuery}` — the guard is an early exit `if (<owner>.{PendingDropQuery}) {{ …; return; }}` or an inclusive `if (!<owner>.{PendingDropQuery})`; found at {string.Join(", ", unguarded.Select(call => $"line {Line(call)}"))}");
	}

	[Fact]
	public void ThePendingDropQuery_ReadsTheDropMachinesUnsentReport()
	{
		var body = DeclaredBody(ItemWorldSyncFile, PendingDropQuery);
		Assert.True(
			body is not null,
			$"{ItemWorldSyncFile} does not declare `{PendingDropQuery}` — the two re-report entry points would have to invent the state themselves instead of asking its owner");

		Assert.True(
			body!.Contains(PendingDropState, StringComparison.Ordinal),
			$"`{PendingDropQuery}` must read {PendingDropState} — the fact that a report is registered and NOT yet sent. Found body: {body}");
	}

	[Theory]
	[InlineData("if (world.HasPendingDropReport) { return; }\n" + ReReportCall + "body);", 0)]
	[InlineData("if (!world.HasPendingDropReport) { " + ReReportCall + "body); }", 0)]
	[InlineData("if (world.HasPendingDropReport && other) { return; }\n" + ReReportCall + "body);", 0)]
	[InlineData(ReReportCall + "body);", 1)]
	[InlineData("if (other) { return; }\n" + ReReportCall + "body);", 1)]
	[InlineData("if (world.IsContinuousGesture()) { return; }\n" + ReReportCall + "body);", 1)]
	[InlineData("if (world.HasPendingDropReport) { " + ReReportCall + "body); }", 1)]
	[InlineData("if (!other && world.HasPendingDropReport) { " + ReReportCall + "body); }", 1)]
	[InlineData("if (other) { " + ReReportCall + "body); }", 1)]
	public void TheGuardMatcher_ReadsTheQueryAndItsPolarity(string body, int expectedUnguarded) =>
		Assert.Equal(expectedUnguarded, UnguardedReReports(body, PendingDropQuery));

	/// <summary>The immediate re-report calls in a source, read from the syntax tree (a doc mention is not a call).</summary>
	private static List<InvocationExpressionSyntax> ReReportCalls(string source) =>
		[.. Parse(source).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Where(invocation => invocation.ToString().StartsWith(ReReportCall, StringComparison.Ordinal))];

	/// <summary>How many immediate re-reports in this source are not protected by the pending-drop query.</summary>
	private static int UnguardedReReports(string body, string query) =>
		ReReportCalls($"internal sealed class Sample {{ private void M() {{ {body} }} }}")
			.Count(call => !IsGuarded(call, query));

	/// <summary>
	/// Two shapes guard the re-report, and the POLARITY is part of the rule: an early exit on the query
	/// itself (<c>if (query) { …; return; }</c>), or an inclusive branch on its negation
	/// (<c>if (!query) { … }</c>). A condition that merely MENTIONS the query — a reversed branch
	/// (<c>if (query) { report }</c>) or a condition that asks about something else and names the query
	/// beside it (<c>if (!other &amp;&amp; query) { report }</c>) — re-reports exactly the state this rule
	/// holds back, so it is not a guard.
	/// </summary>
	private static bool IsGuarded(InvocationExpressionSyntax call, string query)
	{
		var enclosing = call.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
		if (enclosing is not null && IsNegatedQuery(enclosing.Condition, query))
		{
			return true;
		}

		var block = call.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
		if (block is null)
		{
			return false;
		}

		foreach (var statement in block.Statements)
		{
			if (statement.SpanStart >= call.SpanStart)
			{
				break;
			}

			if (statement is IfStatementSyntax guard
				&& AsksTheQuery(guard.Condition, query)
				&& !IsNegatedQuery(guard.Condition, query)
				&& ExitsTheMethod(guard))
			{
				return true;
			}
		}

		return false;
	}

	private static bool AsksTheQuery(ExpressionSyntax expression, string query) =>
		expression.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Any(identifier => string.Equals(identifier.Identifier.ValueText, query, StringComparison.Ordinal));

	/// <summary>True when the query is read INSIDE a logical negation — the inclusive guard's shape.</summary>
	private static bool IsNegatedQuery(ExpressionSyntax condition, string query) =>
		condition.DescendantNodesAndSelf()
			.OfType<PrefixUnaryExpressionSyntax>()
			.Where(unary => unary.IsKind(SyntaxKind.LogicalNotExpression))
			.Any(unary => AsksTheQuery(unary.Operand, query));

	/// <summary>True when the guarded branch cannot fall through — the early exit the ordering rule needs.</summary>
	private static bool ExitsTheMethod(IfStatementSyntax guard) => guard.Else is null && EndsWithExit(guard.Statement);

	private static bool EndsWithExit(StatementSyntax statement) => statement switch
	{
		ReturnStatementSyntax or ThrowStatementSyntax => true,
		BlockSyntax block => block.Statements.Count > 0 && EndsWithExit(block.Statements[^1]),
		_ => false,
	};

	/// <summary>The expression body of a property or method declared in one file, or null when the file declares neither.</summary>
	private static string? DeclaredBody(string file, string member)
	{
		var declarations = Parse(File.ReadAllText(RepositoryPaths.File(file))).DescendantNodes();
		var property = declarations.OfType<PropertyDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, member, StringComparison.Ordinal))
			?.ExpressionBody?.Expression.ToString();
		if (property is not null)
		{
			return property;
		}

		return declarations.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, member, StringComparison.Ordinal))
			?.ExpressionBody?.Expression.ToString();
	}

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();

	private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
