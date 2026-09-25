using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the operand ORDER of the two-item replays. The owner-side executor
/// is scene-bound and cannot be unit-tested, and a swapped argument there charges the
/// wrong item without throwing, so the order decision lives in
/// <see cref="RemoteItemInteractionOperands"/> and is pinned here: the HIT item is the
/// receiver in both native calls, the dragged item is the one consumed.
/// </summary>
public class RemoteItemInteractionOperandsTests
{
	private const ulong Dragged = 41;
	private const ulong Hit = 42;

	[Fact]
	public void CombineTargets_MakeTheHitItemTheReceiverAndTheDraggedItemTheConsumedOne()
	{
		// PlayerCamera.cs:1602 is CombineItems(hitItem, dragItem) and the merge credits
		// it1 (Body.cs:1280-1282): swapping these would charge the dragged item.
		var (receiver, consumed) = RemoteItemInteractionOperands.CombineTargets(Dragged, Hit);

		Assert.Equal(Hit, receiver);
		Assert.Equal(Dragged, consumed);
	}

	[Fact]
	public void BatteryLoadTargets_MakeTheHitItemTheReceiverAndTheDraggedItemTheBattery()
	{
		// PlayerCamera.cs:1550 is hitItem.battery.LoadBattery(dragItem): the receiver owns
		// the battery component the call runs on, the dragged item is consumed by it.
		var (receiver, battery) = RemoteItemInteractionOperands.BatteryLoadTargets(Dragged, Hit);

		Assert.Equal(Hit, receiver);
		Assert.Equal(Dragged, battery);
	}

	[Fact]
	public void TheOrderDecision_ReallyFollowsTheCallerSTargetOperand()
	{
		// A pair function that ignored its arguments would make the two cases above pass
		// by accident: the receiver has to follow whichever operand the caller names as
		// the hit item.
		Assert.Equal(Hit, RemoteItemInteractionOperands.CombineTargets(Dragged, Hit).Receiver);
		Assert.Equal(Dragged, RemoteItemInteractionOperands.CombineTargets(Hit, Dragged).Receiver);
		Assert.Equal(Hit, RemoteItemInteractionOperands.BatteryLoadTargets(Dragged, Hit).Receiver);
		Assert.Equal(Dragged, RemoteItemInteractionOperands.BatteryLoadTargets(Hit, Dragged).Receiver);
	}
}
