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

	private const string BandageMinigamePatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/BandageMinigameSoundPatches.cs";

	private const string GoreAmputationPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/AmputationMinigameSoundPatch.cs";

	private const string GoreShrapnelPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/ShrapnelMinigameSoundPatch.cs";

	private const string DisplayCaptureFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteMedicalDisplayCapture.cs";

	private const string TreatmentTableFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteMedicalTreatmentSoundCatalog.cs";

	private const string RemoteMedicalHandlerFile =
		"src/CasualtiesUnknownOnline.GameAdapter/RemoteMedicalOperationHandler.cs";

	private const string RemoteInjectionPatchFile =
		"src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteInjectionPatches.cs";

	private const string RemoteInjectionSessionFile =
		"src/CasualtiesUnknownOnline.GameAdapter/RemoteInjectionUseHandler.cs";

	private const string OtherMedicalCatalogFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteOtherMedicalCatalog.cs";

	private const string TopicalCatalogFile =
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteTopicalCatalog.cs";

	/// <summary>
	/// The vanilla injectable carriers, pinned HERE because the injection chain
	/// carries no id table any more: its admission rule is the game's own data
	/// (<c>ItemInfo.usableOnLimb</c> + <c>LiquidType.injectable</c>) since Part A of
	/// <c>mod-cross-player-native-semantics</c>, and a repository gate cannot read a
	/// game assembly. The ids are the <c>WaterContainerItem.Inject</c> call sites in
	/// the game's <c>Item.SetupItems()</c> (Item.cs), so a vanilla carrier this
	/// census misses is a reviewed edit here rather than a silent gap — the same
	/// shape, and the same declared reach, as the rest of this gate.
	/// </summary>
	private static readonly string[] VanillaInjectableCarriers =
	[
		"morphine",
		"syringe",
		"opium",
		"heroin",
		"naloxone",
		"fentanyl",
		"ceftriaxone",
		"antiserum",
		"bloodcoagulant",
		"combatpen",
		"streptokinase",
		"bloodbag",
		"saline",
		"ringersolution",
		"bloodbaghuman",
	];

	/// <summary>The catalogs whose entries define what the remote limb gesture accepts — the scan surface the treatment table must decide for, derived from the eligibility check itself rather than restated.</summary>
	private static readonly string[] AcceptedItemCatalogFiles =
	[
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteHealProfiles.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteBandageMinigameCatalog.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/RemoteLimbToolCatalog.cs",
	];

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
		("IsMedicalClip", ["bandage", "boneweld", "cream", "drainuse", "goo", "gore", "gore1", "gore2", "gore3", "gore4", "gore5", "laser", "spray", "splint", "syringe", "tweezeruse", "wrenchhit"]),
		("IsIngestClip", ["crystalenemylaugh", "drink", "eatCrunch", "eatFlesh", "glass", "pills"]),
		("IsItemUseFeedbackClip", ["centrifuge", "combine", "drop", "error", "flashlighttoggle"]),
		("Origin.WorldDrink", ["drink", "pills"]),
		("Origin.InventoryGesture", ["combine", "switch", "waterpour"]),
		("Origin.BodySound", ["dogshake", "stretch", "vomit1", "vomit2"]),
	];

	/// <summary>The census floor — a pin emptied alongside its policy would otherwise pass by checking nothing (the pinned census holds 37 clips).</summary>
	private const int MinimumCensusedClips = 33;

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
	public void TheBandageMinigameStep_IsCapturedInsideTheLimbTreatmentScope()
	{
		// The bandage family's clip is NOT played by the delegate ApplyWoundItem
		// runs: that delegate only STARTS the native minigame. The wrap that plays
		// "bandage" (BandageMinigame.cs:112, 3D, at the item) happens frames later,
		// after ApplyWoundItem returned and its scope closed — so both the local
		// treatment and the remote one (CUO drives the same native minigame) left
		// the clip on the acting client alone. The scope therefore belongs on the
		// minigame's PER-STEP physics update, not on the limb action.
		Assert.True(
			File.Exists(RepositoryPaths.File(BandageMinigamePatchFile)),
			$"{BandageMinigamePatchFile} is missing — BandageMinigame.PhysicsUpdate plays \"bandage\" outside every capture scope");

		var patch = RepositoryPaths.ReadText(BandageMinigamePatchFile);

		Assert.True(
			AnchorsOn(patch, "BandageMinigame", "PhysicsUpdate"),
			"the bandage capture must bind BandageMinigame.PhysicsUpdate — the step that plays \"bandage\" when a wrap completes");
		var code = WithoutComments(patch);

		Assert.Contains("CallContext.Origin.CharacterMedicalUse", code, StringComparison.Ordinal);
		Assert.Contains("CallContext.Enter(", code, StringComparison.Ordinal);
		Assert.Contains("CaptureScopeGuard.IsLocalAction()", code, StringComparison.Ordinal);
	}

	[Fact]
	public void TheGoreClips_AreCapturedInsideTheMinigameStepsThatPlayThem()
	{
		// The gore clips have no limb-action producer: the limb's own Dismember
		// plays them (Limb.cs:91-99), and the two minigames reach it from their
		// own step — the amputation minigame's completion (AmputationMinigame.cs:73-75)
		// and the shrapnel minigame's broken grasp, called from its own Update
		// (ShrapnelMinigame.cs:111/116 → :61-71). Both steps run frames after the
		// limb action that started the minigame returned. The remote amputation is
		// where it showed: the OPERATOR's minigame dismembers the displayed body's
		// limb while the patient's client applies the dismemberment through the
		// kernel projection, so its game never calls Dismember — the operator heard
		// the clip, every peer heard nothing.
		Assert.True(
			File.Exists(RepositoryPaths.File(GoreAmputationPatchFile)),
			$"{GoreAmputationPatchFile} is missing — the amputation minigame's completion plays \"gore\"/\"gore{{N}}\" outside every capture scope");
		Assert.True(
			File.Exists(RepositoryPaths.File(GoreShrapnelPatchFile)),
			$"{GoreShrapnelPatchFile} is missing — the shrapnel minigame's broken grasp plays \"gore{{N}}\" outside every capture scope");

		var amputation = RepositoryPaths.ReadText(GoreAmputationPatchFile);
		var shrapnel = RepositoryPaths.ReadText(GoreShrapnelPatchFile);

		Assert.True(
			AnchorsOn(amputation, "AmputationMinigame", "Update"),
			"the gore capture must bind AmputationMinigame.Update — the step whose completion calls Limb.Dismember");
		Assert.True(
			AnchorsOn(shrapnel, "ShrapnelMinigame", "Update"),
			"the gore capture must bind ShrapnelMinigame.Update — the step that can break a grasp and play the body's gore roll");

		foreach (var (file, patch) in new[] { (GoreAmputationPatchFile, amputation), (GoreShrapnelPatchFile, shrapnel) })
		{
			var code = WithoutComments(patch);
			Assert.Contains("CallContext.Enter(CallContext.Origin.CharacterMedicalUse)", code, StringComparison.Ordinal);
			Assert.Contains("CaptureScopeGuard.IsLocalAction()", code, StringComparison.Ordinal);
			Assert.Contains("__state?.Dispose();", code, StringComparison.Ordinal);

			// The POSITION half, which no anchor can see and the operator's own play
			// depends on: the remote path's native call reports the displayed body's
			// parked transform (RemoteMedicalCoordinator parks it at (0, -10000)), so
			// the window has to re-point it at the patient's own render clone — the
			// same re-point PlayTreatmentSound makes for the treatment table.
			Assert.Contains("RemoteMedicalDisplayCapture.Enter(", code, StringComparison.Ordinal);
		}

		Assert.True(
			File.Exists(RepositoryPaths.File(DisplayCaptureFile)),
			$"{DisplayCaptureFile} is missing — the remote step scopes would report an off-world position");

		var capture = WithoutComments(RepositoryPaths.ReadText(DisplayCaptureFile));
		Assert.Contains("RemoteMedicalView.TargetSteamId", capture, StringComparison.Ordinal);
		Assert.Contains("IPlayerAnchorQuery", capture, StringComparison.Ordinal);
		Assert.Contains("TryGetRemoteHeadPosition(RemoteMedicalView.TargetSteamId", capture, StringComparison.Ordinal);
		Assert.Contains("body.transform.position = new Vector3(x, y, previous.z);", capture, StringComparison.Ordinal);
		Assert.Contains("_body.transform.position = _previous;", capture, StringComparison.Ordinal);

		// The shared shrapnel session has an OBSERVER copy of the same minigame; the
		// operator already reported the removal, so the observer's copy must never
		// report a second play (the local-action guard alone cannot tell them apart).
		Assert.Contains(
			"IsObserverShrapnelMinigame(",
			WithoutComments(shrapnel),
			StringComparison.Ordinal);

		// The clips the steps produce are the medical set's own knowledge: without
		// them both steps stay silent for every peer however the scope is bound.
		var medical = ClipsOf(RepositoryPaths.ReadText(PolicyFile), "IsMedicalClip");
		foreach (var clip in new[] { "gore", "gore1", "gore2", "gore3", "gore4", "gore5" })
		{
			Assert.Contains(clip, medical);
		}
	}

	[Fact]
	public void TheRemoteTreatmentTable_DecidesEveryAcceptedMedicalItem()
	{
		// The remote view blocks the native limb action, so the clip the native
		// delegate would have played has to be played by the medical domain — and
		// the clip identity is native knowledge the blocked call took with it.
		// The table is that knowledge, and this pin makes it a DECIDED row per
		// accepted item: a new catalog entry cannot ship undecided, and a clip
		// invented outside the medical set cannot ship at all.
		Assert.True(
			File.Exists(RepositoryPaths.File(TreatmentTableFile)),
			$"{TreatmentTableFile} is missing — the remote limb treatment plays no clip without it");

		var table = RepositoryPaths.ReadText(TreatmentTableFile);
		var accepted = AcceptedItemIds();

		foreach (var file in AcceptedItemCatalogFiles)
		{
			Assert.True(
				KeyIds(RepositoryPaths.ReadText(file), "Registry").Count > 0,
				$"no item ids could be read from {file} — its items would then look decided by absence");
		}

		Assert.True(accepted.Count >= 45, $"only {accepted.Count} accepted medical item id(s) could be read from the catalogs — one catalog stopped being read, or the surface was emptied");

		var decided = new HashSet<string>(StringComparer.Ordinal);
		foreach (var id in KeyIds(table, "TreatmentClips"))
		{
			Assert.True(decided.Add(id), $"`{id}` is decided twice in the treatment table");
		}

		foreach (var id in LiteralIds(table, "UncarriedItems"))
		{
			Assert.True(decided.Add(id), $"`{id}` is decided twice in the treatment table (silent rows must not repeat a clip row)");
		}

		foreach (var id in LiteralIds(table, "LiquidDrivenItems"))
		{
			Assert.True(decided.Add(id), $"`{id}` is decided twice in the treatment table (a liquid-driven item must not repeat another row)");
		}

		Assert.True(decided.Count >= 34, $"the treatment table decides only {decided.Count} item(s) — the table (or one of its groups) was emptied, not the decision");

		// The SECOND decider: the injection family plays its own clip natively (the
		// operator's client runs the item's own useLimbAction inside the medical
		// capture window), so its carriers are decided by the game's delegate and
		// must NOT also carry a table row — two deciders for one clip is a double
		// play, which is exactly what a migrated row would ship.
		var native = new HashSet<string>(VanillaInjectableCarriers, StringComparer.Ordinal);
		var doubled = decided.Where(native.Contains).OrderBy(id => id, StringComparer.Ordinal).ToArray();
		Assert.True(
			doubled.Length == 0,
			$"the treatment table decides [{string.Join(", ", doubled)}], which the item's own native limb action already plays — remove the table row");

		var covered = new HashSet<string>(decided, StringComparer.Ordinal);
		covered.UnionWith(native);
		var missing = accepted.Where(id => !covered.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
		Assert.True(
			missing.Length == 0,
			$"the remote limb gesture accepts [{string.Join(", ", missing)}] but neither the treatment table nor the native injection path decides them — every accepted item needs a clip row, a recorded silence, or the native limb action that plays its own");

		// The native half has to EXIST for the split above to mean anything: the
		// operator's client runs the item's own limb action inside the capture
		// window, and the native injection call is diverted into the session instead
		// of draining the item and mutating the display copy.
		//
		// What "native-decided" means, stated rather than implied: the item's own
		// delegate owns the clip — NOT that a clip is necessarily played. Six of the
		// fifteen carriers (antiserum, bloodbag, bloodbaghuman, bloodcoagulant,
		// combatpen, streptokinase) play `Sound.Play("syringe", …)` in their own
		// delegate, inside this window, so the operator hears it and every peer
		// receives it. The other nine (morphine, syringe, opium, heroin, naloxone,
		// fentanyl, ceftriaxone, saline, ringersolution) start the syringe minigame
		// instead, whose only cue is its own 2D screen feedback — kept local by the
		// existing ruling (see TheTwoDimensionalCues_OpenNoCaptureScope) — so for
		// them this split means "the table must stay silent", which is what the
		// deleted rows recorded as UncarriedItems anyway. The 15-id list is a
		// hand-written census: a vanilla or mod carrier the game adds later is out of
		// this pin's reach, which the class doc already declares for the game tree.
		Assert.True(File.Exists(RepositoryPaths.File(RemoteInjectionSessionFile)), $"{RemoteInjectionSessionFile} is missing — nothing would run the item's own limb action");
		var injectionSession = WithoutComments(RepositoryPaths.ReadText(RemoteInjectionSessionFile));
		Assert.Contains("useLimbAction(", injectionSession, StringComparison.Ordinal);
		Assert.Contains("NativeLimbActionScope.Enter(", injectionSession, StringComparison.Ordinal);
		Assert.True(File.Exists(RepositoryPaths.File(RemoteInjectionPatchFile)), $"{RemoteInjectionPatchFile} is missing — the native injection call would drain the local item and mutate the displayed body copy");
		Assert.True(
			AnchorsOn(RepositoryPaths.ReadText(RemoteInjectionPatchFile), "WaterContainerItem", "Inject"),
			"the injection divert must bind WaterContainerItem.Inject — the one native call every injection delegate reaches");
		Assert.Contains(
			"TryDivertRemoteInjection",
			WithoutComments(RepositoryPaths.ReadText(RemoteInjectionPatchFile)),
			StringComparison.Ordinal);

		var medical = ClipsOf(RepositoryPaths.ReadText(PolicyFile), "IsMedicalClip");
		var clips = ClipValues(table, "TreatmentClips");
		Assert.True(clips.Count >= 11, $"only {clips.Count} clip row(s) could be read from the treatment table — the table was emptied, not the decision");

		foreach (var clip in clips)
		{
			Assert.True(
				medical.Contains(clip),
				$"the treatment table names `{clip}`, which the medical clip set does not classify — a peer would drop the report");
		}
	}

	[Fact]
	public void TheRemoteTreatmentPlaySite_PlaysOnlyAfterASuccessfulDispatch()
	{
		// One place decides: the handler dispatches first (every refusal path
		// returns before it), and only a dispatched operation plays the clip — so
		// a refused or ineligible gesture stays as silent as it is today, and the
		// bandage family (whose clip the native minigame itself plays) is not
		// double-played by the table.
		Assert.True(File.Exists(RepositoryPaths.File(RemoteMedicalHandlerFile)), $"{RemoteMedicalHandlerFile} is missing");

		var handler = RepositoryPaths.ReadText(RemoteMedicalHandlerFile);

		var handlerCode = WithoutComments(handler);

		Assert.Contains("RemoteMedicalTreatmentSoundCatalog", handlerCode, StringComparison.Ordinal);
		Assert.Contains("private void PlayTreatmentSound(", handlerCode, StringComparison.Ordinal);

		var guard = handler.IndexOf("var dispatch = TryDispatchLimbUse(", StringComparison.Ordinal);
		var play = handler.IndexOf("PlayTreatmentSound(dragItem, limbIndex);", StringComparison.Ordinal);
		Assert.True(guard >= 0, "TryHandleLimbUse must dispatch through TryDispatchLimbUse so the play site has one success gate");
		Assert.True(play > guard, "the treatment clip must be played AFTER the dispatch succeeded");

		// The gate has TWO success outcomes: the clip-replaying families, and the
		// injection family, which ran the item's own native limb action (whose clip
		// the capture window already reported) and must therefore NOT replay the
		// table's copy.
		Assert.Contains("if (dispatch == LimbUseDispatch.Dispatched)", handlerCode, StringComparison.Ordinal);
		Assert.Contains("LimbUseDispatch.DispatchedNative", handlerCode, StringComparison.Ordinal);

		var playBody = MethodBody(handler, "private void PlayTreatmentSound(");
		Assert.Contains("CallContext.Enter(CallContext.Origin.CharacterMedicalUse)", playBody, StringComparison.Ordinal);
		Assert.Contains("Sound.Play(", playBody, StringComparison.Ordinal);
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
			RepositoryPaths.ReadText(BodySoundPatchFile),
			RepositoryPaths.ReadText(BandageMinigamePatchFile),
			RepositoryPaths.ReadText(GoreAmputationPatchFile),
			RepositoryPaths.ReadText(GoreShrapnelPatchFile),
			RepositoryPaths.ReadText(DisplayCaptureFile));

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

	[Theory]
	[InlineData("private static readonly IReadOnlyDictionary<string, string> TreatmentClips =\n\tnew Dictionary<string, string>(StringComparer.Ordinal)\n\t{\n\t\t[\"splint\"] = \"splint\",\n\t\t[\"chestdrain\"] = \"syringe\",\n\t};", 2, "chestdrain", 2)]
	[InlineData("// a doc mention of TreatmentClips = [\"ghost\"] over\nprivate static readonly IReadOnlyDictionary<string, string> TreatmentClips =\n\tnew Dictionary<string, string>(StringComparer.Ordinal)\n\t{\n\t\t[\"splint\"] = \"splint\",\n\t};", 1, "splint", 1)]
	public void TheTableReader_ReadsTheClipRows(string source, int expectedIds, string expectedId, int expectedClips)
	{
		var ids = KeyIds(source, "TreatmentClips");

		Assert.Equal(expectedIds, ids.Count);
		Assert.Contains(expectedId, ids);
		Assert.Equal(expectedClips, ClipValues(source, "TreatmentClips").Count);
	}

	[Theory]
	[InlineData("// using var scope = CallContext.Enter(CallContext.Origin.CharacterMedicalUse);", "CallContext.Enter(", false)]
	[InlineData("using var scope = CallContext.Enter(CallContext.Origin.CharacterMedicalUse);", "CallContext.Enter(", true)]
	public void TheCommentStripper_DoesNotLetACommentedLineSatisfyAPin(string source, string marker, bool expected) =>
		Assert.Equal(expected, WithoutComments(source).Contains(marker, StringComparison.Ordinal));

	[Theory]
	[InlineData("private static readonly string[] UncarriedItems =\n\t[\n\t\t\"syringe\",\n\t\t\"aed\",\n\t];", 2, "aed")]
	[InlineData("// a doc mention of UncarriedItems must never hold \"ghost\"\nprivate static readonly string[] UncarriedItems =\n\t[\n\t\t\"aed\",\n\t];", 1, "aed")]
	[InlineData("// only a doc mention of UncarriedItems with \"ghost\" inside", 0, "")]
	public void TheTableReader_ReadsTheUncarriedRows(string source, int expectedCount, string expectedId)
	{
		var ids = LiteralIds(source, "UncarriedItems");

		Assert.Equal(expectedCount, ids.Count);
		if (expectedCount > 0)
		{
			Assert.Contains(expectedId, ids);
		}
	}

	[Theory]
	[InlineData("void PlayTreatmentSound(Item item, int limbIndex)\n{\n\tusing var scope = CallContext.Enter(CallContext.Origin.CharacterMedicalUse);\n}", "PlayTreatmentSound", true)]
	[InlineData("// a doc mention of PlayTreatmentSound( … )", "PlayTreatmentSound", false)]
	public void TheMethodBodyReader_ReadsTheMethodRange(string source, string method, bool expected)
	{
		var body = MethodBody(source, method);

		Assert.Equal(expected, body.Contains("CallContext.Enter(", StringComparison.Ordinal));
	}

	/// <summary>The source with line comments removed — every reader below works on this, so a doc mention can never satisfy (or mislead) a pin.</summary>
	private static string WithoutComments(string source) => Regex.Replace(source, @"//[^\n]*", "");

	/// <summary>A named declaration's own text, up to its own <c>};</c> / <c>];</c> terminator, or "" when the declaration is gone.</summary>
	private static string NamedBlock(string source, string name)
	{
		var clean = WithoutComments(source);
		var start = clean.IndexOf(name, StringComparison.Ordinal);
		if (start < 0)
		{
			return "";
		}

		var objectEnd = clean.IndexOf("};", start, StringComparison.Ordinal);
		var arrayEnd = clean.IndexOf("];", start, StringComparison.Ordinal);
		var end = objectEnd < 0 ? arrayEnd : arrayEnd < 0 ? objectEnd : Math.Min(objectEnd, arrayEnd);
		return end < 0 ? "" : clean[start..end];
	}

	/// <summary>The <c>["id"] =</c> keys of a named dictionary — the accepted-item catalogs and the treatment table's clip rows.</summary>
	private static IReadOnlyList<string> KeyIds(string source, string name) =>
	[
		.. Regex.Matches(NamedBlock(source, name), @"\[""(?<id>[a-z0-9_]+)""\]\s*=")
			.Select(match => match.Groups["id"].Value)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(id => id, StringComparer.Ordinal),
	];

	/// <summary>The string literals of a named list (or of the whole file) — an explicit silent-row set, or a catalog written as bare lists.</summary>
	private static IReadOnlyList<string> LiteralIds(string source, string name) =>
	[
		.. Regex.Matches(NamedBlock(source, name), @"""(?<id>[a-z0-9_]+)""")
			.Select(match => match.Groups["id"].Value)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(id => id, StringComparer.Ordinal),
	];

	private static IReadOnlyList<string> LiteralIds(string source) =>
	[
		.. Regex.Matches(WithoutComments(source), @"""(?<id>[a-z0-9_]+)""")
			.Select(match => match.Groups["id"].Value)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(id => id, StringComparer.Ordinal),
	];

	/// <summary>The right-hand clip of every treatment row — one entry per ROW (two items may share one clip, and that is the decision being pinned).</summary>
	private static IReadOnlyList<string> ClipValues(string source, string name) =>
	[
		.. Regex.Matches(NamedBlock(source, name), @"\]\s*=\s*""(?<clip>[a-z0-9_]+)""")
			.Select(match => match.Groups["clip"].Value)
			.OrderBy(clip => clip, StringComparer.Ordinal),
	];

	/// <summary>A method's own body — its signature up to the next closing brace at the class-body indentation.</summary>
	private static string MethodBody(string source, string method)
	{
		var clean = WithoutComments(source);
		var start = clean.IndexOf(method, StringComparison.Ordinal);
		if (start < 0)
		{
			return "";
		}

		var end = clean.IndexOf("\n\t}", start, StringComparison.Ordinal);
		return end < 0 ? clean[start..] : clean[start..end];
	}

	/// <summary>Every item id the remote limb gesture accepts: the catalogs the eligibility check still consults, plus the handler's own literal surface and the pinned vanilla injectable carriers (the injection family's admission is game data now).</summary>
	private static IReadOnlyList<string> AcceptedItemIds()
	{
		var ids = new SortedSet<string>(StringComparer.Ordinal);

		foreach (var file in AcceptedItemCatalogFiles)
		{
			ids.UnionWith(KeyIds(RepositoryPaths.ReadText(file), "Registry"));
		}

		ids.UnionWith(LiteralIds(RepositoryPaths.ReadText(OtherMedicalCatalogFile)));
		ids.UnionWith(VanillaInjectableCarriers);
		ids.UnionWith(KeyIds(RepositoryPaths.ReadText(TopicalCatalogFile), "TopicalAmounts"));
		ids.Add("tweezers"); // the handler's own literal surface (dragItem.id == "tweezers")

		return [.. ids];
	}

	/// <summary>The anchor form the pins read: the declaring type AND the method, never a bare method name (a same-shaped anchor on another type must not pass) — and never a mention inside a line comment.</summary>
	private static bool AnchorsOn(string source, string declaringType, string method) =>
		Regex.IsMatch(
			WithoutComments(source),
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
