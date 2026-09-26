using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The item and body one-shot family the ingest cycle left ticketed (ticket
/// <c>backlog/review/unhooked-item-and-body-sound-families</c>): the medical,
/// world-drink, inventory-gesture, item-use-feedback and body-coroutine clips
/// must be PRESENTED on every other side through the same one-shot capture chain
/// the rest of the character sounds already ride — and the census that decides
/// WHICH clips are carried must be a pinned decision rather than whatever the
/// policy happens to list.
///
/// <para>
/// Why a gate rather than only unit tests: the defect is a ROUTING defect. A
/// clip is captured only when a <c>CallContext</c> scope the two
/// <c>Sound.Play</c> patches map is active while the native call runs; the
/// medical clips play inside <c>PlayerCamera.ApplyWoundItem</c>, the
/// world-drink clip inside <c>FluidManager.DrinkLiquid</c>, the inventory
/// gestures inside scopes CUO opens for its own reasons, and the body
/// one-shots inside coroutines whose body runs after the patched method
/// returned — none of which is visible in a pure function. The classification
/// itself is covered behaviourally by
/// <c>CasualtiesUnknownOnline.Tests/Session/CharacterSoundPolicyTests</c> and
/// the wire by <c>CharacterSoundSyncTests</c>.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the anchor pins read OUR OWN patch
/// sources with a matcher over the one-line <c>[HarmonyPatch(typeof(...), "...")]</c>
/// form — a SPLIT anchor (the type in one attribute, the method name in
/// another), the <c>nameof(...)</c> form and a manual <c>PatchProcessor</c> call
/// are outside their reach (the game-assembly contract tests own that half, and
/// the matcher's own samples pin the forms it does and does not read). The
/// census pins read the policy's own decision rows and clip helpers; the REACH
/// of a set is pinned separately, because a set listed under one scope alone
/// leaves the sites that run under another scope silent — exactly the miss this
/// cycle's independent review found. The decompiled game tree is NOT in the
/// repository, so no pin here can notice a NEW native one-shot the game adds;
/// what they notice is a clip added to, removed from or moved between the
/// carried families without this census being reviewed, and — on the 2D half — a
/// scope anchored on a KNOWN 2D source rather than a new 2D cue the game adds.
/// </para>
/// </summary>
public class ItemAndBodySoundCaptureGateTests
{
	private const string PolicyFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/CharacterSoundPolicy.cs";

	private const string SoundPlayPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/SoundPlayPatch.cs";

	private const string MedicalPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/MedicalSoundPatches.cs";

	private const string WorldDrinkPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/WorldDrinkSoundPatches.cs";

	private const string GesturePatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/InventoryGestureSoundPatches.cs";

	private const string BodySoundPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/BodySoundPatches.cs";

	private const string CoroutineFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/ScopedCoroutine.cs";

	/// <summary>
	/// The carried census, one row per capture decision: the clips that decision
	/// must classify. A row's selector is either a policy <c>Origin</c> row (which
	/// may delegate to a clip helper) or the helper itself, so the pin follows
	/// the SAME expression the policy evaluates. The three item-use families are
	/// pinned by HELPER, not by origin, because a medical clip is reportable from
	/// the limb-treatment scope AND from the item-use scope (four censused sites
	/// play theirs from the item's world <c>useAction</c>); which origins consult
	/// which set is pinned by <c>TheItemUseRow_ConsultsTheIngestMedicalAndFeedbackSets</c>.
	/// </summary>
	private static readonly (string Selector, string[] Clips)[] Census =
	[
		("IsMedicalClip", ["boneweld", "cream", "drainuse", "goo", "laser", "spray", "splint", "syringe", "tweezeruse", "wrenchhit"]),
		("IsIngestClip", ["crystalenemylaugh", "drink", "eatCrunch", "eatFlesh", "glass", "pills"]),
		("IsItemUseFeedbackClip", ["centrifuge", "combine", "drop", "error", "flashlighttoggle"]),
		("Origin.WorldDrink", ["drink", "pills"]),
		("Origin.InventoryGesture", ["combine", "switch", "waterpour"]),
		("Origin.BodySound", ["dogshake", "stretch", "vomit1", "vomit2"]),
	];

	/// <summary>The census floor — a pin emptied alongside its policy would otherwise pass by checking nothing (the pinned census holds 30 clips).</summary>
	private const int MinimumCensusedClips = 26;

	/// <summary>
	/// The clips the decision leaves LOCAL, with the reason the ticket records:
	/// the two vomit prompts are 2D, position-less screen feedback —
	/// <c>Sound.Play("vomitwarning", Vector2.zero, true, …)</c> (Vomiter.cs:144/155)
	/// — i.e. the acting player's own HUD, not a world sound.
	/// </summary>
	private static readonly string[] LocalOnlyClips = ["bloodvomitwarning", "vomitwarning"];

	[Fact]
	public void TheMedicalFamily_IsAnchoredOnTheLocalCamerasLimbAction()
	{
		Assert.True(File.Exists(RepositoryPaths.File(MedicalPatchFile)), $"{MedicalPatchFile} is missing — the limb-treatment clips play inside PlayerCamera.ApplyWoundItem, which opened no capture scope");

		var patch = RepositoryPaths.ReadText(MedicalPatchFile);

		Assert.True(
			AnchorsOn(patch, "PlayerCamera", "ApplyWoundItem"),
			"the medical capture must bind PlayerCamera.ApplyWoundItem — the choke point both limb-action branches enter (the item's useLimbAction and the container's ApplyToLimb)");
		Assert.Contains("CallContext.Origin.CharacterMedicalUse", patch, StringComparison.Ordinal);
		Assert.Contains("CallContext.Enter(", patch, StringComparison.Ordinal);
	}

	[Fact]
	public void TheWorldDrinkFamily_IsAnchoredOnDrinkLiquid()
	{
		Assert.True(File.Exists(RepositoryPaths.File(WorldDrinkPatchFile)), $"{WorldDrinkPatchFile} is missing — both world-drink clips play inside FluidManager.DrinkLiquid, which opened no capture scope");

		var patch = RepositoryPaths.ReadText(WorldDrinkPatchFile);

		Assert.True(
			AnchorsOn(patch, "FluidManager", "DrinkLiquid"),
			"the world-drink capture must bind FluidManager.DrinkLiquid — its own water branch and the liquid registry's onDrink delegates both play inside it");
		Assert.Contains("CallContext.Origin.CharacterWorldDrink", patch, StringComparison.Ordinal);
		Assert.Contains("CallContext.Enter(", patch, StringComparison.Ordinal);
	}

	[Fact]
	public void TheInventoryGestureFamily_SplitsBetweenItsOwnScopeAndTheExistingOnes()
	{
		Assert.True(File.Exists(RepositoryPaths.File(GesturePatchFile)), $"{GesturePatchFile} is missing — Body.CombineLiquids plays \"waterpour\" and opened no capture scope");

		var patch = RepositoryPaths.ReadText(GesturePatchFile);
		var map = RepositoryPaths.ReadText(SoundPlayPatchFile);

		Assert.True(
			AnchorsOn(patch, "Body", "CombineLiquids"),
			"the gesture capture must bind Body.CombineLiquids — the transfer UI's finish, where \"waterpour\" plays");
		Assert.Contains("CallContext.Origin.CharacterInventoryGesture", patch, StringComparison.Ordinal);

		// The other two gestures ride scopes that already exist for other reasons.
		// No second, nested scope may be opened inside them: nesting a capture
		// origin there would hide the origin their own guards read.
		Assert.Contains("CallContext.Origin.InternalReorder => CharacterSoundPolicy.Origin.InventoryGesture", map, StringComparison.Ordinal);
		Assert.Contains("CallContext.Origin.Craft => CharacterSoundPolicy.Origin.InventoryGesture", map, StringComparison.Ordinal);
		Assert.False(
			AnchorsOn(patch, "Body", "SwitchHands") || AnchorsOn(patch, "Body", "SwapSlots") || AnchorsOn(patch, "Body", "CombineItems"),
			"the gesture patch must not add a second scope to SwitchHands / SwapSlots / CombineItems — they already run inside InternalReorder / Craft");
	}

	[Fact]
	public void TheBodyOneShotFamily_IsCapturedThroughTheCoroutineWrapper()
	{
		Assert.True(File.Exists(RepositoryPaths.File(BodySoundPatchFile)), $"{BodySoundPatchFile} is missing — the vomit, nap-stretch and water-shake clips play inside coroutine bodies, outside every capture scope");
		Assert.True(File.Exists(RepositoryPaths.File(CoroutineFile)), $"{CoroutineFile} is missing — a coroutine body runs after the patched method returned, so the scope has to be entered per step");

		var patch = RepositoryPaths.ReadText(BodySoundPatchFile);
		var wrapper = RepositoryPaths.ReadText(CoroutineFile);

		// Per routine, with the DECLARING type: the four native coroutines are the
		// census, and a same-named method on another type must not satisfy it.
		Assert.True(AnchorsOn(patch, "Vomiter", "DoVomit"), "BodySoundPatches must bind Vomiter.DoVomit (\"vomit1\"/\"vomit2\")");
		Assert.True(AnchorsOn(patch, "Vomiter", "DoBloodVomit"), "BodySoundPatches must bind Vomiter.DoBloodVomit (\"vomit1\"/\"vomit2\")");
		Assert.True(AnchorsOn(patch, "Body", "NapCoroutine"), "BodySoundPatches must bind Body.NapCoroutine (\"stretch\")");
		Assert.True(AnchorsOn(patch, "Body", "WaterShake"), "BodySoundPatches must bind Body.WaterShake (\"dogshake\")");

		Assert.Contains("ref IEnumerator __result", patch, StringComparison.Ordinal);
		Assert.Contains("ScopedCoroutine.Capture(", patch, StringComparison.Ordinal);
		Assert.Contains("CallContext.Origin.CharacterBodySound", patch, StringComparison.Ordinal);

		Assert.Contains("CallContext.Enter(", wrapper, StringComparison.Ordinal);
		Assert.Contains("MoveNext()", wrapper, StringComparison.Ordinal);
	}

	[Fact]
	public void EveryCensusRow_MatchesThePolicyClassification()
	{
		var policy = RepositoryPaths.ReadText(PolicyFile);

		Assert.True(
			Census.Sum(row => row.Clips.Length) >= MinimumCensusedClips,
			$"the pinned census holds only {Census.Sum(row => row.Clips.Length)} clip(s) — the pin was emptied, not the policy");

		foreach (var (selector, expected) in Census)
		{
			var actual = ClipsOf(policy, selector);
			Assert.True(
				actual.Count > 0,
				$"no clips could be read for `{selector}` — the policy row or its helper changed shape, or the decision was removed");
			Assert.True(
				CensusOf(expected) == CensusOf(actual),
				$"`{selector}` classifies [{string.Join(", ", actual)}] but the census pins [{string.Join(", ", expected)}] — a carried clip changed without the census being reviewed");
		}
	}

	[Fact]
	public void TheItemUseRow_ConsultsTheIngestMedicalAndFeedbackSets()
	{
		// The REACH of the medical set: four censused sites play their clip from
		// the item's world useAction (Item.cs:515 "splint", :1443 "goo",
		// :1658 "drainuse" and :7123 "syringe" through Item.DrawBlood), which runs
		// under CharacterItemUse — so a medical clip listed only under
		// Origin.Medical is carried nowhere for those sites. The independent review
		// of this cycle found exactly that, which is why the item-use row consults
		// all three sets instead of the feedback one alone.
		var policy = RepositoryPaths.ReadText(PolicyFile);
		var row = RowBody(policy, "ItemUse");

		Assert.Contains("IsIngestClip(clip)", row, StringComparison.Ordinal);
		Assert.Contains("IsMedicalClip(clip)", row, StringComparison.Ordinal);
		Assert.Contains("IsItemUseFeedbackClip(clip)", row, StringComparison.Ordinal);
		Assert.Contains("Origin.Medical => IsMedicalClip(clip) ? CharacterSoundKind.Medical : null,", policy, StringComparison.Ordinal);
	}

	[Fact]
	public void TheTwoDimensionalCues_OpenNoCaptureScope()
	{
		// The 2D, position-less cues stay the acting player's own (the user's
		// decision: every 3D world sound is carried, 2D screen feedback is not).
		// The two vomit prompts play in Vomiter.Vomit / VomitBlood, OUTSIDE the
		// wrapped routines; the syringe minigame's own cues (SyringeMinigame.cs:79
		// "bullethit" and :86 "syringe", both at Vector2.zero with the 2D flag)
		// play from the minigame's Update, which no patch anchors; the climb clips
		// are an AudioClip call with no world position. The pin's shape is stated
		// rather than implied: it notices a scope anchored on a KNOWN 2D source and
		// a 2D clip name entering the policy — it cannot notice a new 2D cue the
		// game adds, because the decompiled tree is not in this repository.
		var patches = string.Concat(
			RepositoryPaths.ReadText(MedicalPatchFile),
			RepositoryPaths.ReadText(WorldDrinkPatchFile),
			RepositoryPaths.ReadText(GesturePatchFile),
			RepositoryPaths.ReadText(BodySoundPatchFile));

		Assert.False(
			AnchorsOn(patches, "SyringeMinigame", "Update"),
			"the syringe minigame's 2D cues stay local — a scope around its Update would report the minigame UI as a world sound");
		Assert.False(
			AnchorsOn(patches, "Vomiter", "Vomit") || AnchorsOn(patches, "Vomiter", "VomitBlood"),
			"the 2D vomit prompts play in Vomiter.Vomit / VomitBlood, OUTSIDE the wrapped coroutines — anchoring those methods would capture them");

		var policy = RepositoryPaths.ReadText(PolicyFile);
		foreach (var clip in LocalOnlyClips)
		{
			Assert.DoesNotContain($"\"{clip}\"", policy, StringComparison.Ordinal);
		}
	}

	[Theory]
	[InlineData("[HarmonyPatch(typeof(PlayerCamera), \"ApplyWoundItem\")]", "PlayerCamera", "ApplyWoundItem", true)]
	[InlineData("[HarmonyPatch(typeof(FluidManager), \"ApplyWoundItem\")]", "PlayerCamera", "ApplyWoundItem", false)]
	[InlineData("[HarmonyPatch(typeof(Body), \"NapCoroutine\")]\n[HarmonyPatch(typeof(Body), \"WaterShake\")]", "Body", "WaterShake", true)]
	[InlineData("// a doc mention of [HarmonyPatch(typeof(Vomiter), \"DoVomit\")]", "Vomiter", "DoVomit", false)]
	[InlineData("[HarmonyPatch(typeof(Vomiter), nameof(Vomiter.DoVomit))]", "Vomiter", "DoVomit", false)]
	public void TheAnchorMatcher_ReadsTheDeclaringTypeAndTheMethod(
		string source, string declaringType, string method, bool expected) =>
		Assert.Equal(expected, AnchorsOn(source, declaringType, method));

	[Theory]
	[InlineData("Origin.Medical => IsMedicalClip(clip) ? CharacterSoundKind.Medical : null,\nprivate static bool IsMedicalClip(string clip) =>\n\tclip is \"syringe\" or \"splint\";", "Origin.Medical", 2, "splint")]
	[InlineData("Origin.BodySound => clip is \"stretch\" or \"dogshake\" ? CharacterSoundKind.BodySound : null,", "Origin.BodySound", 2, "dogshake")]
	[InlineData("private static bool IsItemUseFeedbackClip(string clip) =>\n\tclip is \"error\" or \"centrifuge\";", "IsItemUseFeedbackClip", 2, "error")]
	[InlineData("// a doc mention of Origin.WorldDrink => \"drink\"", "Origin.WorldDrink", 0, "")]
	public void TheClipCensus_ReadsTheRowOrItsHelper(string source, string selector, int expectedCount, string expectedClip)
	{
		var clips = ClipsOf(source, selector);

		Assert.Equal(expectedCount, clips.Count);
		if (expectedCount > 0)
		{
			Assert.Contains(expectedClip, clips);
		}
	}

	/// <summary>The anchor form the pins read: the declaring type AND the method, never a bare method name (a same-shaped anchor on another type must not pass) — and never a mention inside a line comment.</summary>
	private static bool AnchorsOn(string source, string declaringType, string method) =>
		Regex.IsMatch(
			Regex.Replace(source, @"//[^\n]*", ""),
			$@"\[HarmonyPatch\(typeof\({Regex.Escape(declaringType)}\), ""{Regex.Escape(method)}""\)\]");

	/// <summary>One policy row's own text (<c>Origin.ItemUse => …</c> up to its terminating comma), or "" when the row is gone.</summary>
	private static string RowBody(string source, string originName) =>
		Regex.Match(source, $@"Origin\.{Regex.Escape(originName)}\s*=>(?<body>.*?),", RegexOptions.Singleline) is { Success: true } row
			? row.Groups["body"].Value
			: "";

	/// <summary>The clips a policy row (or a clip helper) names — the same expression the policy evaluates at capture time.</summary>
	private static IReadOnlyList<string> ClipsOf(string source, string selector)
	{
		string body;
		if (selector.StartsWith("Origin.", StringComparison.Ordinal))
		{
			body = RowBody(source, selector["Origin.".Length..]);
			if (body.Length == 0)
			{
				return [];
			}
		}
		else
		{
			// The selector IS the helper name (e.g. `IsItemUseFeedbackClip`), so
			// the row-shaped body is the helper's own call.
			body = $"{selector}(clip)";
		}

		var helper = Regex.Match(body, @"Is(?<name>\w+Clip)\(clip\)");
		if (helper.Success)
		{
			var method = Regex.Match(
				source,
				$@"private static bool Is{Regex.Escape(helper.Groups["name"].Value)}\(string clip\) =>(?<body>.*?);",
				RegexOptions.Singleline);
			body = method.Success ? method.Groups["body"].Value : "";
		}

		return
		[
			.. Regex.Matches(body, "\"(?<clip>[^\"]+)\"")
				.Select(match => match.Groups["clip"].Value)
				.OrderBy(clip => clip, StringComparer.Ordinal),
		];
	}

	private static string CensusOf(IEnumerable<string> clips) =>
		string.Join(",", clips.OrderBy(clip => clip, StringComparer.Ordinal));
}
