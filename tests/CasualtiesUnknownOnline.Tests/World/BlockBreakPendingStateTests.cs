using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The block-break report state machine (BlockBreakPendingState): a local
/// break holds its report one frame for the drops (the break + drops go out as
/// ONE BlockDamagedMsg — one message, one verdict), the flush is refused on the
/// break frame itself (the drops' Item.Start runs the frame AFTER the break),
/// and a world-left resets it so the operation trace stays balanced. Frame and
/// coordinates are explicit inputs.
/// </summary>
public class BlockBreakPendingStateTests
{
	private const int BreakFrame = 100;
	private const long Op = 777;

	private static BlockDropEntryMsg Drop(ulong itemId) => new()
	{
		ItemId = itemId,
		Item = new CharacterItemMsg { ItemId = "water" },
		Position = new NetVector2Msg { X = 1f, Y = 2f },
		Velocity = new NetVector2Msg { X = 0f, Y = 0f },
	};

	private static TrapDropEntryMsg BuildingDrop(ulong itemId) => new()
	{
		ItemId = itemId,
		Item = new CharacterItemMsg { ItemId = "metalscrap" },
		Position = new NetVector2Msg { X = 1f, Y = 2f },
		Velocity = new NetVector2Msg { X = 3f, Y = -4f },
		FreshItemDrop = true,
	};

	[Fact]
	public void EnterBreak_CurrentBroken()
	{
		var state = new BlockBreakPendingState();

		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);

		Assert.Equal(BlockBreakPendingState.Phase.Broken, state.Current);
	}

	[Fact]
	public void EnterBreak_ThenSameFrameFlush_Refused()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);

		// The drops' Item.Start runs NEXT frame — flushing now sends half the drops.
		Assert.False(state.TryFlush(BreakFrame, out _));
		Assert.True(state.Current == BlockBreakPendingState.Phase.Broken, "the break is still pending");
	}

	[Fact]
	public void EnterBreak_ThenNextFrameFlush_ReturnsFullPayload()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);
		Assert.True(state.TryAddDrop(Drop(1)));
		Assert.True(state.TryAddDrop(Drop(2)));

		Assert.True(state.TryFlush(BreakFrame + 1, out var flushed));
		Assert.Equal(5f, flushed.PosX);
		Assert.Equal(-3f, flushed.PosY);
		Assert.Equal(40f, flushed.Dmg);
		Assert.True(flushed.MetalBonus, "the bonus-metal flag must survive the pending hold");
		Assert.Equal(Op, flushed.Op);
		Assert.Equal(2, flushed.Drops.Count);
		Assert.Equal(BlockBreakPendingState.Phase.Idle, state.Current);
	}

	[Fact]
	public void Flush_Twice_SecondRefused()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);
		Assert.True(state.TryFlush(BreakFrame + 1, out _));

		Assert.False(state.TryFlush(BreakFrame + 2, out _));
	}

	[Fact]
	public void Flush_WithoutBreak_Refused()
	{
		var state = new BlockBreakPendingState();

		Assert.False(state.TryFlush(0, out _));
	}

	[Fact]
	public void TryAddDrop_WithoutBreak_False()
	{
		var state = new BlockBreakPendingState();

		// The drop falls back to a standalone spawn report.
		Assert.False(state.TryAddDrop(Drop(1)));
	}

	[Fact]
	public void TryAddDrop_FoldsIntoThePendingList()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);

		Assert.True(state.TryAddDrop(Drop(1)));
		Assert.True(state.TryAddDrop(Drop(2)));

		state.TryFlush(BreakFrame + 1, out var flushed);
		Assert.Equal(2, flushed.Drops.Count);
	}

	[Fact]
	public void TryAddBuildingDrop_FoldsIntoThePendingList()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);

		Assert.True(state.TryAddBuildingDrop(BuildingDrop(9)));
		Assert.True(state.TryAddBuildingDrop(BuildingDrop(10)));

		state.TryFlush(BreakFrame + 1, out var flushed);
		Assert.Equal(2, flushed.BuildingDrops.Count);
		Assert.Equal(9ul, flushed.BuildingDrops[0].ItemId);
		Assert.Equal(10ul, flushed.BuildingDrops[1].ItemId);
		Assert.True(flushed.BuildingDrops[0].FreshItemDrop);
	}

	[Fact]
	public void TryAddBuildingDrop_WithoutBreak_False()
	{
		var state = new BlockBreakPendingState();

		// The building drop falls back to a standalone spawn report.
		Assert.False(state.TryAddBuildingDrop(BuildingDrop(1)));
	}

	[Fact]
	public void TryReset_CancelsAndReturnsOp()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);

		Assert.True(state.TryReset(out var resetOp));
		Assert.Equal(Op, resetOp);
		Assert.Equal(BlockBreakPendingState.Phase.Idle, state.Current);
	}

	[Fact]
	public void TryReset_WithoutBreak_False()
	{
		var state = new BlockBreakPendingState();

		Assert.False(state.TryReset(out _));
	}

	[Fact]
	public void EnterBreak_OverwritesPreviousPending()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(1f, 1f, 10f, false, 11, BreakFrame);
		state.EnterBreak(2f, 2f, 20f, true, Op, BreakFrame + 1);

		state.TryFlush(BreakFrame + 2, out var flushed);
		Assert.Equal(2f, flushed.PosX);
		Assert.Equal(20f, flushed.Dmg);
		Assert.True(flushed.MetalBonus, "the overwrite carries its own bonus flag");
		Assert.Equal(Op, flushed.Op);
	}

	[Fact]
	public void FullSequence_BreakFoldFlushReset()
	{
		var state = new BlockBreakPendingState();
		state.EnterBreak(5f, -3f, 40f, true, Op, BreakFrame);
		Assert.True(state.TryAddDrop(Drop(1)));
		Assert.True(state.TryFlush(BreakFrame + 1, out var flushed));
		Assert.Single(flushed.Drops);
		Assert.Equal(BlockBreakPendingState.Phase.Idle, state.Current);

		// A new break starts clean.
		state.EnterBreak(9f, 9f, 50f, false, Op + 1, BreakFrame + 2);
		Assert.Equal(BlockBreakPendingState.Phase.Broken, state.Current);
		Assert.True(state.TryReset(out var resetOp));
		Assert.Equal(Op + 1, resetOp);
	}
}
