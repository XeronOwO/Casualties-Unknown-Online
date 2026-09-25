using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

/// <summary>
/// Wire regression for the native inventory intent. Slot 0 is a HAND and limb 0
/// is the first limb: protobuf omits zero values, so both operands ride the wire
/// as <c>value + 1</c> — the encoding <c>PlayerHealRequestMsg.LimbSelection</c>
/// already carries. Without it a release into a hand arrives as -1 and is either
/// refused by the host or silently re-aimed at another limb.
/// </summary>
public class RemoteInventoryIntentWireTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(-1)]
	public void SlotOperands_RoundTripThroughTheWire(int slotIndex)
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.PickUpToSlot,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetSlotIndex = slotIndex,
		});

		Assert.Equal(slotIndex, decoded.TargetSlotIndex);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	[InlineData(-1)]
	public void LimbOperands_RoundTripThroughTheWire(int limbIndex)
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.ApplyToLimb,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetLimbIndex = limbIndex,
		});

		Assert.Equal(limbIndex, decoded.TargetLimbIndex);
	}

	[Fact]
	public void TheOtherOperands_SurviveTheSameRoundTrip()
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.TransferToBody,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetContainerInstanceId = 8,
			TargetSlotIndex = 0,
			TargetBodySteamId = 43,
			TargetLimbIndex = -1,
		});

		Assert.Equal(RemoteInventoryIntentKind.TransferToBody, decoded.Kind);
		Assert.Equal(42UL, decoded.OwnerSteamId);
		Assert.Equal(7UL, decoded.ItemInstanceId);
		Assert.Equal(8UL, decoded.TargetContainerInstanceId);
		Assert.Equal(0, decoded.TargetSlotIndex);
		Assert.Equal(43UL, decoded.TargetBodySteamId);
		Assert.Equal(-1, decoded.TargetLimbIndex);
	}

	[Theory]
	[InlineData(0f)]
	[InlineData(0.25f)]
	[InlineData(1500f)]
	public void TheDrainAmount_RoundTripsThroughTheWire(float amount)
	{
		// Zero is a legal amount — a frame whose delta time made the tick empty still
		// runs the native call — and protobuf's default-zero rule preserves it: unlike
		// a slot or a limb index, this operand has no "not used" state to be told
		// apart from, so it needs no `value + 1` encoding.
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.Drain,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			Amount = amount,
		});

		Assert.Equal(RemoteInventoryIntentKind.Drain, decoded.Kind);
		Assert.Equal(amount, decoded.Amount);
	}

	[Fact]
	public void TheContainerChildBatchOperands_RoundTripThroughTheWire()
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.MoveContainerChildren,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetContainerInstanceId = 8,
			TargetSlotIndex = -1,
			TargetLimbIndex = -1,
		});

		Assert.Equal(RemoteInventoryIntentKind.MoveContainerChildren, decoded.Kind);
		Assert.Equal(7UL, decoded.ItemInstanceId);
		Assert.Equal(8UL, decoded.TargetContainerInstanceId);
		Assert.Equal(-1, decoded.TargetSlotIndex);
		Assert.Equal(-1, decoded.TargetLimbIndex);
	}

	[Fact]
	public void TheTwoItemOperands_RoundTripThroughTheWire()
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.CombineItems,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetItemInstanceId = 9,
		});

		Assert.Equal(RemoteInventoryIntentKind.CombineItems, decoded.Kind);
		Assert.Equal(7UL, decoded.ItemInstanceId);
		Assert.Equal(9UL, decoded.TargetItemInstanceId);
	}

	[Fact]
	public void TheTraderPosition_RoundTripsThroughTheWire()
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.GiveToTrader,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetTraderPosition = new NetVector2Msg(12.5f, -3.25f),
		});

		Assert.Equal(RemoteInventoryIntentKind.GiveToTrader, decoded.Kind);
		Assert.Equal(12.5f, decoded.TargetTraderPosition!.X);
		Assert.Equal(-3.25f, decoded.TargetTraderPosition.Y);
	}

	[Fact]
	public void AnAbsentTraderPosition_StaysAbsent()
	{
		// The operand is nullable on purpose: a kind that takes no trader must not
		// arrive looking like a trader standing at the origin, because the owner would
		// then resolve a trader nobody pointed at.
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.UseItem,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
		});

		Assert.Null(decoded.TargetTraderPosition);
		Assert.Equal(0UL, decoded.TargetItemInstanceId);
	}

	[Fact]
	public void ATraderStandingAtTheOrigin_IsNotAnAbsentOperand()
	{
		// The nullable member has to tell a trader that really stands at (0,0) apart from
		// a kind that carries no trader operand: a decoded (0,0) that arrived as "absent"
		// would send the owner looking for a trader nobody pointed at.
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.GiveToTrader,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetTraderPosition = new NetVector2Msg(0f, 0f),
		});

		Assert.NotNull(decoded.TargetTraderPosition);
		Assert.Equal(0f, decoded.TargetTraderPosition!.X);
		Assert.Equal(0f, decoded.TargetTraderPosition.Y);
	}

	[Fact]
	public void TheBatteryLoadOperands_RoundTripThroughTheWire()
	{
		var decoded = RoundTrip(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.LoadBattery,
			OwnerSteamId = 42,
			ItemInstanceId = 7,
			TargetItemInstanceId = 9,
		});

		Assert.Equal(RemoteInventoryIntentKind.LoadBattery, decoded.Kind);
		Assert.Equal(7UL, decoded.ItemInstanceId);
		Assert.Equal(9UL, decoded.TargetItemInstanceId);
		Assert.Null(decoded.TargetTraderPosition);
	}

	private static RemoteInventoryIntentMsg RoundTrip(RemoteInventoryIntentMsg msg) =>
		NetPacket.DecodePayload<RemoteInventoryIntentMsg>(NetPacket.Encode(NetMsg.RemoteInventoryIntent, msg));
}
