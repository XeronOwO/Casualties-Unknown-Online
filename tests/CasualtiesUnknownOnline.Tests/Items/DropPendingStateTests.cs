using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;
using Source = CasualtiesUnknownOnline.Runtime.Session.Items.DropPendingState.Source;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The drop-report pending machine (DropPendingState): the transition decisions
/// behind the departure report — one player input, one report, and ONE ENTRY PER
/// ITEM so a same-frame second departure cannot swallow the first. All game
/// inputs (frame, alive, standalone) are explicit parameters.
/// </summary>
public class DropPendingStateTests
{
	private const ulong ItemA = 100;
	private const ulong ItemB = 200;

	private static DropPendingState StateWithDrop(ulong itemId = ItemA, int frame = 10, long op = 7, Source source = Source.CarriedInventory)
	{
		var state = new DropPendingState();
		state.EnterDrop(itemId, frame, op, source);
		return state;
	}

	[Fact]
	public void EnterDrop_ThenTake_ConsumesWithOpAndSource()
	{
		var state = StateWithDrop(source: Source.World);

		Assert.True(state.TryTake(ItemA, out var pending));
		Assert.Equal(7, pending.Op);
		Assert.Equal(ItemA, pending.ItemId);
		Assert.Equal(Source.World, pending.Source);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void Take_DifferentItem_NotTaken()
	{
		var state = StateWithDrop();

		Assert.False(state.TryTake(ItemB, out var pending));
		Assert.Equal(0UL, pending.ItemId);
		Assert.True(state.HasPending);
	}

	[Fact]
	public void IsPendingFor_MatchesOnlyOwnItem()
	{
		var state = StateWithDrop();

		Assert.True(state.IsPendingFor(ItemA));
		Assert.False(state.IsPendingFor(ItemB));
	}

	[Fact]
	public void ASecondDepartureInTheSameFrame_DoesNotSwallowTheFirst()
	{
		// Ticket drop-pending-single-slot-overwrite, and the reason the container
		// pair needs the same machine: two departures in one frame — a slot release
		// onto an occupied slot drops both occupants, a container expansion unloads
		// one child per refused load — are TWO reports, and one slot let the second
		// EnterDrop overwrite the first, so the first item's report never went out.
		var state = new DropPendingState();
		state.EnterDrop(ItemA, frame: 10, op: 7, Source.CarriedInventory);
		state.EnterDrop(ItemB, frame: 10, op: 8, Source.World);

		Assert.True(state.IsPendingFor(ItemA), "the first departure's report must survive a second departure in the same frame");
		Assert.True(state.IsPendingFor(ItemB));
	}

	[Fact]
	public void TwoDeparturesInOneFrame_BothSettleAndReportAfterTheFrame()
	{
		var state = new DropPendingState();
		state.EnterDrop(ItemA, frame: 10, op: 7, Source.CarriedInventory);
		state.EnterDrop(ItemB, frame: 10, op: 8, Source.World);

		Assert.True(state.TrySettle(ItemA, currentFrame: 11, alive: true, standalone: true, out var first));
		Assert.True(state.TrySettle(ItemB, currentFrame: 11, alive: true, standalone: true, out var second));
		Assert.Equal(7, first.Op);
		Assert.Equal(8, second.Op);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void EnterDrop_SameItemTwice_ReplacesThatItemsOwnEntry()
	{
		var state = StateWithDrop(ItemA, frame: 10, op: 7, source: Source.World);
		state.EnterDrop(ItemA, frame: 11, op: 8, Source.CarriedInventory); // the item cannot leave twice — one entry per item

		Assert.True(state.TryTake(ItemA, out var pending));
		Assert.Equal(8, pending.Op);
		Assert.Equal(Source.CarriedInventory, pending.Source);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void CopyItemIds_IsASnapshotTheSettleCanRemoveFrom()
	{
		var state = StateWithDrop();

		var ids = state.CopyItemIds();
		Assert.True(state.TryTake(ItemA, out _));

		Assert.Equal([ItemA], ids); // the copy still names the taken entry — the caller iterates it while settling
		Assert.Empty(state.CopyItemIds());
	}

	[Fact]
	public void TrySettle_SameFrame_Rejected()
	{
		var state = StateWithDrop(frame: 10);

		Assert.False(state.TrySettle(ItemA, currentFrame: 10, alive: true, standalone: true, out _)); // the throw velocity may still land
		Assert.True(state.HasPending);
	}

	[Fact]
	public void TrySettle_NextFrame_AliveStandalone_Consumed()
	{
		var state = StateWithDrop();

		Assert.True(state.TrySettle(ItemA, currentFrame: 11, alive: true, standalone: true, out var pending));
		Assert.Equal(7, pending.Op);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void TrySettle_DestroyedItem_Rejected()
	{
		var state = StateWithDrop();

		Assert.False(state.TrySettle(ItemA, currentFrame: 11, alive: false, standalone: true, out _));
		Assert.True(state.HasPending);
	}

	[Fact]
	public void TrySettle_NotStandalone_Rejected()
	{
		var state = StateWithDrop();

		Assert.False(state.TrySettle(ItemA, currentFrame: 11, alive: true, standalone: false, out _));
		Assert.True(state.HasPending);
	}

	[Fact]
	public void ResetAll_ReturnsEveryOpAndClears()
	{
		var state = new DropPendingState();
		state.EnterDrop(ItemA, frame: 10, op: 7, Source.CarriedInventory);
		state.EnterDrop(ItemB, frame: 10, op: 8, Source.World);

		var cancelled = new List<DropPendingState.Pending>();
		state.ResetAll(cancelled);

		Assert.Equal(2, cancelled.Count);
		Assert.Contains(cancelled, pending => pending.ItemId == ItemA && pending.Op == 7);
		Assert.Contains(cancelled, pending => pending.ItemId == ItemB && pending.Op == 8);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void FullSequence_DepartureThenThrow_ReturnsToIdle()
	{
		var state = new DropPendingState();
		Assert.False(state.HasPending);

		state.EnterDrop(ItemA, frame: 10, op: 1, Source.CarriedInventory);
		Assert.True(state.HasPending);

		Assert.True(state.TryTake(ItemA, out var thrown));
		Assert.Equal(1, thrown.Op);
		Assert.False(state.HasPending);
		Assert.False(state.TryTake(ItemA, out _)); // nothing left to take
	}
}
