using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The 1.0 endings gate (ticket <c>backlog/todo/1.0-endings-authority.md</c>, night batch `20261110`).
///
/// <para>
/// The release ends a run at the bottom layer with one of two branches — <c>MercEnding</c> when the host
/// carries the merchant's scarf, else <c>SurfaceBreachCutscene</c> — and every client resolves that branch
/// from its OWN body and scarf today, so two clients can play different endings. The ending is a property
/// of the run: the host records it, the run row carries it, and a member plays the recorded branch.
/// </para>
///
/// <para>
/// The rule this gate encodes: <c>RunState</c> carries a <c>RunEnding</c>; the host's projection stamps
/// the tracker's kind into the run it advances with; the adapter patches both native ending methods so the
/// host records the kind and the member plays the recorded one. A refactor that drops any of the three
/// returns the game to per-client endings.
/// </para>
/// </summary>
public class RunEndingGateTests
{
	private const string RunStateFile = "src/CasualtiesUnknownOnline.GameState/Domains/World/RunState.cs";
	private const string ProjectionFile = "src/CasualtiesUnknownOnline.Runtime/Session/World/WorldRunProjection.cs";
	private const string MercPatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/MercEndingPatch.cs";
	private const string BreachPatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/SurfaceBreachCutscenePatch.cs";

	[Fact]
	public void TheRunState_CarriesTheEnding()
	{
		var runState = RepositoryPaths.ReadText(RunStateFile);
		Assert.Contains("RunEnding Ending", runState);
	}

	[Fact]
	public void TheHostProjection_StampsTheEndingIntoTheAdvancedRun()
	{
		var body = RequireMethodBody(ProjectionFile, "WorldRunProjection", "CommitBaseline");
		Assert.Contains("RunEnding", body);
	}

	[Fact]
	public void TheAdapter_PatchesBothNativeEndingMethods()
	{
		Assert.Contains("MercEnding", RepositoryPaths.ReadText(MercPatchFile));
		Assert.Contains("SurfaceBreachCutscene", RepositoryPaths.ReadText(BreachPatchFile));
	}

	private static string RequireMethodBody(string file, string typeName, string methodName)
	{
		var tree = CSharpSyntaxTree.ParseText(RepositoryPaths.ReadText(file), new CSharpParseOptions(LanguageVersion.Preview));
		var type = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, typeName, StringComparison.Ordinal));
		Assert.True(type is not null, $"{file} no longer declares `{typeName}` — this gate's scan surface is stale, not clean");
		var method = type!.Members.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal));
		Assert.True(method is not null, $"{file}: `{typeName}` no longer declares `{methodName}` — this gate's scan surface is stale, not clean");
		return method!.Body is not null ? method.Body.ToString() : method.ExpressionBody?.ToString() ?? string.Empty;
	}
}
