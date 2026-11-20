using System.Threading;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// Re-keying an item row when the same player returns under a new transport id:
/// the placement (carried, container parent, limb) must survive, and a row that
/// is not owner-keyed or no longer belongs to the old id must be refused.
/// </summary>
public class ItemOwnerReassignTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId G1 = new(2001);
	private static readonly ActorId G2 = new(3001);
	private static readonly ItemData Data = new(1f, false, -1, [], []);

	private static int _operation;

	[Fact]
	public void ReassignCarried_ChangesTheOwnerAndKeepsThePlacement()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 10, ItemLocation.World(1, 2));
		Pickup(kernel, 10, G1);

		var result = Reassign(kernel, 10, G1, G2, 2);

		Assert.True(result.Accepted);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Carried, item.Location.Kind);
		Assert.Equal(G2, item.Location.Owner);
		Assert.Equal(3ul, item.Revision);
	}

	[Fact]
	public void ReassignContained_KeepsTheParent()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 20, ItemLocation.World(1, 2));
		Pickup(kernel, 20, G1);
		Spawn(kernel, 21, ItemLocation.Contained(G1, 20));

		var result = Reassign(kernel, 21, G1, G2, 1);

		Assert.True(result.Accepted, $"reassign rejected: {result.Reason}");
		var item = kernel.FindItem(21)!.Value;
		Assert.Equal(ItemLocationKind.Contained, item.Location.Kind);
		Assert.Equal(G2, item.Location.Owner);
		Assert.Equal(20ul, item.Location.ParentItemId);
		Assert.Equal(G1, kernel.FindItem(20)!.Value.Location.Owner);
	}

	[Fact]
	public void ReassignApplied_KeepsTheLimb()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 30, ItemLocation.World(1, 2));
		Pickup(kernel, 30, G1);
		var applied = kernel.Execute(
			new ApplyLimbItemCommand(NextOperation(), G1, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				30, G1, 3, 2),
			new CommandContext(Epoch, G1));
		Assert.True(applied.IsAccepted);

		var result = Reassign(kernel, 30, G1, G2, 3);

		Assert.True(result.Accepted);
		var item = kernel.FindItem(30)!.Value;
		Assert.Equal(ItemLocationKind.Applied, item.Location.Kind);
		Assert.Equal(G2, item.Location.Owner);
		Assert.Equal(3, item.Location.LimbIndex);
	}

	[Fact]
	public void ReassignRefusesWorldItems()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 40, ItemLocation.World(1, 2));

		var result = Reassign(kernel, 40, G1, G2, 1);

		Assert.False(result.Accepted);
		Assert.Equal(RejectionReason.InvalidTransition, result.Reason);
	}

	[Fact]
	public void ReassignRefusesTerminalItems()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 41, ItemLocation.World(1, 2));
		var destroyed = kernel.Execute(
			new DestroyItemCommand(NextOperation(), Host, Epoch, AuthorityKind.HostOnly,
				41, TerminalKind.Destroyed, 1),
			new CommandContext(Epoch, Host));
		Assert.True(destroyed.IsAccepted);

		var result = Reassign(kernel, 41, G1, G2, 2);

		Assert.False(result.Accepted);
		Assert.Equal(RejectionReason.InvalidTransition, result.Reason);
	}

	[Fact]
	public void ReassignRefusesAStaleOldOwner()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 50, ItemLocation.World(1, 2));
		Pickup(kernel, 50, G2);

		var result = Reassign(kernel, 50, G1, G2, 2);

		Assert.False(result.Accepted);
		Assert.Equal(RejectionReason.InvalidTransition, result.Reason);
	}

	[Fact]
	public void ReassignRefusesAStaleRevision()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 60, ItemLocation.World(1, 2));
		Pickup(kernel, 60, G1);

		var result = Reassign(kernel, 60, G1, G2, 1);

		Assert.False(result.Accepted);
		Assert.Equal(RejectionReason.WrongRevision, result.Reason);
	}

	private static void Spawn(GameStateKernel kernel, ulong id, ItemLocation location)
	{
		var decision = kernel.Execute(
			new SpawnItemCommand(NextOperation(), Host, Epoch, AuthorityKind.HostOnly,
				new ItemIdentity(id, "test_item"), location, 0, Data),
			new CommandContext(Epoch, Host));
		Assert.True(decision.IsAccepted);
	}

	private static void Pickup(GameStateKernel kernel, ulong id, ActorId owner)
	{
		var current = kernel.FindItem(id)!.Value;
		var decision = kernel.Execute(
			new PickUpItemCommand(NextOperation(), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				id, owner, current.Revision),
			new CommandContext(Epoch, owner));
		Assert.True(decision.IsAccepted);
	}

	private static (bool Accepted, RejectionReason? Reason) Reassign(GameStateKernel kernel, ulong id, ActorId oldOwner, ActorId newOwner, ulong expectedRevision)
	{
		var decision = kernel.Execute(
			new ReassignItemOwnerCommand(NextOperation(), Host, Epoch, AuthorityKind.HostOnly,
				id, oldOwner, newOwner, expectedRevision),
			new CommandContext(Epoch, Host));
		return (decision.IsAccepted, decision.Rejection?.Reason);
	}

	private static OperationId NextOperation() => new((ulong)Interlocked.Increment(ref _operation));
}
