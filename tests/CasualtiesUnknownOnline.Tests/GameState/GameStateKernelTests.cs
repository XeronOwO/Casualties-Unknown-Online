using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class GameStateKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId Guest = new(2001);

	[Fact]
	public void SpawnItem_CommitsWorldItemAndRevision()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2));

		Assert.True(decision.IsAccepted);
		Assert.Equal(1ul, decision.Batch!.GlobalRevision);
		var item = kernel.FindItem(10);
		Assert.NotNull(item);
		Assert.Equal(ItemLocationKind.World, item.Value.Location.Kind);
		Assert.Equal(1ul, item.Value.Revision);
	}

	[Fact]
	public void DuplicateOperationId_ReturnsOriginalDecisionAndDoesNotApplyTwice()
	{
		var kernel = new GameStateKernel(Epoch);
		var first = Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2));
		var second = Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2));

		Assert.True(second.IsAccepted);
		Assert.Equal(first.Batch!.GlobalRevision, second.Batch!.GlobalRevision);
		Assert.Equal(1, kernel.QueryItems().Count);
	}

	[Fact]
	public void DuplicateSpawnWithDifferentOperation_IsRejectedAsConflict()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2)).IsAccepted);
		var second = Spawn(kernel, 2, 10, "water", ItemLocation.World(3, 4));

		Assert.False(second.IsAccepted);
		Assert.Equal(RejectionReason.Conflict, second.Rejection!.Reason);
	}

	[Fact]
	public void StaleRevision_RejectsPickup()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2)).IsAccepted);

		var stale = Pickup(kernel, 2, 10, Guest, expectedRevision: 0);

		Assert.False(stale.IsAccepted);
		Assert.Equal(RejectionReason.WrongRevision, stale.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.World, kernel.FindItem(10)!.Value.Location.Kind);
	}

	[Fact]
	public void PickupThenDrop_DrivesCarriedAndWorldFacts()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Pickup(kernel, 2, 10, Guest, expectedRevision: 1).IsAccepted);

		var carried = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Carried, carried.Location.Kind);
		Assert.Equal(Guest, carried.Location.Owner);
		Assert.Equal(2ul, carried.Revision);

		Assert.True(Drop(kernel, 3, 10, Guest, ItemLocation.World(5, 6), expectedRevision: 2).IsAccepted);

		var world = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.World, world.Location.Kind);
		Assert.Equal(3ul, world.Revision);
	}

	[Fact]
	public void DestroyedItem_CannotBePickedUpAgain()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Destroy(kernel, 2, 10, expectedRevision: 1).IsAccepted);

		var terminal = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Terminal, terminal.Location.Kind);

		var resurrect = Pickup(kernel, 3, 10, Guest, expectedRevision: 2);
		Assert.False(resurrect.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, resurrect.Rejection!.Reason);
	}

	[Fact]
	public void WrongEpoch_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		var command = new SpawnItemCommand(
			new OperationId(1),
			Host,
			new RunEpoch(999),
			AuthorityKind.HostOnly,
			new ItemIdentity(10, "water"),
			ItemLocation.World(1, 2),
			0);

		var decision = kernel.Execute(command, new CommandContext(new RunEpoch(999), Host));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.WrongEpoch, decision.Rejection!.Reason);
	}

	[Fact]
	public void Apply_ReducesCommittedBatchOnReplaySideAndIsIdempotent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Spawn(source, 1, 10, "water", ItemLocation.World(1, 2)).Batch!;

		var replay = new GameStateKernel(Epoch);
		Assert.True(replay.Apply(batch).Success);
		Assert.NotNull(replay.FindItem(10));

		var firstApply = replay.CreateCheckpoint().GlobalRevision;
		Assert.True(replay.Apply(batch).Success);
		Assert.Equal(firstApply, replay.CreateCheckpoint().GlobalRevision);
		Assert.Equal(1, replay.QueryItems().Count);
	}

	[Fact]
	public void CheckpointRoundTrip_RestoresItemStateAndRevision()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Pickup(kernel, 2, 10, Guest, expectedRevision: 1).IsAccepted);
		var checkpoint = kernel.CreateCheckpoint();

		var restored = new GameStateKernel(new RunEpoch(99));
		Assert.True(restored.Restore(checkpoint).Success);
		Assert.Equal(2ul, restored.CreateCheckpoint().GlobalRevision);

		var item = restored.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Carried, item.Location.Kind);
		Assert.Equal(Guest, item.Location.Owner);
	}

	private static Decision Spawn(GameStateKernel kernel, ulong op, ulong instanceId, string definition, ItemLocation location) =>
		kernel.Execute(
			new SpawnItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, new ItemIdentity(instanceId, definition), location, 0),
			new CommandContext(Epoch, Host));

	private static Decision Pickup(GameStateKernel kernel, ulong op, ulong instanceId, ActorId owner, ulong expectedRevision) =>
		kernel.Execute(
			new PickUpItemCommand(new OperationId(op), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated, instanceId, owner, expectedRevision),
			new CommandContext(Epoch, owner));

	private static Decision Drop(GameStateKernel kernel, ulong op, ulong instanceId, ActorId owner, ItemLocation newLocation, ulong expectedRevision) =>
		kernel.Execute(
			new DropItemCommand(new OperationId(op), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated, instanceId, newLocation, expectedRevision),
			new CommandContext(Epoch, owner));

	private static Decision Destroy(GameStateKernel kernel, ulong op, ulong instanceId, ulong expectedRevision) =>
		kernel.Execute(
			new DestroyItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, instanceId, TerminalKind.Destroyed, expectedRevision),
			new CommandContext(Epoch, Host));
}
