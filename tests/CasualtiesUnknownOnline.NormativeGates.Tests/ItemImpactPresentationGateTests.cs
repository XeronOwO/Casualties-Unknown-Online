using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The world-item impact presentation family (ticket
/// <c>backlog/todo/suppressed-native-call-sounds-stay-unheard</c>): the two
/// native collision presentations <c>NonAuthoritativeItemImpactPolicy</c>
/// suppresses on a guest copy — <c>Item.OnCollisionEnter2D</c> (the <c>drop</c>
/// clip, the landing block's step sound and the <c>DustMini</c> puff) and
/// <c>PlushScript.OnCollisionEnter2D</c> (the plush's own squeak) — must reach
/// the other members as the AUTHORITY's presentation, or a guest hears and sees
/// nothing when a world item lands, its own drops included.
///
/// <para>
/// Why a gate rather than only unit tests: the defect is a division-of-labour
/// defect. The suppression lives in one guard, the report has to sit on the side
/// that still simulates the landing, the wire carries a position rather than a
/// call identity, and the receiver has to replay the game's OWN presentation
/// (the clip the host played, the step sound the receiver's own world picks for
/// the landing block, and the dust) — none of which is visible in a pure
/// function. The pure halves are pinned where they live: the presentation
/// decision is a Runtime table the test project evaluates directly, and the
/// authority/suppression truth table is exercised reflectively by
/// <c>CasualtiesUnknownOnline.Tests/Patching/NonAuthoritativeItemImpactPolicyTests</c>.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the anchor pins read OUR OWN patch sources
/// with a matcher over the one-line <c>[HarmonyPatch(typeof(...), "...")]</c>
/// form (a SPLIT anchor, the <c>nameof(...)</c> form and a manual
/// <c>PatchProcessor</c> call are outside their reach — the game-assembly
/// contract tests own that half). The decompiled game tree is NOT in the
/// repository, so nothing here can notice a NEW native impact presentation the
/// game adds; what they notice is a reported or suppressed family member moving
/// without this census being reviewed.
/// </para>
/// </summary>
public class ItemImpactPresentationGateTests
{
	private const string ItemPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/ItemCollisionEnter2DPatch.cs";

	private const string PlushPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/PlushScriptCollisionEnter2DPatch.cs";

	private const string PolicyFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Items/NonAuthoritativeItemImpactPolicy.cs";

	private const string GuardFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/NonAuthoritativeItemImpactGuard.cs";

	private const string NetMsgFile =
		"src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs";

	private const string MessageFile =
		"src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/ItemImpactMsg.cs";

	private const string HandlerFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/Handlers/ItemImpactHandler.cs";

	private const string SyncFile =
		"src/CasualtiesUnknownOnline.GameAdapter/World/WorldItemImpactSync.cs";

	private const string ReplayFile =
		"src/CasualtiesUnknownOnline.GameAdapter/World/WorldItemImpactReplay.cs";

	private const string PresentationFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemImpactPresentation.cs";

	[Fact]
	public void TheSuppressedImpactFamily_IsReportedByTheAuthoritySideOnly()
	{
		// The family is defined by the guard the guest half already uses: whatever
		// that guard suppresses on a non-authoritative copy is exactly what the
		// authority side must report. The TWO members censused here are pinned; a
		// third suppressed presentation the game adds is outside this pin's reach
		// (it reads the two known collision hooks, not the guard's call sites).
		Assert.True(File.Exists(RepositoryPaths.File(ItemPatchFile)), $"{ItemPatchFile} is missing");
		Assert.True(File.Exists(RepositoryPaths.File(PlushPatchFile)), $"{PlushPatchFile} is missing");

		var itemPatch = RepositoryPaths.ReadText(ItemPatchFile);
		var plushPatch = RepositoryPaths.ReadText(PlushPatchFile);
		var guard = RepositoryPaths.ReadText(GuardFile);
		var policy = RepositoryPaths.ReadText(PolicyFile);

		Assert.True(AnchorsOn(itemPatch, "Item", "OnCollisionEnter2D"), "the item impact family must keep its Item.OnCollisionEnter2D hook");
		Assert.True(AnchorsOn(plushPatch, "PlushScript", "OnCollisionEnter2D"), "the plush squeak must ride the same family");

		Assert.Contains("ShouldSuppress", guard, StringComparison.Ordinal);
		Assert.Contains("ShouldReport", policy, StringComparison.Ordinal);
		Assert.Contains("ShouldSuppress", policy, StringComparison.Ordinal);
		Assert.Contains("ShouldReport", itemPatch, StringComparison.Ordinal);
		Assert.Contains("ShouldReport", plushPatch, StringComparison.Ordinal);
	}

	[Fact]
	public void TheImpactReport_KeepsTheNativeThresholdAndTheOncePerCollisionShape()
	{
		// The native bodies decide by the collision's own relative velocity
		// (> 3 for an item, > 2 for a plush, Item.cs:238-247 / PlushScript.cs:17-23).
		// The report rides the SAME condition instead of inventing a second one, and
		// it lives in the COLLISION callback (the patch's postfix) — the bodies are
		// read, not the files, so a report moved into any other method fails here.
		var itemPostfix = BodyOf(RepositoryPaths.ReadText(ItemPatchFile), "Postfix");
		var plushPostfix = BodyOf(RepositoryPaths.ReadText(PlushPatchFile), "Postfix");

		Assert.True(itemPostfix.Length > 0, "the item patch must report from its collision postfix");
		Assert.True(plushPostfix.Length > 0, "the plush patch must report from its collision postfix");

		Assert.Contains("relativeVelocity.magnitude", itemPostfix, StringComparison.Ordinal);
		Assert.Contains("3f", itemPostfix, StringComparison.Ordinal);
		Assert.Contains("ShouldReport", itemPostfix, StringComparison.Ordinal);
		Assert.Contains("relativeVelocity.magnitude", plushPostfix, StringComparison.Ordinal);
		Assert.Contains("2f", plushPostfix, StringComparison.Ordinal);
		Assert.Contains("ShouldReport", plushPostfix, StringComparison.Ordinal);
	}

	[Fact]
	public void TheImpactEvent_IsAHostToGuestWorldEvent()
	{
		// One impact = one event, and only the authority side may send it: a guest
		// copy is the suppressed half, so a guest that reported its own collision
		// would re-introduce exactly the ghost presentation the guard removes.
		Assert.True(File.Exists(RepositoryPaths.File(MessageFile)), $"{MessageFile} is missing — the impact needs its own world-event carrier");
		Assert.True(File.Exists(RepositoryPaths.File(HandlerFile)), $"{HandlerFile} is missing — a message without a handler is dead wire");

		var messages = RepositoryPaths.ReadText(NetMsgFile);
		var message = RepositoryPaths.ReadText(MessageFile);
		var handler = RepositoryPaths.ReadText(HandlerFile);

		Assert.Matches(@"ItemImpact = \d+,", messages);
		Assert.Contains("NetVector2Msg", message, StringComparison.Ordinal);
		Assert.Contains("ItemImpactKind", message, StringComparison.Ordinal);
		Assert.Contains("[PacketHandler(NetMsg.ItemImpact, NetMessageDirection.HostToGuest)]", handler, StringComparison.Ordinal);
	}

	[Fact]
	public void TheImpactReplay_RunsUnderRemoteApplyAndUsesTheGamesOwnPresentation()
	{
		// The receiver replays what the authority's native call presented, through
		// the game's OWN calls: the exact clip the host played, the landing block's
		// step sound as THIS world picks it (the same RandomStepSound the native
		// body calls), and the same dust object. The replay is a presentation, so
		// it runs under RemoteApply — the capture must never report it back.
		Assert.True(File.Exists(RepositoryPaths.File(SyncFile)), $"{SyncFile} is missing");
		Assert.True(File.Exists(RepositoryPaths.File(ReplayFile)), $"{ReplayFile} is missing");

		var replay = RepositoryPaths.ReadText(ReplayFile);

		Assert.Contains("CallContext.Origin.RemoteApply", replay, StringComparison.Ordinal);
		Assert.Contains("Sound.Play(\"drop\"", replay, StringComparison.Ordinal);
		Assert.Contains("RandomStepSound(", replay, StringComparison.Ordinal);
		Assert.Contains("\"DustMini\"", replay, StringComparison.Ordinal);
	}

	[Fact]
	public void ThePresentationDecision_IsAPureRuntimeTable()
	{
		// Which parts of the native presentation a received event replays is a
		// DECISION, not a code path: it lives in Runtime so the test project can
		// evaluate it directly (the adapter only executes it).
		Assert.True(File.Exists(RepositoryPaths.File(PresentationFile)), $"{PresentationFile} is missing — the replay decision must be a pure table");

		var presentation = RepositoryPaths.ReadText(PresentationFile);

		Assert.Contains("PlaysDropClip", presentation, StringComparison.Ordinal);
		Assert.Contains("PlaysLandingStepClip", presentation, StringComparison.Ordinal);
		Assert.Contains("SpawnsDust", presentation, StringComparison.Ordinal);
		Assert.Contains("PlaysOwnClip", presentation, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("[HarmonyPatch(typeof(Item), \"OnCollisionEnter2D\")]", "Item", "OnCollisionEnter2D", true)]
	[InlineData("[HarmonyPatch(typeof(PlushScript), \"OnCollisionEnter2D\")]", "Item", "OnCollisionEnter2D", false)]
	[InlineData("// a doc mention of [HarmonyPatch(typeof(Item), \"OnCollisionEnter2D\")]", "Item", "OnCollisionEnter2D", false)]
	[InlineData("[HarmonyPatch(typeof(Item), nameof(Item.OnCollisionEnter2D))]", "Item", "OnCollisionEnter2D", false)]
	public void TheAnchorMatcher_ReadsTheDeclaringTypeAndTheMethod(
		string source, string declaringType, string method, bool expected) =>
		Assert.Equal(expected, AnchorsOn(source, declaringType, method));

	[Theory]
	[InlineData("private static bool Prefix(Item __instance)\n{\n\tif (ShouldReport(__instance))\n\t{\n\t\treturn true;\n\t}\n}", "ShouldReport(", true)]
	[InlineData("private static bool Prefix(Item __instance)\n{\n\treturn true;\n}", "ShouldReport(", false)]
	public void ThePatchBodyReader_ReadsOnlyTheBody(string source, string marker, bool expected) =>
		Assert.Equal(expected, BodyOf(source, "Prefix").Contains(marker, StringComparison.Ordinal));

	/// <summary>The anchor form the pins read: the declaring type AND the method, never a bare method name — and never a mention inside a line comment.</summary>
	private static bool AnchorsOn(string source, string declaringType, string method) =>
		Regex.IsMatch(
			Regex.Replace(source, @"//[^\n]*", ""),
			$@"\[HarmonyPatch\(typeof\({Regex.Escape(declaringType)}\), ""{Regex.Escape(method)}""\)\]");

	/// <summary>A patch method's own body — its signature up to the next closing brace at the class-body indentation.</summary>
	private static string BodyOf(string source, string method)
	{
		var clean = Regex.Replace(source, @"//[^\n]*", "");
		var start = clean.IndexOf(method + "(", StringComparison.Ordinal);
		if (start < 0)
		{
			return "";
		}

		var end = clean.IndexOf("\n\t}", start, StringComparison.Ordinal);
		return end < 0 ? clean[start..] : clean[start..end];
	}
}
