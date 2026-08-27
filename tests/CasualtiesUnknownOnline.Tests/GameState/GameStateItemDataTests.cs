using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class GameStateItemDataTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId G1 = new(2001);
	private static readonly ActorId G2 = new(3001);

	private static readonly ItemData SampleData = new(
		0.75f,
		true,
		2,
		[new ItemLiquidStack("water", 0.5f)],
		[
			new ItemComponentState("GunScript",
			[
				new ItemComponentField("roundsInMag", ItemComponentFieldKind.Int, 0f, 7, false, "", [])
			])
		]);

	[Fact]
	public void SpawnWithData_StoresPayloadAndRevision()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = kernel.Execute(
			new SpawnItemCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly,
				new ItemIdentity(10, "rifle"), ItemLocation.World(1, 2), 0, SampleData),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var item = kernel.FindItem(10)!.Value;
		Assert.True(item.Data.SemanticallyEquals(SampleData));
		Assert.Equal(1ul, item.Revision);
	}

	[Fact]
	public void UpdateState_PreservesLocationAndAdvancesRevision()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 10, ItemLocation.World(1, 2), SampleData);

		var updated = new ItemData(1f, false, 3, [], []);
		var decision = kernel.Execute(
			new UpdateItemStateCommand(new OperationId(2), Host, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				10, updated, 1),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(2ul, item.Revision);
		Assert.Equal(ItemLocationKind.World, item.Location.Kind);
		Assert.True(item.Data.SemanticallyEquals(updated));
	}

	[Fact]
	public void PickupAndDrop_PreservePayload()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 10, ItemLocation.World(1, 2), SampleData);

		Assert.True(kernel.Execute(
			new PickUpItemCommand(new OperationId(2), G1, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				10, G1, 1),
			new CommandContext(Epoch, G1)).IsAccepted);

		Assert.True(kernel.Execute(
			new DropItemCommand(new OperationId(3), G1, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				10, ItemLocation.World(5, 6), 2, SampleData),
			new CommandContext(Epoch, G1)).IsAccepted);

		Assert.True(kernel.FindItem(10)!.Value.Data.SemanticallyEquals(SampleData));
	}

	[Fact]
	public void Transfer_ChangesCarriedOwner()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 10, ItemLocation.World(1, 2), SampleData);
		Pickup(kernel, 10, G1, 1);

		var decision = kernel.Execute(
			new TransferItemCommand(new OperationId(3), G1, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				10, G2, null, 2),
			new CommandContext(Epoch, G1));

		Assert.True(decision.IsAccepted);
		var item = kernel.FindItem(10)!.Value;
		Assert.Equal(G2, item.Location.Owner);
		Assert.Equal(3ul, item.Revision);
	}

	[Fact]
	public void CheckpointRoundTrip_PreservesPayload()
	{
		var kernel = new GameStateKernel(Epoch);
		Spawn(kernel, 10, ItemLocation.World(1, 2), SampleData);
		Pickup(kernel, 10, G1, 1);
		var checkpoint = kernel.CreateCheckpoint();

		var restored = new GameStateKernel(new RunEpoch(99));
		Assert.True(restored.Restore(checkpoint).Success);
		var item = restored.FindItem(10)!.Value;
		Assert.True(item.Data.SemanticallyEquals(SampleData));
		Assert.Equal(G1, item.Location.Owner);
		Assert.Equal(2ul, item.Revision);
	}

	[Fact]
	public void ContainedSpawn_RequiresExistingParent()
	{
		var kernel = new GameStateKernel(Epoch);
		var missing = kernel.Execute(
			new SpawnItemCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly,
				new ItemIdentity(11, "water"), ItemLocation.Contained(Host, 99), 0, SampleData),
			new CommandContext(Epoch, Host));

		Assert.False(missing.IsAccepted);
		Assert.Equal(RejectionReason.UnknownAggregate, missing.Rejection!.Reason);

		Spawn(kernel, 99, ItemLocation.World(3, 4), new ItemData(1f, false, -1, [], []));
		var child = kernel.Execute(
			new SpawnItemCommand(new OperationId(2), Host, Epoch, AuthorityKind.HostOnly,
				new ItemIdentity(11, "water"), ItemLocation.Contained(Host, 99), 0, SampleData),
			new CommandContext(Epoch, Host));

		Assert.True(child.IsAccepted);
		Assert.Equal(ItemLocationKind.Contained, kernel.FindItem(11)!.Value.Location.Kind);
		Assert.Equal(99ul, kernel.FindItem(11)!.Value.Location.ParentItemId);
	}

	private static void Spawn(GameStateKernel kernel, ulong id, ItemLocation location, ItemData data)
	{
		var decision = kernel.Execute(
			new SpawnItemCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly,
				new ItemIdentity(id, "test_item"), location, 0, data),
			new CommandContext(Epoch, Host));
		Assert.True(decision.IsAccepted);
	}

	private static void Pickup(GameStateKernel kernel, ulong id, ActorId owner, ulong expectedRevision)
	{
		var decision = kernel.Execute(
			new PickUpItemCommand(new OperationId(2), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated,
				id, owner, expectedRevision),
			new CommandContext(Epoch, owner));
		Assert.True(decision.IsAccepted);
	}
}
