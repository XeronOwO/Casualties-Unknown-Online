using System;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The give gesture's context and release-order pins. The user's ruling of 2026-10-23 splits the drag by context:
/// inside the player's own UI nothing crosses to another player; in the world the detection range chooses whose
/// backpack is displayed; and a displayed backpack gives on a slot and uses/wears on its centre. The 2026-10-22
/// ruling still orders the displayed backpack before anything else.
///
/// <para>
/// The refusal half stays: the close rule keeps the drag-opened backpack only for a give that actually went out
/// and for a centre use/wear (the vanilla ring stays open), so the flag must come from the call's answer and the
/// finalizer must read it.
/// </para>
/// </summary>
public class GiveGestureGateTests
{
	private const string ReleasePatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/PlayerCameraDragUsePatch.cs";

	private const string OpenPatchFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/PlayerCameraDragGiveOpenPatch.cs";

	private const string CueTrackerFile = "src/CasualtiesUnknownOnline.GameAdapter/DragUseCueTracker.cs";

	private const string ApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs";

	[Fact]
	public void TheReceiversExactPlacementBranch_ReportsInsteadOfSubstituting()
	{
		// The give's answer-first custody: the transfer carries the ANSWERED destination, and the receiving
		// client must not substitute another slot or container when its live body cannot take it — it reports
		// that it placed nothing (the not-applied outcome) and the host returns the row. The branch is adapter
		// code (a Unity body), so it is pinned by its shape here; the kernel projection and the host half's
		// behaviour are `PlacementAnswerTests`.
		var source = RepositoryPaths.ReadText(ApplyFile);

		Assert.Contains("msg.ExactDestination", source, StringComparison.Ordinal);
		Assert.Contains("CanTakeExactDestination(", source, StringComparison.Ordinal);
		Assert.Contains("LocalPlacement.NotPlaced(", source, StringComparison.Ordinal);
		Assert.Contains("SendPlacementOutcome(", source, StringComparison.Ordinal);

		// The reported place must be the PLACEMENT's own answer, never the transfer's request: a body that
		// placed the item somewhere else has to say so, or the host's comparison would always agree.
		Assert.Contains("outcome = placed", source, StringComparison.Ordinal);
		Assert.Contains("PlaceCarriedItem(body, item, msg.TargetParentItemId, msg.ExactDestination)", source, StringComparison.Ordinal);

		// The one destroy the give's outcome makes safe: an unplaceable handed-over item is discarded WITHOUT
		// claiming its id ended, because the host is about to return that id to the giver. The marker is only
		// worth attaching if the deferred destroy hook reads it, and only the give's arm may attach it (a
		// returned item that finds no place has nothing behind it and must keep its end report).
		var patches = RepositoryPaths.ReadText("src/CasualtiesUnknownOnline.GameAdapter/Patches/ItemPatches.cs");
		Assert.Contains("GetComponent<UnplacedGiveItemOrigin>() != null", patches, StringComparison.Ordinal);
		Assert.Contains("suppressUnplacedEnd", source, StringComparison.Ordinal);

		// The port must also be INJECTED by the one production construction, and the flag must be carried by
		// the kernel projection: a branch with a null port or a discarded flag never runs (the review's
		// blocker), and neither omission is visible from the apply file alone.
		var adapter = RepositoryPaths.ReadText("src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs");
		Assert.Contains("new PlayerInteractionApply(_domains, placementResolver)", adapter, StringComparison.Ordinal);

		var codec = RepositoryPaths.ReadText("src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerInteractionKernelCodec.cs");
		Assert.Contains("ExactDestination = e.ExactDestination", codec, StringComparison.Ordinal);
		Assert.Contains("Returned = e.Returned", codec, StringComparison.Ordinal);
	}

	[Fact]
	public void AReleaseInsideThePlayersOwnUi_IsNeverCrossPlayer()
	{
		// The own-UI gate must run BEFORE any cross-player resolution: while the player's own ring or a panel is
		// open, a drag means the local body, whatever the cursor's world point is (user ruling 2026-10-23).
		var release = RepositoryPaths.ReadText(ReleasePatchFile);
		Assert.Contains("GiveGestureModePolicy.Read(", release, StringComparison.Ordinal);
		Assert.Contains("GiveGestureMode.OwnUi", release, StringComparison.Ordinal);
		Assert.True(
			OrderHolds(release, "GiveGestureModePolicy.Read", "DragGiveTargetResolver.Resolve"),
			"the own-UI gate must run before the displayed backpack's give resolution");

		var open = RepositoryPaths.ReadText(OpenPatchFile);
		Assert.Contains("GiveGestureModePolicy.Read(", open, StringComparison.Ordinal);
		Assert.Contains("GiveGestureMode.OwnUi", open, StringComparison.Ordinal);
		Assert.True(
			OrderHolds(open, "GiveGestureMode.OwnUi", "AutoOpenRemoteBackpackForDrag"),
			"the range must not be read while the player's own UI is open");

		// The drag's selection must still END when the drag stops in the own UI: that path never reaches the
		// item branch, and without this call the next gesture keeps the previous target's opened-for state.
		Assert.Contains("AutoOpenRemoteBackpackForDrag(null)", open, StringComparison.Ordinal);
	}

