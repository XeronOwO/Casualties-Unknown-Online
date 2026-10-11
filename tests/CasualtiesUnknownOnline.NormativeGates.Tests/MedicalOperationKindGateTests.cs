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
/// Every medical-operation message NAMES its own kind, and this gate is what says so without a running
/// game. The field is not decoration: the receiving client routes on it — the target's own body write
/// and the display/fact-table update are two separate branches — so a message that leaves it out does
/// not fail, it takes a DIFFERENT branch.
/// <para>
/// That is exactly what happened (2026-10-11, batch <c>20261011-a</c>): the shared shrapnel session's
/// state messages omitted the field, the enum's default was <c>Injection</c>, and the receiving client
/// therefore took the injection branch — which a shrapnel state cannot satisfy, because it carries no
/// <c>AppliedDose</c>. The authority's limb write was dropped in silence, no log line was produced, and
/// the target's next 1 Hz report carried its un-damaged limb straight back over the authority's own
/// copy. Behavioural tests could not see it: they assert the AUTHORITY's stored values, which the write
/// did reach. The default is now the <c>None</c> sentinel, so the omission is no longer plausible — and
/// this gate keeps a new builder from being written without the field at all.
/// </para>
/// <para>
/// What this gate cannot see, stated rather than implied: it matches two construction SHAPES — the
/// explicitly typed <c>new MedicalOperationStateMsg { … }</c> and the target-typed
/// <c>Message x = new() { … }</c> in a variable declarator. Three further shapes it does NOT reach, none
/// of which exists in <c>src/</c> today (measured 2026-10-11: all sixteen constructions are explicitly
/// typed): an assignment to an already-declared target (<c>out MedicalOperationStateMsg? state</c> and
/// then <c>state = new() { … }</c>), a target-typed <c>new()</c> handed straight to a call, and a
/// construction whose type the matcher cannot name. The backstops for those are the <c>None</c> sentinel
/// itself and the receivers' warnings, which name a kind-less message instead of acting on it in silence.
/// </para>
/// </summary>
public class MedicalOperationKindGateTests
{
	/// <summary>The five operation messages, which is the whole family that carries the field.</summary>
	private static readonly string[] KindedMessages =
	[
		"MedicalOperationStartRequestMsg",
		"MedicalOperationStartAckMsg",
		"MedicalOperationTargetCheckRequestMsg",
		"MedicalOperationStateMsg",
		"MedicalOperationEndCommittedMsg",
	];

	/// <summary>Census floor: the tree builds sixteen of these (measured 2026-10-11), and every one of the five types has at least one builder. A scan that finds fewer is a broken scan — the matcher or the source walk stopped resolving — not a clean tree.</summary>
	private const int ConstructionFloor = 16;

	[Fact]
	public void EveryOperationMessageConstruction_NamesItsKind()
	{
		var found = new List<string>();
		var failures = new List<string>();
		foreach (var path in SourceScan.SourceFiles(RepositoryPaths.File("src")))
		{
			var relative = path[RepositoryPaths.Root.Length..].TrimStart('\\', '/').Replace('\\', '/');
			foreach (var (message, kindAssignments) in Constructions(File.ReadAllText(path)))
			{
				found.Add($"{relative}:{message}");
				if (kindAssignments == 0)
				{
					failures.Add(
						$"{relative}: `new {message}` does not name its Kind — the receiving client would read the wire "
						+ "default and route the message down another family's branch");
				}
			}
		}

		if (found.Count < ConstructionFloor)
		{
			failures.Add($"the scan read {found.Count} construction(s) of these messages (floor {ConstructionFloor})");
		}

		foreach (var message in KindedMessages)
		{
			if (!found.Any(entry => entry.EndsWith($":{message}", StringComparison.Ordinal)))
			{
				failures.Add($"no builder of {message} was found at all — the gate no longer reaches it");
			}
		}

		Assert.True(
			failures.Count == 0,
			"a medical-operation message is built without naming its Kind." + Environment.NewLine
			+ string.Join(Environment.NewLine, failures));
	}

	/// <summary>
	/// The matcher is pinned with both shapes it must read and the ones it must not, so the gate above
	/// cannot pass by matching almost nothing.
	/// </summary>
	[Fact]
	public void TheMatcher_ReadsTheNamedAndTargetTypedShapes_AndNothingElse()
	{
		const string named = """
			internal static class Sample
			{
				private static MedicalOperationStateMsg Build() => new MedicalOperationStateMsg
				{
					OperationId = 7,
					Kind = MedicalOperationKind.Shrapnel,
					TargetLimbs = [],
				};
			}
			""";

		const string targetTyped = """
			internal static class Sample
			{
				private static void Build()
				{
					MedicalOperationEndCommittedMsg end = new()
					{
						OperationId = 7,
						Kind = MedicalOperationKind.Injection,
					};
				}
			}
			""";

		const string unnamed = """
			internal static class Sample
			{
				private static MedicalOperationStateMsg Build() => new MedicalOperationStateMsg
				{
					OperationId = 7,
					TargetLimbs = [],
				};
			}
			""";

		const string noInitializer = """
			internal static class Sample
			{
				private static MedicalOperationStartAckMsg Build() => new MedicalOperationStartAckMsg();
			}
			""";

		const string otherMessage = """
			internal static class Sample
			{
				private static CrystalStateMsg Build() => new CrystalStateMsg { Kind = 1 };
			}
			""";

		Assert.Equal(["MedicalOperationStateMsg:1"], Describe(named));
		Assert.Equal(["MedicalOperationEndCommittedMsg:1"], Describe(targetTyped));
		Assert.Equal(["MedicalOperationStateMsg:0"], Describe(unnamed));
		Assert.Equal(["MedicalOperationStartAckMsg:0"], Describe(noInitializer));
		Assert.Empty(Describe(otherMessage));
	}

	private static List<string> Describe(string source) =>
		[.. Constructions(source).Select(entry => $"{entry.Message}:{entry.KindAssignments}")];

	/// <summary>
	/// Every construction of one of the operation messages, with how many times its object initializer
	/// assigns <c>Kind</c>. Two shapes are read: the explicitly typed <c>new Message { … }</c> and the
	/// target-typed <c>Message x = new() { … }</c>.
	/// </summary>
	internal static List<(string Message, int KindAssignments)> Constructions(string source)
	{
		var root = CSharpSyntaxTree
			.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();

		var found = new List<(string Message, int KindAssignments)>();
		foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
		{
			if (SimpleName(creation.Type) is not { } message || !KindedMessages.Contains(message))
			{
				continue;
			}

			found.Add((message, KindAssignments(creation.Initializer)));
		}

		foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
		{
			if (declarator.Initializer?.Value is not ImplicitObjectCreationExpressionSyntax implicitCreation
				|| declarator.Parent is not VariableDeclarationSyntax declaration
				|| SimpleName(declaration.Type) is not { } message
				|| !KindedMessages.Contains(message))
			{
				continue;
			}

			found.Add((message, KindAssignments(implicitCreation.Initializer)));
		}

		return found;
	}

	private static int KindAssignments(InitializerExpressionSyntax? initializer) =>
		initializer is null
			? 0
			: initializer.Expressions
				.OfType<AssignmentExpressionSyntax>()
				.Count(assignment => SimpleName(assignment.Left) == "Kind");

	private static string? SimpleName(SyntaxNode node) => node switch
	{
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		QualifiedNameSyntax qualified => SimpleName(qualified.Right),
		MemberAccessExpressionSyntax access => SimpleName(access.Name),
		_ => null,
	};
}
