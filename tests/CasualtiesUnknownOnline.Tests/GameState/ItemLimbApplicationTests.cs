using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// The APPLIED location family (user ruling 2026-10-11, option 1): an item the
/// game's own limb action turned into a limb component KEEPS its instance id. It
/// stops being carried and becomes located on that limb, and taking it off is a
/// move to the operator — never a mint, in either direction.
/// <para>
/// The kernel half only: what the production apply and removal paths do with
/// these commands is the next stage of the same ruling.
/// </para>
/// </summary>
public class ItemLimbApplicationTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId Guest = new(2001);

	[Fact]
	public void Apply_MovesTheCarriedItemOntoTheLimbAndKeepsItsInstanceId()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.World(1, 2));
		Pickup(kernel, 2, 10, Host, expectedRevision: 1);

		var applied = Apply(kernel, 3, 10, Host, limbIndex: 3, expectedRevision: 2);

		Assert.True(applied.IsAccepted, applied.Rejection?.Message);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Applied, item.Location.Kind);
		Assert.Equal(Host, item.Location.Owner);
		Assert.Equal(3, item.Location.LimbIndex);
		Assert.Equal(10ul, item.Identity.InstanceId);
		Assert.Equal("splint", item.Identity.DefinitionId);
		Assert.Equal(3ul, item.Revision);
	}

	[Fact]
	public void Apply_KeepsThePayloadTheReportCarried()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));

		var applied = Apply(kernel, 2, 10, Host, limbIndex: 4, expectedRevision: 1, condition: 0.62f);

		Assert.True(applied.IsAccepted, applied.Rejection?.Message);
		Assert.Equal(0.62f, kernel.FindItem(10)!.Value.Data.Condition);
	}

	[Fact]
	public void Apply_OfATerminalItem_IsRejectedAndStaysTerminal()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));
		Assert.True(Destroy(kernel, 2, 10, expectedRevision: 1).IsAccepted);

		var applied = Apply(kernel, 3, 10, Host, limbIndex: 1, expectedRevision: 2);

		Assert.False(applied.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, applied.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Terminal, kernel.FindItem(10)!.Value.Location.Kind);
	}

	[Fact]
	public void Apply_WithAStaleRevision_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));

		var applied = Apply(kernel, 2, 10, Host, limbIndex: 1, expectedRevision: 0);

		Assert.False(applied.IsAccepted);
		Assert.Equal(RejectionReason.WrongRevision, applied.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Carried, kernel.FindItem(10)!.Value.Location.Kind);
	}

	[Theory]
	[InlineData(0ul, 3)]
	[InlineData(1001ul, -1)]
	public void Apply_NamingNoLimb_IsRejected(ulong owner, int limbIndex)
	{
		// Neither half may be defaulted: owner 0 is not an actor and limb -1 is not
		// a limb, and an applied item with either would be a row nobody could place.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));

		var applied = Apply(kernel, 2, 10, new ActorId(owner), limbIndex, expectedRevision: 1);

		Assert.False(applied.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, applied.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Carried, kernel.FindItem(10)!.Value.Location.Kind);
	}

	[Fact]
	public void Apply_ReportedTwice_IsAcceptedAndLeavesTheItemOnTheSameLimb()
	{
		// The affected side reports the apply once, but the same fact may reach the
		// kernel again (a re-report, a repair). A repeated observation is a
		// re-assertion of the same location, never a divergence.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));
		Assert.True(Apply(kernel, 2, 10, Host, limbIndex: 2, expectedRevision: 1).IsAccepted);

		var again = Apply(kernel, 3, 10, Host, limbIndex: 2, expectedRevision: 2);

		Assert.True(again.IsAccepted, again.Rejection?.Message);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Applied, item.Location.Kind);
		Assert.Equal(2, item.Location.LimbIndex);
	}

	[Fact]
	public void AppliedItem_CannotBePickedUp()
	{
		// The item is held by the limb, so no pickup reaches it. Accepting one would
		// move the item while the component that IS the item stayed on the limb —
		// one item in two places — so the request is refused by name.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));
		Assert.True(Apply(kernel, 2, 10, Host, limbIndex: 2, expectedRevision: 1).IsAccepted);

		var pickup = Pickup(kernel, 3, 10, Guest, expectedRevision: 2);

		Assert.False(pickup.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, pickup.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Applied, kernel.FindItem(10)!.Value.Location.Kind);
		Assert.Equal(2, kernel.FindItem(10)!.Value.Location.LimbIndex);

		// The state, not the revision, is what makes the gesture impossible: a report
		// whose revision is stale as well is answered with the same refusal, so the
		// family's rule is what a caller diagnoses rather than a revision race.
		var stale = Pickup(kernel, 4, 10, Guest, expectedRevision: 0);
		Assert.Equal(RejectionReason.InvalidTransition, stale.Rejection!.Reason);
	}

	[Fact]
	public void AppliedItem_CannotBeDropped()
	{
		// The pickup rule's other half: an item held by a limb is not dropped out of
		// an inventory either.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));
		Assert.True(Apply(kernel, 2, 10, Host, limbIndex: 2, expectedRevision: 1).IsAccepted);

		var drop = Drop(kernel, 3, 10, Host, ItemLocation.World(5, 6), expectedRevision: 2);

		Assert.False(drop.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, drop.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Applied, kernel.FindItem(10)!.Value.Location.Kind);
	}

	[Fact]
	public void SpawnDirectlyOntoALimb_IsRejected()
	{
		// The applied family has one way in — the relocation that names the limb and
		// needs the item to exist. A spawn that claimed one would create an item
		// already installed on somebody's limb, and (because only that limb's owner
		// may report an applied row's end) nothing another peer reported could ever
		// correct it.
		var kernel = new GameStateKernel(Epoch);

		var spawn = Spawn(kernel, 1, 10, "splint", ItemLocation.Applied(Guest, 3));

		Assert.False(spawn.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, spawn.Rejection!.Reason);
		Assert.Null(kernel.FindItem(10));
	}

	[Fact]
	public void AppliedItem_CannotBeCooked()
	{
		// An item held by a limb is in no cooker, and the cook's first half would end
		// a row whose component stayed on the limb.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "meat", ItemLocation.Carried(Host));
		Assert.True(Apply(kernel, 2, 10, Host, limbIndex: 2, expectedRevision: 1).IsAccepted);

		var cook = kernel.Execute(
			new CookItemCommand(
				new OperationId(3),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(10, "meat"),
				new ItemIdentity(11, "cookedmeat"),
				ItemLocation.World(1, 2),
				null,
				2),
			new CommandContext(Epoch, Host));

		Assert.False(cook.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, cook.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Applied, kernel.FindItem(10)!.Value.Location.Kind);
		Assert.Null(kernel.FindItem(11));
	}

	[Fact]
	public void Remove_MovesTheAppliedItemToTheOperatorAndKeepsItsInstanceId()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Guest));
		Assert.True(Apply(kernel, 2, 10, Guest, limbIndex: 5, expectedRevision: 1).IsAccepted);

		var removed = Remove(kernel, 3, 10, appliedOwner: Guest, limbIndex: 5, newOwner: Host, expectedRevision: 2);

		Assert.True(removed.IsAccepted, removed.Rejection?.Message);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Carried, item.Location.Kind);
		Assert.Equal(Host, item.Location.Owner);
		Assert.Equal(10ul, item.Identity.InstanceId);
		Assert.Equal(3ul, item.Revision);
	}

	[Fact]
	public void Remove_NamingAnotherLimb_IsRejectedAndLeavesTheItemApplied()
	{
		// The removal gesture names the limb it was made on, so a removal that
		// names a different one is refused rather than executed against whichever
		// limb the id happens to sit on.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Guest));
		Assert.True(Apply(kernel, 2, 10, Guest, limbIndex: 5, expectedRevision: 1).IsAccepted);

		var removed = Remove(kernel, 3, 10, appliedOwner: Guest, limbIndex: 2, newOwner: Host, expectedRevision: 2);

		Assert.False(removed.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, removed.Rejection!.Reason);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Applied, item.Location.Kind);
		Assert.Equal(5, item.Location.LimbIndex);
	}

	[Fact]
	public void Remove_OfAnItemOnAnotherBody_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Guest));
		Assert.True(Apply(kernel, 2, 10, Guest, limbIndex: 5, expectedRevision: 1).IsAccepted);

		var removed = Remove(kernel, 3, 10, appliedOwner: Host, limbIndex: 5, newOwner: Host, expectedRevision: 2);

		Assert.False(removed.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, removed.Rejection!.Reason);
	}

	[Fact]
	public void Remove_OfACarriedItem_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Guest));

		var removed = Remove(kernel, 2, 10, appliedOwner: Guest, limbIndex: 5, newOwner: Host, expectedRevision: 1);

		Assert.False(removed.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, removed.Rejection!.Reason);
		Assert.Equal(ItemLocationKind.Carried, kernel.FindItem(10)!.Value.Location.Kind);
	}

	[Fact]
	public void Remove_NamingNoOperator_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Guest));
		Assert.True(Apply(kernel, 2, 10, Guest, limbIndex: 5, expectedRevision: 1).IsAccepted);

		var removed = Remove(kernel, 3, 10, appliedOwner: Guest, limbIndex: 5, newOwner: new ActorId(0), expectedRevision: 2);

		Assert.False(removed.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, removed.Rejection!.Reason);
	}

	[Fact]
	public void ApplyThenRemove_RoundTripsTheSameIdAndTheRoundTripIsTwoRelocations()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "tourniquet", ItemLocation.Carried(Guest));
		var original = kernel.FindItem(10)!.Value;

		Assert.True(Apply(kernel, 2, 10, Guest, limbIndex: 7, expectedRevision: 1).IsAccepted);
		Assert.True(Remove(kernel, 3, 10, appliedOwner: Guest, limbIndex: 7, newOwner: Guest, expectedRevision: 2).IsAccepted);

		var returned = kernel.FindItem(10)!.Value;
		Assert.Equal(original.Identity, returned.Identity);
		Assert.Equal(ItemLocationKind.Carried, returned.Location.Kind);
		Assert.Equal(Guest, returned.Location.Owner);
		Assert.Equal(3ul, returned.Revision);
		Assert.NotEqual(ItemLocationKind.Terminal, returned.Location.Kind);
	}

	[Fact]
	public void AppliedItem_IsNotWorldRooted_SoTheLayerBoundaryKeepsIt()
	{
		// The layer took the ground and everything on it. An item held by a limb
		// travels with the body, exactly as a carried item does.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 9, "shell", ItemLocation.World(3, 4));
		Spawn(kernel, 2, 10, "splint", ItemLocation.Carried(Host));
		Assert.True(Apply(kernel, 3, 10, Host, limbIndex: 1, expectedRevision: 1).IsAccepted);

		Assert.True(ResetWorldItems(kernel, 4).IsAccepted);

		Assert.Null(kernel.FindItem(9));
		var applied = kernel.FindItem(10)!.Value;
		Assert.Equal(ItemLocationKind.Applied, applied.Location.Kind);
		Assert.Equal(1, applied.Location.LimbIndex);
	}

	[Fact]
	public void AppliedRowWithoutALimb_FailsTheKernelInvariantInsteadOfBeingStored()
	{
		// A rejection is the only way an applied row without a limb could reach the
		// state table, so the invariant is what keeps a location nobody can place
		// out of the model. The batch is built by hand because the command decides
		// against it.
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 1, 10, "splint", ItemLocation.Carried(Host));
		var batch = new CommittedBatch(
			new OperationId(9),
			2,
			Host,
			AuthorityKind.HostOnly,
			Epoch,
			[],
			[
				new ItemRelocatedEvent(
					new ItemIdentity(10, "splint"),
					1,
					2,
					ItemLocation.Carried(Host),
					ItemLocation.Applied(Host, ItemLocation.NoLimb))
			]);

		var applied = kernel.Apply(batch);

		Assert.False(applied.Success);
		Assert.Contains("applied item 10 names no limb", applied.Error);
		Assert.Equal(ItemLocationKind.Carried, kernel.FindItem(10)!.Value.Location.Kind);
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

	private static Decision Apply(GameStateKernel kernel, ulong op, ulong instanceId, ActorId owner, int limbIndex, ulong expectedRevision, float? condition = null) =>
		kernel.Execute(
			new ApplyLimbItemCommand(
				new OperationId(op),
				owner,
				Epoch,
				AuthorityKind.OwnerPredictedHostValidated,
				instanceId,
				owner,
				limbIndex,
				expectedRevision,
				condition is null ? null : new ItemData(condition.Value, false, -1, [], [])),
			new CommandContext(Epoch, owner));

	private static Decision Remove(GameStateKernel kernel, ulong op, ulong instanceId, ActorId appliedOwner, int limbIndex, ActorId newOwner, ulong expectedRevision) =>
		kernel.Execute(
			new RemoveLimbItemCommand(
				new OperationId(op),
				Host,
				Epoch,
				AuthorityKind.OwnerPredictedHostValidated,
				instanceId,
				appliedOwner,
				limbIndex,
				newOwner,
				expectedRevision),
			new CommandContext(Epoch, Host));

	private static Decision Destroy(GameStateKernel kernel, ulong op, ulong instanceId, ulong expectedRevision) =>
		kernel.Execute(
			new DestroyItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, instanceId, TerminalKind.Destroyed, expectedRevision),
			new CommandContext(Epoch, Host));

	private static Decision ResetWorldItems(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new ResetWorldItemsCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly),
			new CommandContext(Epoch, Host));
}
