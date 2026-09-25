using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the item-interaction half of the drag-window state machine —
/// R10's radial-centre use/wear, R6's combine, R2/R3's battery calls, the
/// while-dragging <c>favourited</c> store and R12's trader hand-in. The window is
/// pure (no scene), so the operand pairing, the cross-item refusals and the way a
/// field store sits beside the frame's drain tick are locked here; the patch layer
/// only resolves ids, compares the field across the frame and skips the original
/// call when the window took it.
/// </summary>
public class RemoteItemInteractionCaptureTests
{
	private const ulong Owner = 7101;
	private const ulong OtherOwner = 7103;
	private const ulong Item = 43;
	private const ulong HitItem = 44;
	private const ulong OtherItem = 45;

	private static RemoteDragIntentCapture Open()
	{
		var window = new RemoteDragIntentCapture();
		window.Open(Item, Owner, OtherOwner, destinationIsOwnerBody: true, RemoteDragNoOp.None);
		return window;
	}

	private static RemoteDragIntentCapture OpenFrame()
	{
		var window = new RemoteDragIntentCapture();
		window.OpenWhileDragging(Item, Owner);
		return window;
	}

	[Fact]
	public void RadialUse_IsOneUseIntentForTheDraggedProxy()
	{
		var window = Open();
		window.CaptureUseItem(Item);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.UseItem, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Empty(outcome.Refusals);
	}

	[Fact]
	public void RadialWearAndUse_BothArriveInTheNativeOrder()
	{
		// PlayerCamera.cs:1640-1647 tests wearable and usable as two independent ifs,
		// so one release really can produce both calls. The pair is the native order.
		var window = Open();
		window.CaptureWearItem(Item);
		window.CaptureUseItem(Item);

		var outcome = window.Close();

		Assert.Equal(2, outcome.Intents.Count);
		Assert.Equal(RemoteInventoryIntentKind.WearItem, outcome.Intents[0].Kind);
		Assert.Equal(RemoteInventoryIntentKind.UseItem, outcome.Intents[1].Kind);
	}

	[Fact]
	public void RadialUse_OfAnItemThatIsNotTheDraggedProxy_IsRefused()
	{
		var window = Open();
		window.CaptureUseItem(HitItem);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void Combine_NamesBothOperandsWithTheHitItemAsTheSecond()
	{
		var window = Open();
		window.CaptureCombine(Item, HitItem, Owner);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.CombineItems, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Equal(HitItem, intent.TargetItemInstanceId);
	}

	[Fact]
	public void Combine_AgainstAnItemOfAnotherOwner_IsRefused()
	{
		var window = Open();
		window.CaptureCombine(Item, HitItem, OtherOwner);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void Combine_AgainstAProxyWithoutAnIdentity_IsRefused()
	{
		var window = Open();
		window.CaptureCombine(Item, 0, Owner);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void BatteryLoad_NamesTheBatteryAndTheReceivingItem()
	{
		var window = Open();
		window.CaptureBatteryLoad(Item, HitItem, Owner);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.LoadBattery, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.Equal(HitItem, intent.TargetItemInstanceId);
	}

	[Fact]
	public void BatteryUnload_NamesTheHitItemTheCallReads()
	{
		// The native call is hitItem.battery.UnloadBattery(false): the dragged item
		// only told the dispatch the direction, so the hit item is the one operand.
		var window = Open();
		window.CaptureBatteryUnload(HitItem, Owner);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.UnloadBattery, intent.Kind);
		Assert.Equal(HitItem, intent.ItemInstanceId);
		Assert.Equal(0UL, intent.TargetItemInstanceId);
	}

	[Fact]
	public void Favourite_IsOneToggleIntentForTheHoveredItem()
	{
		var window = OpenFrame();
		window.CaptureFavourite(HitItem, Owner);

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.ToggleFavourite, intent.Kind);
		Assert.Equal(HitItem, intent.ItemInstanceId);
		Assert.Equal(RemoteDragWindowKind.WhileDragging, outcome.Kind);
	}

	[Fact]
	public void AFramesDrainAndItsFavouriteStore_BothSurviveTheClose()
	{
		// The release-only rule must drop release calls, never the frame's own kinds:
		// one frame can carry the continuous tick AND the field store.
		var window = OpenFrame();
		window.CaptureDrain(Item, 0.25f);
		window.CaptureFavourite(HitItem, Owner);

		var outcome = window.Close();

		Assert.Equal(2, outcome.Intents.Count);
		Assert.Equal(RemoteInventoryIntentKind.Drain, outcome.Intents[0].Kind);
		Assert.Equal(RemoteInventoryIntentKind.ToggleFavourite, outcome.Intents[1].Kind);
		Assert.Empty(outcome.Refusals);
	}

	[Fact]
	public void Favourite_OnAnotherOwnersItem_IsRefused()
	{
		var window = OpenFrame();
		window.CaptureFavourite(HitItem, OtherOwner);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void Favourite_OutsideAWhileDraggingFrame_IsRefused()
	{
		var window = Open();
		window.CaptureFavourite(HitItem, Owner);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}

	[Fact]
	public void ARadialCentreReleaseThatRanNoCall_IsAClassifiedNoOp()
	{
		// R10 consumes the release without running anything when the dragged item is
		// neither wearable nor usable (PlayerCamera.cs:1638-1648). The probe records the
		// native method's own answer, so the window must not report that release as a
		// gesture CUO has never seen.
		var window = Open();
		window.NoteClassifiedNoOp(RemoteDragNoOp.RadialCentreWithoutAnAction);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Empty(outcome.Refusals);
		Assert.False(outcome.IsUnclassified);
	}

	[Fact]
	public void AWhileDraggingFrame_DoesNotTakeAReleaseNoOp()
	{
		// The probe reports a RELEASE outcome (PlayerCamera.TryPerformRadialAction runs at
		// the end of the release); a frame must not claim it, or a frame that produced
		// nothing would carry a reason that belongs to a release.
		var window = OpenFrame();
		window.NoteClassifiedNoOp(RemoteDragNoOp.RadialCentreWithoutAnAction);

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Equal(RemoteDragNoOp.None, outcome.NoOp);
	}

	[Fact]
	public void TraderHandIn_CarriesTheDraggedItemAndTheTraderPosition()
	{
		var window = Open();
		window.CaptureGiveToTrader(Item, new NetVector2Msg(12.5f, -3f));

		var outcome = window.Close();

		var intent = Assert.Single(outcome.Intents);
		Assert.Equal(RemoteInventoryIntentKind.GiveToTrader, intent.Kind);
		Assert.Equal(Item, intent.ItemInstanceId);
		Assert.NotNull(intent.TargetTraderPosition);
		Assert.Equal(12.5f, intent.TargetTraderPosition!.X);
		Assert.Equal(-3f, intent.TargetTraderPosition.Y);
	}

	[Fact]
	public void TraderHandIn_OfAnItemThatIsNotTheDraggedProxy_IsRefused()
	{
		var window = Open();
		window.CaptureGiveToTrader(OtherItem, new NetVector2Msg(1f, 2f));

		var outcome = window.Close();

		Assert.Empty(outcome.Intents);
		Assert.Single(outcome.Refusals);
	}
}
