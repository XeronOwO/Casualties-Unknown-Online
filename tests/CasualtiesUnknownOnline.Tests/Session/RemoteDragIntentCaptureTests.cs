using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the release-window state machine that turns the calls the
/// game's own drag branch made into native intents. The window is pure (no
/// scene), so every coalescing rule, every refusal and the unclassified-gesture
/// case are locked here: the patch layer only resolves ids and skips the
/// original call when the window took it.
/// </summary>
public class RemoteDragIntentCaptureTests
{
	private const ulong Owner = 7001;
	private const ulong Local = 7002;
	private const ulong Item = 42;
	private const ulong OtherItem = 43;
	private const ulong Container = 77;
	private const ulong OtherContainer = 78;

	private static RemoteDragIntentCapture Open(bool ownerRing = true, RemoteDragNoOp noOp = RemoteDragNoOp.None)
	{
		var window = new RemoteDragIntentCapture();
		window.Open(Item, Owner, Local, ownerRing, noOp);
		return window;
	}

	[Fact]
	public void SlotRelease_OnTheOwnersRing_IsOnePickUpIntentOnTheOwner()
	{
		var window = Open();
		window.CapturePickUp(Item, 3);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.PickUpToSlot, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Equal(3, intent.TargetSlotIndex);
		Assert.Equal(0UL, intent.TargetBodySteamId);
		Assert.Empty(outcome.Refusals);
	}

	[Fact]
	public void SlotRelease_IntoSlotZero_KeepsTheHandSlotOperand()
	{
		var window = Open();
		window.CapturePickUp(Item, 0);

		var outcome = window.Close();

		Assert.Equal(0, Assert.Single(outcome.Intents).TargetSlotIndex);
	}

