using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

public class TrapDropPendingStateTests
{
	[Fact]
	public void TryAddDrop_WithoutPending_ReturnsFalse()
	{
		var state = new TrapDropPendingState();

		Assert.False(state.TryAddDrop(NewDrop(1, 0f, 0f)));
	}

	[Fact]
	public void Enter_TracksPendingAndRejectsDuplicateAtSamePosition()
	{
		var state = new TrapDropPendingState();
		state.Enter(EntityEventKind.MineExploded, 1f, 2f, 0, startFrame: 10);
		state.Enter(EntityEventKind.MineExploded, 1.2f, 2.1f, 0, startFrame: 10);

		Assert.Equal(1, state.Count);
	}

	[Fact]
	public void TryAddDrop_MatchesNearestPendingTrap()
	{
		var state = new TrapDropPendingState();
		state.Enter(EntityEventKind.MineExploded, 0f, 0f, 0, startFrame: 10);
		state.Enter(EntityEventKind.TurretSelfDestructed, 100f, 100f, 0, startFrame: 10);

		Assert.True(state.TryAddDrop(NewDrop(7, 1f, 1f)));
		Assert.True(state.TryFlush(12, out var events));

		var mine = Assert.Single(events, e => e.Kind == EntityEventKind.MineExploded);
		Assert.Single(mine.Drops);
		var turret = Assert.Single(events, e => e.Kind == EntityEventKind.TurretSelfDestructed);
		Assert.Empty(turret.Drops);
	}

	[Fact]
	public void TryFlush_WaitsHoldFramesAndReturnsCollectedDrops()
	{
		var state = new TrapDropPendingState();
		state.Enter(EntityEventKind.CrystalUnstableExploded, 3f, 4f, 2, startFrame: 10);
		Assert.True(state.TryAddDrop(NewDrop(9, 3f, 4f)));

		Assert.False(state.TryFlush(11, out _));
		Assert.True(state.TryFlush(12, out var events));

		var evt = Assert.Single(events);
		Assert.Equal(EntityEventKind.CrystalUnstableExploded, evt.Kind);
		Assert.Equal(3f, evt.Position.X);
		Assert.Equal(4f, evt.Position.Y);
		Assert.Equal(2, evt.Extra);
		Assert.Single(evt.Drops);
		Assert.Equal(9ul, evt.Drops[0].ItemId);
	}

	[Fact]
	public void Reset_ClearsPending()
	{
		var state = new TrapDropPendingState();
		state.Enter(EntityEventKind.MineExploded, 0f, 0f, 0, startFrame: 10);

		state.Reset();

		Assert.Equal(0, state.Count);
		Assert.False(state.TryFlush(12, out _));
	}

	private static TrapDropEntryMsg NewDrop(ulong itemId, float x, float y) =>
		new()
		{
			ItemId = itemId,
			Position = new NetVector2Msg(x, y),
		};
}
