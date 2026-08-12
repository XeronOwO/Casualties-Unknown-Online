using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The drop-operation pending machine (DropPendingState): the transition
/// decisions behind the drop→throw merge — one player input, one report.
/// All game inputs (frame, alive, standalone) are explicit parameters.
/// </summary>
public class DropPendingStateTests
{
	private const ulong ItemA = 100;
	private const ulong ItemB = 200;

	private static DropPendingState StateWithDrop(ulong itemId = ItemA, int frame = 10, long op = 7)
	{
		var state = new DropPendingState();
		state.EnterDrop(itemId, frame, op);
		return state;
	}

	[Fact]
	public void EnterDrop_ThenConsumeByThrow_ConsumesWithOp()
	{
		var state = StateWithDrop();

		Assert.True(state.TryConsumeByThrow(ItemA, out var dropped));
		Assert.Equal(7, dropped.Op);
		Assert.Equal(ItemA, dropped.ItemId);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void ConsumeByThrow_DifferentItem_NotConsumed()
	{
		var state = StateWithDrop();

		Assert.False(state.TryConsumeByThrow(ItemB, out var dropped));
		Assert.Equal(0UL, dropped.ItemId);
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
	public void TryFlush_SameFrame_Rejected()
	{
		var state = StateWithDrop(frame: 10);

		Assert.False(state.TryFlush(10, alive: true, standalone: true, out _)); // the throw velocity may still land
		Assert.True(state.HasPending);
	}

	[Fact]
	public void TryFlush_NextFrame_AliveStandalone_Consumed()
	{
		var state = StateWithDrop();

		Assert.True(state.TryFlush(11, alive: true, standalone: true, out var op));
		Assert.Equal(7, op);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void TryFlush_DestroyedItem_Rejected()
	{
		var state = StateWithDrop();

		Assert.False(state.TryFlush(11, alive: false, standalone: true, out _));
		Assert.True(state.HasPending);
	}

	[Fact]
	public void TryFlush_NotStandalone_Rejected()
	{
		var state = StateWithDrop();

		Assert.False(state.TryFlush(11, alive: true, standalone: false, out _));
		Assert.True(state.HasPending);
	}

	[Fact]
	public void TryCancel_Matches_ReturnsOp()
	{
		var state = StateWithDrop();

		Assert.True(state.TryCancel(ItemA, out var op));
		Assert.Equal(7, op);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void TryCancel_DifferentItem_NoOp()
	{
		var state = StateWithDrop();

		Assert.False(state.TryCancel(ItemB, out var op));
		Assert.Equal(0, op);
		Assert.True(state.HasPending);
	}

	[Fact]
	public void TryReset_ReturnsOp_AndClears()
	{
		var state = StateWithDrop();

		Assert.True(state.TryReset(out var op));
		Assert.Equal(7, op);
		Assert.False(state.HasPending);
	}

	[Fact]
	public void EnterDrop_OverwritesPriorPending()
	{
		var state = StateWithDrop(ItemA, frame: 10, op: 7);
		state.EnterDrop(ItemB, frame: 11, op: 8); // the caller flushed the different item first

		Assert.False(state.IsPendingFor(ItemA));
		Assert.True(state.TryConsumeByThrow(ItemB, out var dropped));
		Assert.Equal(8, dropped.Op);
	}

	[Fact]
	public void FullSequence_DropThrow_ReturnsToIdle()
	{
		var state = new DropPendingState();
		Assert.False(state.HasPending);

		state.EnterDrop(ItemA, frame: 10, op: 1);
		Assert.True(state.HasPending);

		Assert.True(state.TryConsumeByThrow(ItemA, out _));
		Assert.False(state.HasPending);
		Assert.False(state.TryConsumeByThrow(ItemA, out _)); // nothing left to consume
	}
}
