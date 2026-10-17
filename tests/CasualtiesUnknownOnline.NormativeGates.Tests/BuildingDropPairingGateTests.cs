using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The building-drop pairing gate (ticket <c>backlog/todo/validation-and-source-checks.md</c>, the
/// two S2b limits closed on 2026-10-17).
///
/// <para>
/// A support-loss drop has no identity of its own: the dying entity is destroyed before its drop's
/// <c>Item.Start</c> runs, and the entity's own <c>blockPlacedOn</c> is the ONLY key that says which
/// break owns the drop. That key is read in the death branch and has to reach the fold through three
/// glue sites that no behaviour test can see — the patch opens the scope with the entity's own cell,
/// <c>Item.Awake</c> stamps it on the drop marker, and <c>ItemWorldSync</c> folds only on it. Each site
/// is one line, and each of them, dropped, leaves the whole suite green while the bound silently
/// returns to pairing drops by TIME: the host then judges a report's building drops against the
/// buildings that lost support at the REPORT's cell and refuses a drop the breaker really rolled.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the scan surface is the three method bodies, resolved through
/// Roslyn, and the matchers are pinned with positive and negative samples (a comment or a string
/// literal that mentions the call is not a call). What it does NOT reach: whether the published cell is
/// the right one at runtime — no test can instantiate the game's own entities, so that is the
/// acceptance batch's row, declared owed in the ticket beside this gate.
/// </para>
/// </summary>
public class BuildingDropPairingGateTests
{
	private const string BuildingEntityPatchesFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/BuildingEntityPatches.cs";

	private const string ItemPatchesFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/ItemPatches.cs";

	private const string ItemWorldSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs";

	[Fact]
	public void TheDeathScope_IsOpenedWithTheDyingBuildingsOwnCell()
	{
		var body = RequireMethodBody(BuildingEntityPatchesFile, "BuildingEntityUpdatePatch", "Prefix");

		Assert.True(
			Calls(body, "EnterBuildingDeathDrop"),
			$"{BuildingEntityPatchesFile}: `BuildingEntityUpdatePatch.Prefix` must open the death branch through `CallContext.EnterBuildingDeathDrop(…)` — the plain `Enter(Origin.BuildingDeathDrop)` publishes no cell, and a drop with no cell cannot be paired with the break that killed it");
		Assert.True(
			PublishesOnlyTheBuildingsOwnCell(body),
			$"{BuildingEntityPatchesFile}: the scope's argument must be `requireGround ? blockPlacedOn : null` — the dying entity's OWN cell, published only when it requires ground. A building that does not depend on its ground has no break that could own its drops, publishing a cell for it would pair it with an unrelated one, and a constant would pair every drop with whatever cell it named");
	}

	[Fact]
	public void TheAwakePatch_StampsThePublishedCellOnTheDropMarker()
	{
		var body = RequireMethodBody(ItemPatchesFile, "ItemAwakePatch", "Postfix");

		Assert.True(
			Calls(body, "TryGetBuildingDeathSupportCell"),
			$"{ItemPatchesFile}: `ItemAwakePatch.Postfix` must ask `CallContext.TryGetBuildingDeathSupportCell(…)` — `Item.Awake` runs synchronously inside the death branch and is the ONE moment the cell can be stamped on the drop; `Item.Start` runs the next frame, when the entity is gone");
		Assert.True(
			StampsThePublishedCell(body),
			$"{ItemPatchesFile}: the value the scope published must be the value written onto the marker's `SupportCell` — a stamp of anything else (`SupportCell = null`, another local) leaves every behaviour test green while the marker carries no cell");
	}

	[Fact]
	public void TheFold_IsKeyedOnTheDropsOwnCell()
	{
		var body = RequireMethodBody(ItemWorldSyncFile, "ItemWorldSync", "OnItemInstantiated");

		Assert.True(
			Reads(body, "SupportCell"),
			$"{ItemWorldSyncFile}: `ItemWorldSync.OnItemInstantiated` must gate the building-death fold on the marker's `SupportCell` — without the gate every building death inside the break's frame window rides that break, and the host refuses the whole building set by name");
		Assert.True(
			PassesTheDropsOwnCell(body),
			$"{ItemWorldSyncFile}: the fold must hand the pending state the drop's OWN cell — `TryAddBuildingDrop(drop, cell.x, cell.y)` on one object's two axes. A two-argument call pairs nothing, and a constant pairs every drop with whatever cell it named");
	}

	[Theory]
	[InlineData("CallContext.EnterBuildingDeathDrop(__instance.blockPlacedOn);", "EnterBuildingDeathDrop", true)]
	[InlineData("EnterBuildingDeathDrop(null);", "EnterBuildingDeathDrop", true)]
	[InlineData("// CallContext.EnterBuildingDeathDrop(cell)", "EnterBuildingDeathDrop", false)]
	[InlineData("\"CallContext.EnterBuildingDeathDrop(cell)\"", "EnterBuildingDeathDrop", false)]
	[InlineData("CallContext.Enter(CallContext.Origin.BuildingDeathDrop);", "EnterBuildingDeathDrop", false)]
	public void TheCallMatcher_ReadsCallsAndIgnoresMentions(string statement, string name, bool expected) =>
		Assert.Equal(expected, Calls($"internal sealed class Sample {{ private void M() {{ {statement} }} }}", name));

	[Theory]
	[InlineData("origin.SupportCell = cell;", "SupportCell", true)]
	[InlineData("var cell = drop.SupportCell;", "SupportCell", true)]
	[InlineData("// origin.SupportCell = cell;", "SupportCell", false)]
	[InlineData("\"SupportCell\"", "SupportCell", false)]
	public void TheClauseMatcher_ReadsIdentifiersAndIgnoresMentions(string statement, string name, bool expected) =>
		Assert.Equal(expected, Reads($"internal sealed class Sample {{ private void M() {{ {statement} }} }}", name));

	[Theory]
	[InlineData("_breakState.TryAddBuildingDrop(drop, cell.x, cell.y);", true)]
	[InlineData("_breakState.TryAddBuildingDrop(drop, cell.x, other.y);", false)]
	[InlineData("_breakState.TryAddBuildingDrop(drop, 0, 0);", false)]
	[InlineData("_breakState.TryAddBuildingDrop(drop);", false)]
	[InlineData("// _breakState.TryAddBuildingDrop(drop, cell.x, cell.y);", false)]
	public void TheFolderMatcher_RequiresOneObjectsTwoAxes(string statement, bool expected) =>
		Assert.Equal(expected, PassesTheDropsOwnCell($"internal sealed class Sample {{ private void M() {{ {statement} }} }}"));

	[Theory]
	[InlineData("CallContext.TryGetBuildingDeathSupportCell(out var supportCell); origin.SupportCell = supportCell;", true)]
	[InlineData("CallContext.TryGetBuildingDeathSupportCell(out var supportCell); origin.SupportCell = null;", false)]
	[InlineData("CallContext.TryGetBuildingDeathSupportCell(out var supportCell); origin.SupportCell = other;", false)]
	[InlineData("// origin.SupportCell = supportCell;", false)]
	public void TheStampMatcher_RequiresThePublishedValue(string statement, bool expected) =>
		Assert.Equal(expected, StampsThePublishedCell($"internal sealed class Sample {{ private void M() {{ {statement} }} }}"));

	[Theory]
	[InlineData("CallContext.EnterBuildingDeathDrop(__instance.requireGround ? (Vector2Int?)__instance.blockPlacedOn : null);", true)]
	[InlineData("CallContext.EnterBuildingDeathDrop(__instance.blockPlacedOn);", false)]
	[InlineData("CallContext.EnterBuildingDeathDrop(null);", false)]
	[InlineData("CallContext.EnterBuildingDeathDrop(__instance.requireGround ? (Vector2Int?)default : null);", false)]
	public void TheScopeArgumentMatcher_RequiresTheOwnCellBehindTheGroundTest(string statement, bool expected) =>
		Assert.Equal(expected, PublishesOnlyTheBuildingsOwnCell($"internal sealed class Sample {{ private void M() {{ {statement} }} }}"));

	/// <summary>A call to the named method anywhere in the body — a comment or a string literal that names it is not a call.</summary>
	private static bool Calls(string body, string name) =>
		Invocations(body).Any(invocation => CalledName(invocation) == name);

	/// <summary>
	/// The scope is opened with the dying entity's own cell and nothing else: the argument is a conditional
	/// that asks `requireGround`, whose taken branch reads `blockPlacedOn` and whose other branch is `null`.
	/// </summary>
	private static bool PublishesOnlyTheBuildingsOwnCell(string body) =>
		Invocations(body)
			.Where(invocation => CalledName(invocation) == "EnterBuildingDeathDrop")
			.SelectMany(invocation => invocation.ArgumentList.Arguments)
			.Any(argument => argument.Expression is ConditionalExpressionSyntax conditional
				&& conditional.Condition.ToString().Contains("requireGround", StringComparison.Ordinal)
				&& conditional.WhenTrue.ToString().Contains("blockPlacedOn", StringComparison.Ordinal)
				&& conditional.WhenFalse.IsKind(SyntaxKind.NullLiteralExpression));

	/// <summary>
	/// The marker is stamped with the value the scope PUBLISHED: the assignment's source is the very local the
	/// query declared in its own `out`, so a stamp of a literal or of another local fails here.
	/// </summary>
	private static bool StampsThePublishedCell(string body)
	{
		var published = Invocations(body)
			.Where(invocation => CalledName(invocation) == "TryGetBuildingDeathSupportCell")
			.SelectMany(invocation => invocation.ArgumentList.Arguments)
			.Select(argument => (argument.Expression as DeclarationExpressionSyntax)?.Designation as SingleVariableDesignationSyntax)
			.FirstOrDefault(designation => designation is not null)?
			.Identifier.ValueText;
		if (published is null)
		{
			return false;
		}

		return Parse(body).DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Any(assignment => assignment.Left.ToString().EndsWith(".SupportCell", StringComparison.Ordinal)
				&& assignment.Right is IdentifierNameSyntax source
				&& string.Equals(source.Identifier.ValueText, published, StringComparison.Ordinal));
	}

	/// <summary>The fold reads the drop's own cell: three arguments, and the two the state reads are ONE object's `x` and `y`.</summary>
	private static bool PassesTheDropsOwnCell(string body) =>
		Invocations(body)
			.Where(invocation => CalledName(invocation) == "TryAddBuildingDrop")
			.Any(invocation =>
			{
				var arguments = invocation.ArgumentList.Arguments;
				return arguments.Count == 3
					&& arguments[1].Expression is MemberAccessExpressionSyntax xAxis
					&& string.Equals(xAxis.Name.Identifier.ValueText, "x", StringComparison.Ordinal)
					&& arguments[2].Expression is MemberAccessExpressionSyntax yAxis
					&& string.Equals(yAxis.Name.Identifier.ValueText, "y", StringComparison.Ordinal)
					&& string.Equals(xAxis.Expression.ToString(), yAxis.Expression.ToString(), StringComparison.Ordinal);
			});

	/// <summary>True when the body names the identifier (a read, a write or a named argument) — not a mention in prose.</summary>
	private static bool Reads(string body, string name) =>
		Parse(body).DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Any(identifier => string.Equals(identifier.Identifier.ValueText, name, StringComparison.Ordinal));

	private static string? CalledName(InvocationExpressionSyntax invocation) => invocation.Expression switch
	{
		MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		_ => null,
	};

	private static IEnumerable<InvocationExpressionSyntax> Invocations(string body) =>
		Parse(body).DescendantNodes().OfType<InvocationExpressionSyntax>();

	/// <summary>The body text of a method declared in one file, or a failed assertion naming the stale scan surface.</summary>
	private static string RequireMethodBody(string file, string typeName, string methodName)
	{
		var type = Parse(RepositoryPaths.ReadText(file)).DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, typeName, StringComparison.Ordinal));
		Assert.True(type is not null, $"{file} no longer declares `{typeName}` — this gate's scan surface is stale, not clean");

		var method = type!.Members.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal));
		Assert.True(method is not null, $"{file}: `{typeName}` no longer declares `{methodName}` — this gate's scan surface is stale, not clean");

		return method!.Body is not null ? method.Body.ToString() : method.ExpressionBody?.ToString() ?? string.Empty;
	}

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
