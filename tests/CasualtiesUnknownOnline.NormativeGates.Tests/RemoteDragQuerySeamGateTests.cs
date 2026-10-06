using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The display-body QUERY seam gate (ticket <c>remote-inventory-native-parity-rework</c> — batch
/// `20261006-f` lost matrix row 6's swap half to <c>UnityException: Transform child out of bounds</c>).
///
/// <para>
/// While the remote backpack is open the game's own release branch reads <c>PlayerCamera.body</c> — always
/// the LOCAL body — while the inventory ring and the dragged item belong to the displayed remote clone, so
/// CUO's predicate seams answer that branch's guards from the body the ring shows. Answering only the
/// PREDICATE is not enough, and that is the defect this gate keeps out: a native query whose body keeps
/// reading the local body after the redirect answered its guard runs on state the redirect did not answer.
/// <c>Body.GetItem(int)</c> is exactly that shape — its body guards on <c>HoldingItem(slot)</c> (answered
/// from the clone, so "occupied") and then indexes the LOCAL body's slot transform
/// (<c>this.slots[slot].transform.GetChild(0)</c>, <c>Body.cs:1346-1353</c>), which throws on an empty local
/// slot and takes the whole release with it: no intent, no refusal line, and <c>HandleReleaseDragging</c>
/// never reaches its own <c>dragItem = null</c>.
/// </para>
///
/// <para>
/// The rule is therefore about WHERE the answer happens: every seam that patches a <c>Body</c> query in
/// this file answers by SKIPPING the native body — a prefix that returns <c>false</c> on its answering
/// path. A postfix answers after the native body has already run on the local body. The scan surface is the
/// seam file itself, and the scope is the PATCHED TYPE rather than the file, because the same file carries
/// the one <c>PlayerCamera.OpenContainer</c> notification seam, whose native body must run (CUO only tracks
/// the container it showed) and which therefore keeps its postfix. The census holds a floor, so a renamed or
/// emptied scan fails loudly instead of checking nothing.
/// </para>
///
/// <para>
/// What is NOT reached: whether the redirected query answers the right body at runtime — the seam is
/// Harmony-bound to the game's own <c>Body</c>, which no test can instantiate, so the three-client
/// acceptance batch is the half that reads the swap on the owner's body (the same reading that found this
/// defect), and the matcher is pinned here with positive and negative samples. The matcher is a source
/// SHAPE check and says so: it reads where the answer happens (before the native body), that the answering
/// path exists, that the native path is still reachable, and that the seam consults the redirect decision —
/// it cannot read the CONDITION on that path, so a prefix that consults the redirect and branches on the
/// wrong side of it satisfies this gate and is the runtime's evidence to give.
/// </para>
/// </summary>
public class RemoteDragQuerySeamGateTests
{
	private const string PatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteDragPredicatePatches.cs";

	/// <summary>The patched type whose queries this gate governs: the game's own body, which indexes its OWN slot transforms.</summary>
	private const string PatchedType = "Body";

	/// <summary>The queries the release branch reads from the displayed body today (five, all in the seam file).</summary>
	private const int QuerySeamFloor = 5;

	/// <summary>The decisions a query seam must consult: what makes the answer the DISPLAYED body's own. The four slot/wearable queries ask <c>AnsweringBody</c>; the pickup check has its own bracket decision, <c>AnswersPickupCheckFor</c>.</summary>
	private static readonly string[] RedirectDecisions = ["AnsweringBody", "AnswersPickupCheckFor"];

	[Fact]
	public void EveryBodyQuerySeam_AnswersBySkippingTheNativeBody()
	{
		var source = RepositoryPaths.ReadText(PatchFile);

		var seams = QuerySeams(source, PatchedType);
		Assert.True(
			seams.Count >= QuerySeamFloor,
			$"{PatchFile} declares {seams.Count} `[HarmonyPatch(typeof({PatchedType}), …)]` seam(s), below the floor of {QuerySeamFloor} — either the answer moved out of this file, or the scan surface is stale; both need this gate updated in the same change");

		var runsTheNativeBody = seams.Where(seam => !SkipsTheNativeBody(seam)).ToList();
		Assert.True(
			runsTheNativeBody.Count == 0,
			$"{PatchFile}: {runsTheNativeBody.Count} of {seams.Count} {PatchedType} query seam(s) do not answer by skipping the native body — {string.Join(", ", runsTheNativeBody.Select(Name))}. A postfix (or a prefix that never returns false) lets the native body read the LOCAL body's state after the redirect answered its guard: `Body.GetItem`'s own slot index threw `Transform child out of bounds` for exactly that reason and lost the whole release (batch `20261006-f`)");
	}

	[Theory]
	[InlineData("[HarmonyPatch(typeof(Body), \"HoldingItem\")] internal static class P { private static void Postfix(Body __instance, ref bool __result) { __result = true; } }", 1)]
	[InlineData("[HarmonyPatch(typeof(Body), \"HoldingItem\")] internal static class P { private static bool Prefix(Body __instance, ref bool __result) { if (View.AnsweringBody(__instance) is not { } answering) { return true; } __result = answering.HoldingItem(); return false; } }", 0)]
	[InlineData("[HarmonyPatch(typeof(Body), \"GetItem\")] internal static class P { private static bool Prefix(Body __instance, ref Item __result) { __result = View.AnsweringBody(__instance).GetItem(slot); return true; } }", 1)]
	[InlineData("[HarmonyPatch(typeof(PlayerCamera), \"OpenContainer\")] internal static class P { private static void Postfix(Container cont) { Track(cont); } }", 0)]
	[InlineData("// a doc mention of [HarmonyPatch(typeof(Body), \"GetItem\")]", 0)]
	// Answers unconditionally: no native path left, and the redirect is never consulted.
	[InlineData("[HarmonyPatch(typeof(Body), \"GetWearable\")] internal static class P { private static bool Prefix(ref Item __result) { __result = null; return false; } }", 1)]
	// Skips the body on a NON-answering path and runs it on the answering one — the shape that answers nothing.
	[InlineData("[HarmonyPatch(typeof(Body), \"GetItem\")] internal static class P { private static bool Prefix(Body __instance, int slot, ref Item __result) { if (!View.NamesASlotOfTheRing(slot)) { return false; } __result = __instance.GetItem(slot); return true; } }", 1)]
	// Consults the redirect but never lets the native answer through: every other query would be answered by the clone.
	[InlineData("[HarmonyPatch(typeof(Body), \"GetItem\")] internal static class P { private static bool Prefix(Body __instance, int slot, ref Item __result) { if (View.AnsweringBody(__instance) is not { } answering) { return false; } __result = answering.GetItem(slot); return false; } }", 1)]
	public void TheSeamMatcher_ReadsWhereTheAnswerHappens(string snippet, int expectedUnanswered) =>
		Assert.Equal(expectedUnanswered, QuerySeams($"internal static class Sample {{ {snippet} }}", PatchedType).Count(seam => !SkipsTheNativeBody(seam)));

	/// <summary>Every nested class in a source that patches one type, read from the syntax tree — a doc mention is not an attribute.</summary>
	private static List<ClassDeclarationSyntax> QuerySeams(string source, string patchedType) =>
		[.. Parse(source).DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Where(candidate => string.Equals(PatchedTypeOf(candidate), patchedType, StringComparison.Ordinal))];

	/// <summary>
	/// True when the seam answers BEFORE the native body and still leaves the native answer reachable: a prefix
	/// returning bool whose body has a <c>return false</c> path, a <c>return true</c> path, and the redirect
	/// decision named in the code it runs (<see cref="RedirectDecisions"/>). A postfix answers after the body; a
	/// prefix that never returns false answers nothing; one that never returns true answers EVERYTHING, so the
	/// game's own bodies would never run; one that skips the body without consulting the redirect does not answer
	/// from the displayed body at all.
	///
	/// What this cannot read: the CONDITION on the answering path. A prefix can name the redirect decision and
	/// still branch on the wrong side of it, which is the runtime's evidence to give (the self-check's limits say
	/// so); the matcher reads where the answer happens and that both paths exist.
	/// </summary>
	private static bool SkipsTheNativeBody(ClassDeclarationSyntax seam)
	{
		var answer = seam.Members
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(method => string.Equals(method.Identifier.ValueText, "Prefix", StringComparison.Ordinal));
		if (answer is null || answer.ReturnType.ToString() != "bool")
		{
			return false;
		}

		var returns = answer.DescendantNodes()
			.OfType<ReturnStatementSyntax>()
			.Select(ret => ret.Expression)
			.OfType<LiteralExpressionSyntax>()
			.ToList();

		var body = answer.Body?.ToString() ?? string.Empty;
		return returns.Any(literal => literal.IsKind(SyntaxKind.FalseLiteralExpression))
			&& returns.Any(literal => literal.IsKind(SyntaxKind.TrueLiteralExpression))
			&& RedirectDecisions.Any(decision => body.Contains(decision, StringComparison.Ordinal));
	}

	/// <summary>The first type argument of a class's <c>[HarmonyPatch(typeof(…), …)]</c>, or null when it patches no type.</summary>
	private static string? PatchedTypeOf(ClassDeclarationSyntax candidate) =>
		candidate.AttributeLists
			.SelectMany(list => list.Attributes)
			.Where(attribute => attribute.Name.ToString() is "HarmonyPatch" or "HarmonyLib.HarmonyPatch")
			.SelectMany(attribute => attribute.ArgumentList?.Arguments ?? default)
			.Select(argument => argument.Expression)
			.OfType<TypeOfExpressionSyntax>()
			.Select(type => type.Type.ToString())
			.FirstOrDefault();

	private static string Name(ClassDeclarationSyntax seam) => $"`{seam.Identifier.ValueText}`";

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
