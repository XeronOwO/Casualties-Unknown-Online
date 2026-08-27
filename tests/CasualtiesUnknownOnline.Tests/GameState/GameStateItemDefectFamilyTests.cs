using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// Named historical item-defect families mapped onto the kernel first slice.
/// These are not replay-differential tests yet; they prove the invariants that
/// each family relies on.
/// </summary>
public class GameStateItemDefectFamilyTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly RunEpoch OldEpoch = new(0);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId G1 = new(2001);
	private static readonly ActorId G2 = new(3001);

	[Fact]
	public void DuplicateOperation_DoesNotCreateGhostOrDuplicateItem()
	{
		var kernel = new GameStateKernel(Epoch);
		var first = Spawn(kernel, 1, 42, ItemLocation.World(1, 2));
		var second = Spawn(kernel, 1, 42, ItemLocation.World(3, 4));

		Assert.True(first.IsAccepted);
		Assert.True(second.IsAccepted);
		Assert.Equal(first.Batch!.GlobalRevision, second.Batch!.GlobalRevision);
		Assert.Equal(1, kernel.QueryItems().Count);
		Assert.Equal(ItemLocation.World(1, 2), kernel.FindItem(42)!.Value.Location);
	}

	[Fact]
	public void FirstWriterWins_SecondPickupIsRejectedAsConflict()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 42, ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Pickup(kernel, 2, 42, G1, 1).IsAccepted);

		var second = Pickup(kernel, 3, 42, G2, 2);

		Assert.False(second.IsAccepted);
		Assert.Equal(RejectionReason.Conflict, second.Rejection!.Reason);
		Assert.Equal(G1, kernel.FindItem(42)!.Value.Location.Owner);
	}

	[Fact]
	public void DuplicateDrop_WithSameOperation_IsIdempotent()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 42, ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Pickup(kernel, 2, 42, G1, 1).IsAccepted);

		var first = Drop(kernel, 3, 42, G1, ItemLocation.World(10, 20), 2);
		var second = Drop(kernel, 3, 42, G1, ItemLocation.World(99, 99), 2);

		Assert.True(first.IsAccepted);
		Assert.True(second.IsAccepted);
		Assert.Equal(first.Batch!.GlobalRevision, second.Batch!.GlobalRevision);
		Assert.Equal(ItemLocation.World(10, 20), kernel.FindItem(42)!.Value.Location);
	}

	[Fact]
	public void DestroyedItem_CheckpointRestore_StillCannotResurrect()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 42, ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Destroy(kernel, 2, 42, 1).IsAccepted);
		var checkpoint = kernel.CreateCheckpoint();

		var restored = new GameStateKernel(new RunEpoch(99));
		Assert.True(restored.Restore(checkpoint).Success);

		var resurrect = Pickup(restored, 3, 42, G1, 2);
		Assert.False(resurrect.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, resurrect.Rejection!.Reason);
	}

	[Fact]
	public void OldEpochCommand_IsRejected_NoCrossRunResidue()
	{
		var kernel = new GameStateKernel(Epoch);
		var command = new SpawnItemCommand(
			new OperationId(1),
			Host,
			OldEpoch,
			AuthorityKind.HostOnly,
			new ItemIdentity(42, "test_item"),
			ItemLocation.World(1, 2),
			0);

		var decision = kernel.Execute(command, new CommandContext(OldEpoch, Host));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.WrongEpoch, decision.Rejection!.Reason);
		Assert.Equal(0, kernel.QueryItems().Count);
	}

	private static Decision Spawn(GameStateKernel kernel, ulong op, ulong id, ItemLocation location) =>
		kernel.Execute(
			new SpawnItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, new ItemIdentity(id, "test_item"), location, 0),
			new CommandContext(Epoch, Host));

	private static Decision Pickup(GameStateKernel kernel, ulong op, ulong id, ActorId owner, ulong expectedRevision) =>
		kernel.Execute(
			new PickUpItemCommand(new OperationId(op), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated, id, owner, expectedRevision),
			new CommandContext(Epoch, owner));

	private static Decision Drop(GameStateKernel kernel, ulong op, ulong id, ActorId owner, ItemLocation location, ulong expectedRevision) =>
		kernel.Execute(
			new DropItemCommand(new OperationId(op), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated, id, location, expectedRevision),
			new CommandContext(Epoch, owner));

	private static Decision Destroy(GameStateKernel kernel, ulong op, ulong id, ulong expectedRevision) =>
		kernel.Execute(
			new DestroyItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, id, TerminalKind.Destroyed, expectedRevision),
			new CommandContext(Epoch, Host));
}