	[Fact]
	public void SlotRelease_AfterTheViewClosed_IsATransferToTheRequester()
	{
		var window = Open(ownerRing: false);
		window.CapturePickUp(Item, 1);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.TransferToBody, intent.Kind);
		Assert.Equal(1, intent.TargetSlotIndex);
		Assert.Equal(Local, intent.TargetBodySteamId);
	}

	[Fact]
	public void R9sOwnDrops_AreAbsorbedByTheSlotPickUp()
	{
		var window = Open();
		window.CaptureDropItem(Item);
		window.CapturePickUp(Item, 2);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.PickUpToSlot, intent.Kind);
	}

	[Fact]
	public void HeldDropWithoutASlotPickUp_IsAWorldDrop()
	{
		var window = Open();
		window.CaptureDropItem(Item);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.DropItem, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
	}

	[Fact]
	public void WornDrop_IsOneDropWearableIntent()
	{
		var window = Open();
		window.CaptureDropWearable(Item);

		var outcome = window.Close();

		Assert.Equal(RemoteInventoryIntentKind.DropWearable, Assert.Single(outcome.Intents).Kind);
	}

	[Fact]
	public void Swap_IsOneSwapIntentAndTheOwnerResolvesTheItemsOwnSlot()
	{
		var window = Open();
		window.CaptureSwapSlots(Item, 2);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.SwapSlots, intent.Kind);
		Assert.Equal(2, intent.TargetSlotIndex);
	}

	[Fact]
	public void ContainerUnloadThenLoad_IsOneMoveIntoThatContainer()
	{
		var window = Open();
		window.CaptureContainerUnload(Item, Container);
		window.CaptureContainerLoad(Item, Container);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.MoveIntoContainer, intent.Kind);
		Assert.Equal(Container, intent.TargetContainerInstanceId);
	}

	[Fact]
	public void ContainerUnloadWithoutALoad_IsATakeIntoTheWorld()
	{
		var window = Open();
		window.CaptureContainerUnload(Item, Container);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.TakeOutOfContainer, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Empty(outcome.Refusals);
	}

	[Fact]
	public void TakeOutOfOneContainerThenIntoAnother_IsTwoIntentsInNativeOrder()
	{
		// W1 + W4 in one TryPerformWorldActions (PlayerCamera.cs:1686): the item
		// leaves its own container and enters the container under the pointer. The
		// take-out must survive the second pair, in the order the native code ran.
		var window = Open();
		window.CaptureContainerUnload(Item, Container);
		window.CaptureContainerUnload(Item, OtherContainer);
		window.CaptureContainerLoad(Item, OtherContainer);

		var outcome = window.Close();

		Assert.Equal(2, outcome.Intents.Count);
		Assert.Equal(RemoteInventoryIntentKind.TakeOutOfContainer, outcome.Intents[0].Kind);
		Assert.Equal(Container, outcome.Intents[0].TargetContainerInstanceId);
		Assert.Equal(RemoteInventoryIntentKind.MoveIntoContainer, outcome.Intents[1].Kind);
		Assert.Equal(OtherContainer, outcome.Intents[1].TargetContainerInstanceId);
	}

	[Fact]
	public void MoveIntoAContainerThenTakeOutOfAnother_KeepsBothIntents()
	{
		var window = Open();
		window.CaptureContainerUnload(Item, Container);
		window.CaptureContainerLoad(Item, Container);
		window.CaptureContainerUnload(Item, OtherContainer);

		var outcome = window.Close();

		Assert.Equal(2, outcome.Intents.Count);
		Assert.Equal(RemoteInventoryIntentKind.MoveIntoContainer, outcome.Intents[0].Kind);
		Assert.Equal(Container, outcome.Intents[0].TargetContainerInstanceId);
		Assert.Equal(RemoteInventoryIntentKind.TakeOutOfContainer, outcome.Intents[1].Kind);
		Assert.Equal(OtherContainer, outcome.Intents[1].TargetContainerInstanceId);
	}

	[Fact]
	public void ContainerChildBatch_IsOneMoveContainerChildrenIntentForTheWholeGesture()
	{
		// R5 (PlayerCamera.cs:1585): with expanddesc held, the native loop unloads
		// every direct child out of the DRAGGED item's own container and loads it
		// into the hit container. One intent per child would make the owner run the
		// whole loop once per child — on a container the first run already emptied —
		// so the gesture is ONE intent and the owner enumerates the children itself.
		var window = Open();
		window.CaptureContainerUnload(99, Item);
		window.CaptureContainerLoad(99, Container);
		window.CaptureContainerUnload(100, Item);
		window.CaptureContainerLoad(100, Container);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.MoveContainerChildren, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Equal(Container, intent.TargetContainerInstanceId);
		Assert.Empty(outcome.Refusals);
	}

	[Fact]
	public void ContainerChildBatchWithoutItsLoad_IsRefused()
	{
		// The native loop never leaves a child unloaded: it calls the pair for every
		// child its guard admitted. A bare unload of a child is not a gesture the
		// vocabulary can name, so it stays observable instead of becoming an intent.
		var window = Open();
		window.CaptureContainerUnload(99, Item);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void AContainerCallAboutAChildTheBatchNeverUnloaded_IsRefused()
	{
		var window = Open();
		window.CaptureContainerLoad(99, Container);
		window.CaptureContainerUnload(99, OtherContainer);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Equal(2, outcome.Refusals.Count);
	}

	[Fact]
	public void ContainerChildBatchReachingTwoTargets_IsRefusedWhole()
	{
		// The native loop resolves its target container once per invocation, so this
		// shape is defensive: a batch sent against the first target would be a gesture
		// the native code never makes.
		var window = Open();
		window.CaptureContainerUnload(99, Item);
		window.CaptureContainerLoad(99, Container);
		window.CaptureContainerUnload(100, Item);
		window.CaptureContainerLoad(100, OtherContainer);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void ThePickupGate_IsAnsweredForTheDraggedProxyInBothBracketKinds()
	{
		// The native release opens with `DoPickupCheck(dragItem, false)`
		// (PlayerCamera.cs:1467) and the drain tick gates on
		// `DoPickupCheck(dragItem, true)` (:1729). Both compare the LOCAL body against
		// an item that stands where the remote player stands, so both must be answered
		// for the dragged proxy — the frame bracket exactly as the release bracket.
		var closed = new RemoteDragIntentCapture();
		Assert.False(closed.AnswersPickupCheckFor(Item));

		var release = Open();
		Assert.True(release.AnswersPickupCheckFor(Item));
		Assert.False(release.AnswersPickupCheckFor(OtherItem));

		var frame = new RemoteDragIntentCapture();
		frame.OpenWhileDragging(Item, Owner);
		Assert.True(frame.AnswersPickupCheckFor(Item));
		Assert.False(frame.AnswersPickupCheckFor(OtherItem));
		Assert.False(frame.AnswersPickupCheckFor(0));
	}

	[Fact]
	public void OnlyAWhileDraggingFrame_CapturesTheContinuousCalls()
	{
		// A proxy no frame bracket took — no authoritative identity, or another item's
		// bracket — must not have its native drain run: the seam asks this and refuses
		// the call instead.
		var frame = new RemoteDragIntentCapture();
		frame.OpenWhileDragging(Item, Owner);
		Assert.True(frame.CapturesContinuousCallsFor(Item));
		Assert.False(frame.CapturesContinuousCallsFor(OtherItem));

		var release = Open();
		Assert.False(release.CapturesContinuousCallsFor(Item));

		var closed = new RemoteDragIntentCapture();
		Assert.False(closed.CapturesContinuousCallsFor(Item));
	}

	[Fact]
	public void TheDrainTick_IsOneIntentCarryingThatFramesAmount()
	{
		// The while-dragging bracket spans one frame, and the native tick runs once
		// per frame (PlayerCamera.cs:1732): every frame is its own intent, so the
		// owner's drain follows the native timing instead of a merged approximation.
		var window = new RemoteDragIntentCapture();
		window.OpenWhileDragging(Item, Owner);
		window.CaptureDrain(Item, 0.25f);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.Drain, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Equal(0.25f, intent.Amount);
		Assert.Empty(outcome.Refusals);
	}

	[Fact]
	public void AWhileDraggingFrameWithoutADrain_ProducesNothingAndNoRefusal()
	{
		// Most frames of a drag are not drain frames; the release path's
		// unclassified-gesture rule must not fire sixty times a second here.
		var window = new RemoteDragIntentCapture();
		window.OpenWhileDragging(Item, Owner);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Empty(outcome.Refusals);
		Assert.False(outcome.IsUnclassified);
	}

	[Fact]
	public void ADrainOutsideAWhileDraggingFrame_IsRefused()
	{
		var window = Open();
		window.CaptureDrain(Item, 0.25f);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void ADrainWithAnUnusableAmount_IsRefused()
	{
		var window = new RemoteDragIntentCapture();
		window.OpenWhileDragging(Item, Owner);
		window.CaptureDrain(Item, float.NaN);
		window.CaptureDrain(Item, -1f);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Equal(2, outcome.Refusals.Count);
	}

	[Fact]
	public void AReleaseOnlyCallInsideAWhileDraggingFrame_IsRefusedAndItsIntentDropped()
	{
		// Defensive: the while-dragging body makes no release call today, and a
		// bracket that swallowed one would send a gesture the frame never produced.
		var window = new RemoteDragIntentCapture();
		window.OpenWhileDragging(Item, Owner);
		window.CapturePickUp(Item, 1);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void AWhileDraggingFrameKeepsItsDrainWhenAReleaseCallSlipsIn()
	{
		var window = new RemoteDragIntentCapture();
		window.OpenWhileDragging(Item, Owner);
		window.CaptureDrain(Item, 0.5f);
		window.CaptureDropItem(Item);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.Drain, intent.Kind);
		Assert.Equal(0.5f, intent.Amount);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void LimbApplication_IsOneApplyToLimbIntent()
	{
		var window = Open();
		window.CaptureApplyToLimb(Item, 2);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.ApplyToLimb, intent.Kind);
		Assert.Equal(2, intent.TargetLimbIndex);
	}

	[Fact]
	public void LimbApplication_OnLimbZero_KeepsTheLimbOperand()
	{
		var window = Open();
		window.CaptureApplyToLimb(Item, 0);

		var outcome = window.Close();

		Assert.Equal(0, Assert.Single(outcome.Intents).TargetLimbIndex);
	}

	[Fact]
	public void CallsAboutAnotherItem_AreRefusedAndNeverBecomeAnIntent()
	{
		var window = Open();
		window.CaptureDropItem(99);
		window.CaptureDropWearable(99);
		window.CaptureSwapSlots(99, 1);
		window.CapturePickUp(99, 1);
		window.CaptureApplyToLimb(99, 0);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Equal(5, outcome.Refusals.Count);
	}

	[Fact]
	public void ReleaseThatProducesNothing_IsReportedAsUnclassified()
	{
		var window = Open();

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Empty(outcome.Refusals);
		Assert.True(outcome.ProducedNothing);
		Assert.True(outcome.IsUnclassified);
	}

	[Fact]
	public void ReleaseBackOntoItsOwnSlot_IsAClassifiedNoOp()
	{
		var window = Open(noOp: RemoteDragNoOp.ReturnedToOwnSlot);

		var outcome = window.Close();

		Assert.True(outcome.ProducedNothing);
		Assert.False(outcome.IsUnclassified);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void TheClassifiedNativeNoOps_AreNeverReportedAsUnknownGestures(int noOpValue)
	{
		// The three classified native no-ops: R1 (its own slot), R7 (a wearable
		// that cannot be held), R14 (local craft UI).
		var noOp = (RemoteDragNoOp)noOpValue;
		var window = Open(noOp: noOp);

		var outcome = window.Close();

		Assert.Equal(noOp, outcome.NoOp);
		Assert.False(outcome.IsUnclassified);
	}

	[Fact]
	public void ShouldAnswerFromDisplayBody_FollowsTheBracketAndTheRing()
	{
		var closed = new RemoteDragIntentCapture();
		Assert.False(closed.ShouldAnswerFromDisplayBody(isLocalBody: true));

		var ownerRing = Open();
		Assert.True(ownerRing.ShouldAnswerFromDisplayBody(isLocalBody: true));
		Assert.False(ownerRing.ShouldAnswerFromDisplayBody(isLocalBody: false));

		var localRing = Open(ownerRing: false);
		Assert.False(localRing.ShouldAnswerFromDisplayBody(isLocalBody: true));

		_ = ownerRing.Close();
		Assert.False(ownerRing.ShouldAnswerFromDisplayBody(isLocalBody: true));
	}

	[Fact]
	public void Close_ResetsTheWindowForTheNextRelease()
	{
		var window = Open();
		window.CaptureDropItem(Item);
		_ = window.Close();

		Assert.False(window.IsOpen);
		Assert.Equal(0UL, window.DraggedItemId);

		window.Open(Item, Owner, Local, destinationIsOwnerBody: false, noOp: RemoteDragNoOp.None);
		window.CapturePickUp(Item, 0);

		var outcome = window.Close();
		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.TransferToBody, intent.Kind);
		Assert.DoesNotContain(outcome.Intents, i => i.Kind == RemoteInventoryIntentKind.DropItem);
	}
}