	/// <summary>
	/// The displayed backpack's centre is the use/wear target (user ruling 2026-10-23): the detection range no
	/// longer decides a use. The release consumes the centre even when the seam refuses, so the native centre can
	/// never use or wear the item on the GIVER, and a centre release re-asserts the native ring (the native body
	/// clears it because the centre graphic is hidden in the remote view).
	/// </summary>
	[Fact]
	public void AReleaseOnTheDisplayedCentre_IsTheUse_AndKeepsTheBackpackOpen()
	{
		var source = RepositoryPaths.ReadText(ReleasePatchFile);

		Assert.Contains("DragUseCenter.IsUnderPointer(__instance)", source, StringComparison.Ordinal);
		Assert.Contains("TryHandleDraggedItemUseOnRemote(dragItem, __instance.body);", source, StringComparison.Ordinal);
		Assert.Contains("__state.Center = true;", source, StringComparison.Ordinal);
		Assert.Contains("if (__state.Center)", source, StringComparison.Ordinal);
		Assert.Contains("__instance.radialOpen = true;", source, StringComparison.Ordinal);
	}

	/// <summary>
	/// The centre hit test must NOT depend on the tagged Graphic: CUO's remote view hides the `RadialCenter` circle
	/// (`AnchorRadialToFocus` disables it), so a UI raycast can never reach it while a displayed backpack is open.
	/// The native guard's GEOMETRY is the judgment (ring open, pointer inside the centre circle); the batch
	/// `20261023-e` review found the tag-gated version could never fire.
	/// </summary>
	[Fact]
	public void TheCentreHitTest_DoesNotNeedTheHiddenTaggedGraphic()
	{
		var source = RepositoryPaths.ReadText("src/CasualtiesUnknownOnline.GameAdapter/DragUseCenter.cs");

		Assert.Contains("inventoryUseLeniency", source, StringComparison.Ordinal);
		Assert.Contains("radialCircle.transform.position", source, StringComparison.Ordinal);
		Assert.Contains("rectTransform.sizeDelta.x * 0.5f", source, StringComparison.Ordinal);
		Assert.DoesNotContain("CompareTag(\"RadialCenter\")", source, StringComparison.Ordinal);
	}

	/// <summary>
	/// The centre use targets the DISPLAYED player (never a range target), the view-only pack refuses it, and the
	/// live mode read names the four own panels: these are the three facts the pure matrix test cannot see.
	/// </summary>
	[Fact]
	public void TheCentreUse_IsTheDisplayedPlayer_AndTheLiveModeReadsTheOwnPanels()
	{
		var cross = RepositoryPaths.ReadText("src/CasualtiesUnknownOnline.GameAdapter/CrossPlayerDragUse.cs");
		Assert.Contains("TryUseOnDisplayed(", cross, StringComparison.Ordinal);
		Assert.Contains("RemoteBackpackView.FocusedSteamId", cross, StringComparison.Ordinal);
		Assert.Contains("RemoteBackpackView.IsReadOnly", cross, StringComparison.Ordinal);
		Assert.Contains("!IsTargetInRange", cross, StringComparison.Ordinal);

		var mode = RepositoryPaths.ReadText("src/CasualtiesUnknownOnline.GameAdapter/GiveGestureModePolicy.cs");
		Assert.Contains("woundView.activeSelf", mode, StringComparison.Ordinal);
		Assert.Contains("containerMenu.activeSelf", mode, StringComparison.Ordinal);
		Assert.Contains("craftingPanel.activeSelf", mode, StringComparison.Ordinal);
		Assert.Contains("tradeMenu.activeSelf", mode, StringComparison.Ordinal);
	}

	[Fact]
	public void TheCueAndTheRelease_ReadTheSameDisplayedCentre()
	{
		Assert.Contains("DragUseCenter.IsUnderPointer(", RepositoryPaths.ReadText(ReleasePatchFile), StringComparison.Ordinal);
		Assert.Contains("DragUseCenter.IsUnderPointer(", RepositoryPaths.ReadText(CueTrackerFile), StringComparison.Ordinal);
	}

	[Fact]
	public void TheGiveFlag_IsTheSeamsAnswer_AndTheCloseRuleReadsIt()
	{
		var source = RepositoryPaths.ReadText(ReleasePatchFile);

		Assert.Contains("__state.Give = giveBridge.ReportLocalReleaseOntoProxy(", source, StringComparison.Ordinal);
		Assert.Contains("__state.AutoOpened && !__state.Give", source, StringComparison.Ordinal);
	}

	[Fact]
	public void TheOrderMatcher_SeesBothShapes()
	{
		Assert.True(OrderHolds("first(); second();", "first", "second"));
		Assert.False(OrderHolds("second(); first();", "first", "second"));
		Assert.False(OrderHolds("only one();", "first", "second"));
	}

	private static bool OrderHolds(string source, string before, string after)
	{
		var beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
		var afterIndex = source.IndexOf(after, StringComparison.Ordinal);
		return beforeIndex >= 0 && afterIndex >= 0 && beforeIndex < afterIndex;
	}
}
