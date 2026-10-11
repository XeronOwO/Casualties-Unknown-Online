using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The authority's grip-slip draw script reads the engine stream in the NATIVE order, and this gate is
/// the only thing that says so without a running game.
/// <para>
/// Both halves of the mechanism draw the same values only while they consume the same draws in the same
/// order: the operator's half is the game's own <c>ShrapnelMinigame.BreakGrasp</c>, and this file's
/// <c>DrawScript</c> is CUO's reproduction of it. The order includes two draws nobody keeps — the gore
/// clip index inside <c>Body.DoGoreSound</c> and the pitch shift inside <c>Sound.Play</c> — so deleting
/// either line, or moving one, leaves both screens drawing different pains while every behavioural test
/// stays green: the fake engine stream the unit and integration cases use has a script of its own and
/// cannot see this one. That is exactly the failure the runtime comparison in
/// <c>ShrapnelGripSlipRoller</c> reports AFTER the wrong damage was committed, so the cheap defence is
/// here, in front of it.
/// </para>
/// <para>
/// What this gate cannot do is read the game: <c>reversing/</c> is gitignored and absent from a fresh
/// clone, so the expected order is written out rather than derived, and each line names the native call
/// site it reproduces. A game update that changes the order is caught by the runtime comparison; this
/// gate catches CUO changing it by accident.
/// </para>
/// </summary>
public class ShrapnelGripSlipDrawOrderGateTests
{
	private const string DrawScriptFile = "src/CasualtiesUnknownOnline.GameAdapter/EngineShrapnelGripSlipRandom.cs";

	private const string DrawScriptMethod = "DrawScript";

	/// <summary>
	/// The native <c>BreakGrasp</c>'s own reads, in its own order, with the draw that produces each one:
	/// skin and bleed (<c>ShrapnelMinigame.cs:69-70</c>), the gore clip pick (<c>Body.cs:2445</c>), the
	/// pitch shift <c>Sound.Play</c> rolls because <c>DoGoreSound</c> asks for one with the default pitch
	/// (<c>Sound.cs:17</c>), pain (<c>ShrapnelMinigame.cs:72</c>), and the head limb's <c>0.8</c> gate
	/// with its brain loss (<c>:73-76</c>).
	/// </summary>
	private static readonly string[] NativeDrawOrder =
	[
		"Random.Range(4f, 6f)",
		"Random.Range(0.4f, 1f)",
		"Random.Range(1, 6)",
		"Random.Range(0.85f, 1.15f)",
		"Random.Range(9f, 16f)",
		"Random.value",
		"Random.value",
	];

	[Fact]
	public void TheAuthoritysDrawScript_ReadsTheEngineStreamInTheNativeOrder()
	{
		var draws = Draws(RepositoryPaths.ReadText(DrawScriptFile), DrawScriptMethod);

		Assert.True(
			draws.Count > 0,
			$"{DrawScriptFile}: no engine draw found in {DrawScriptMethod} — the gate read nothing, so it would pass over a deleted script");
		Assert.Equal(NativeDrawOrder, draws);
	}

	/// <summary>
	/// The extraction is order-sensitive and picks up only the engine's own reads, pinned with both
	/// samples so the gate above cannot pass by reading almost nothing.
	/// </summary>
	[Fact]
	public void Draws_ReadsOnlyTheEnginesOwnReads_AndInSourceOrder()
	{
		const string inOrder = """
			internal static class Sample
			{
				private static void Script()
				{
					Sound.Play("gore1", Vector2.zero);
					_ = Random.Range(4f, 6f);
					if (Random.value < 0.8f)
					{
						_ = Random.value;
					}
				}
			}
			""";

		Assert.Equal(["Random.Range(4f, 6f)", "Random.value", "Random.value"], Draws(inOrder, "Script"));

		const string reordered = """
			internal static class Sample
			{
				private static void Script()
				{
					if (Random.value < 0.8f)
					{
						_ = Random.value;
					}

					_ = Random.Range(4f, 6f);
				}
			}
			""";

		Assert.Equal(["Random.value", "Random.value", "Random.Range(4f, 6f)"], Draws(reordered, "Script"));
		Assert.NotEqual(Draws(inOrder, "Script"), Draws(reordered, "Script"));
	}

	/// <summary>
	/// Every engine read inside the named method, in source order. Only the engine's own stream counts —
	/// a <c>Random.state</c> save or a <c>Random.InitState</c> is the window itself, not a draw, and the
	/// script under test carries neither.
	/// </summary>
	internal static List<string> Draws(string source, string methodName)
	{
		var root = CSharpSyntaxTree
			.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();
		var method = root
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => candidate.Identifier.ValueText == methodName);
		if (method is null)
		{
			return [];
		}

		var reads = new List<(int Position, string Text)>();
		foreach (var node in method.DescendantNodes())
		{
			switch (node)
			{
				case InvocationExpressionSyntax invocation
					when invocation.Expression is MemberAccessExpressionSyntax access
						&& access.Expression.ToString() == "Random":
					reads.Add((invocation.SpanStart, invocation.ToString()));
					break;
				case MemberAccessExpressionSyntax member when member.ToString() == "Random.value":
					reads.Add((member.SpanStart, member.ToString()));
					break;
			}
		}

		return [.. reads.OrderBy(read => read.Position).Select(read => read.Text)];
	}
}
