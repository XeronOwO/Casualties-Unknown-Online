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
/// Gate for the Stage-3 (Other) medical terminal path. A session ends in exactly one place: the settlement
/// class owns <c>PrepareTerminal</c> (the defib drain), <c>Terminate</c> (the one terminal broadcast and the
/// sibling stop), <c>CompleteUnitOnTerminal</c> (the unit's completion rules), <c>StopOtherOperatorsOnTheUnit</c>
/// and <c>ReleaseSession</c> (the claims); the session service keeps the registry, the start/update entries and
/// the client surface, and hands the settlement its session table. Before the split the same 90 lines lived in
/// the 580-line session service, where the terminal rules could only grow.
///
/// <para>
/// Scan surface and its boundary, stated rather than implied: the two files named below, parsed through Roslyn
/// so a comment or a string literal that names a member is not a declaration. A member declared in another
/// partial file of the same type is invisible to this scan — the solution has no partial session types and the
/// one-type-per-file gate bounds the rest. The gate proves shape, not that the terminal bodies behave: the
/// runtime half is the smoke join (queue → continue → apply) plus the suite.
/// </para>
/// </summary>
public class OtherMedicalSettlementShapeGateTests
{
	private const string SettlementFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalOperationSettlement.cs";
	private const string SessionServiceFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalOperationSessionService.cs";
	private const string SettlementType = "OtherMedicalOperationSettlement";
	private const string SessionServiceType = "OtherMedicalOperationSessionService";

	private static readonly string[] TerminalMembers =
	[
		"PrepareTerminal",
		"Terminate",
		"CompleteUnitOnTerminal",
		"StopOtherOperatorsOnTheUnit",
		"ReleaseSession",
	];

	/// <summary>Every terminal member is declared by the settlement class — the one owner of a session's end.</summary>
	[Fact]
	public void TheSettlement_DeclaresTheTerminalPath()
	{
		Assert.True(File.Exists(RepositoryPaths.File(SettlementFile)),
			$"{SettlementFile} does not exist — the settlement half of the Stage-3 service has not been extracted");

		var declared = DeclaredMethodNames(SettlementFile, SettlementType);

		foreach (var member in TerminalMembers)
		{
			Assert.Contains(member, declared);
		}
	}

	/// <summary>The session service no longer declares the terminal path — the end of a session has one owner.</summary>
	[Fact]
	public void TheSessionService_NoLongerDeclaresTheTerminalPath()
	{
		var declared = DeclaredMethodNames(SessionServiceFile, SessionServiceType);

		foreach (var member in TerminalMembers)
		{
			Assert.DoesNotContain(member, declared);
		}
	}

	private static IReadOnlyList<string> DeclaredMethodNames(string file, string typeName)
	{
		var root = CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(file), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();

		var type = root.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, typeName, StringComparison.Ordinal));

		Assert.True(type is not null, $"{file} no longer declares `{typeName}` — this gate's scan surface is stale, not clean");

		return
		[
			.. type!.Members
				.OfType<MethodDeclarationSyntax>()
				.Select(method => method.Identifier.ValueText),
		];
	}
}
