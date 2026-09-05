using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class KernelWireMapperTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void BatchRoundTrip_PreservesItemEventAndRevisions()
	{
		var source = new GameStateKernel(Epoch);
		var decision = source.Execute(
			new SpawnItemCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(42, "water"),
				ItemLocation.World(1f, 2f),
				0,
				new ItemData(0.8f, false, -1, [], [])),
			new CommandContext(Epoch, Host));
		Assert.True(decision.IsAccepted);

		var wire = KernelWireMapper.ToWireBatch(decision.Batch!);
		var restored = KernelWireMapper.FromWireBatch(wire, Epoch);

		Assert.Equal(decision.Batch!.OperationId.Value, restored.OperationId.Value);
		Assert.Equal(decision.Batch!.GlobalRevision, restored.GlobalRevision);
		Assert.Single(restored.Events);
		Assert.IsType<ItemSpawnedEvent>(restored.Events[0]);
		var spawned = Assert.IsType<ItemSpawnedEvent>(restored.Events[0]);
		Assert.Equal(42ul, spawned.Identity.InstanceId);
		Assert.Equal(0.8f, spawned.Data!.Value.Condition);
	}

	[Fact]
	public void BatchRoundTrip_PreservesSpawnPresentationFields()
	{
		var source = new GameStateKernel(Epoch);
		var decision = source.Execute(
			new SpawnItemCommand(
				new OperationId(7),
				Host,
				Epoch,
				AuthorityKind.OwnerPredictedHostValidated,
				new ItemIdentity(77, "metalscrap"),
				ItemLocation.World(10f, 20f),
				0,
				new ItemData(0.9f, false, -1, [], []),
				VelocityX: 3.5f,
				VelocityY: -2f,
				Rotation: 45f,
				FreshItemDrop: true,
				AngularVelocity: 8f),
			new CommandContext(Epoch, Host));
		Assert.True(decision.IsAccepted);

		var wire = KernelWireMapper.ToWireBatch(decision.Batch!);
		var restored = KernelWireMapper.FromWireBatch(wire, Epoch);

		var spawned = Assert.IsType<ItemSpawnedEvent>(Assert.Single(restored.Events));
		Assert.Equal(3.5f, spawned.VelocityX);
		Assert.Equal(-2f, spawned.VelocityY);
		Assert.Equal(45f, spawned.Rotation);
		Assert.True(spawned.FreshItemDrop);
		Assert.Equal(8f, spawned.AngularVelocity);
	}

	[Fact]
	public void CheckpointSplitAndAssemble_RoundTripsItemsAndRevision()
	{
		var source = new GameStateKernel(Epoch);
		Assert.True(source.Execute(
			new SpawnItemCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(42, "water"),
				ItemLocation.World(1f, 2f),
				0,
				new ItemData(0.8f, false, -1, [], [])),
			new CommandContext(Epoch, Host)).IsAccepted);

		var checkpoint = source.CreateCheckpoint();
		var chunks = WireCheckpointAssembler.Split(checkpoint);
		var restoredCheckpoint = WireCheckpointAssembler.Assemble(chunks);

		Assert.Equal(checkpoint.RunEpoch.Value, restoredCheckpoint.RunEpoch.Value);
		Assert.Equal(checkpoint.GlobalRevision, restoredCheckpoint.GlobalRevision);
		var item = Assert.Single(restoredCheckpoint.Items);
		Assert.Equal(42ul, item.Identity.InstanceId);
		Assert.Equal("water", item.Identity.DefinitionId);
		Assert.Equal(0.8f, item.Data.Condition);
	}

	[Fact]
	public void CheckpointSplit_CompressesRepeatedDefinitionIdsIntoTable()
	{
		var source = new GameStateKernel(Epoch);
		for (var i = 0; i < 3; i++)
		{
			Assert.True(source.Execute(
				new SpawnItemCommand(
					new OperationId((ulong)(i + 1)),
					Host,
					Epoch,
					AuthorityKind.HostOnly,
					new ItemIdentity((ulong)(i + 1), "shell"),
					ItemLocation.World(i, 0f),
					0,
					new ItemData(1f, false, -1, [], [])),
				new CommandContext(Epoch, Host)).IsAccepted);
		}

		var checkpoint = source.CreateCheckpoint();
		var chunks = WireCheckpointAssembler.Split(checkpoint);

		Assert.Equal(["shell"], chunks[0].ItemDefinitionTable);
		Assert.All(chunks.SelectMany(c => c.Items), item =>
		{
			Assert.Equal("", item.Identity.DefinitionId);
			Assert.Equal(1, item.Identity.DefinitionIndex);
		});

		var restored = WireCheckpointAssembler.Assemble(chunks);
		Assert.Equal(3, restored.Items.Count);
		Assert.All(restored.Items, item => Assert.Equal("shell", item.Identity.DefinitionId));
	}

	[Fact]
	public void CheckpointSplit_UniqueDefinitionIdsKeepDirectStrings()
	{
		var source = new GameStateKernel(Epoch);
		var ids = new[] { "shell", "stone", "wood" };
		for (var i = 0; i < ids.Length; i++)
		{
			Assert.True(source.Execute(
				new SpawnItemCommand(
					new OperationId((ulong)(i + 1)),
					Host,
					Epoch,
					AuthorityKind.HostOnly,
					new ItemIdentity((ulong)(i + 1), ids[i]),
					ItemLocation.World(i, 0f),
					0,
					new ItemData(1f, false, -1, [], [])),
				new CommandContext(Epoch, Host)).IsAccepted);
		}

		var checkpoint = source.CreateCheckpoint();
		var chunks = WireCheckpointAssembler.Split(checkpoint);

		Assert.Empty(chunks[0].ItemDefinitionTable);
		Assert.All(chunks.SelectMany(c => c.Items), item =>
		{
			Assert.Equal(0, item.Identity.DefinitionIndex);
			Assert.Contains(item.Identity.DefinitionId, ids);
		});

		var restored = WireCheckpointAssembler.Assemble(chunks);
		Assert.Equal(3, restored.Items.Count);
		Assert.Equal(["shell", "stone", "wood"], restored.Items.Select(i => i.Identity.DefinitionId).ToArray());
	}

	[Fact]
	public void CheckpointAssemble_InvalidDefinitionIndex_Throws()
	{
		var source = new GameStateKernel(Epoch);
		Assert.True(source.Execute(
			new SpawnItemCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(42, "shell"),
				ItemLocation.World(0f, 0f),
				0,
				new ItemData(1f, false, -1, [], [])),
			new CommandContext(Epoch, Host)).IsAccepted);

		var chunks = WireCheckpointAssembler.Split(source.CreateCheckpoint());
		chunks[0].Items[0].Identity.DefinitionIndex = 5;
		chunks[0].ItemDefinitionTable = [];

		Assert.Throws<InvalidOperationException>(() => WireCheckpointAssembler.Assemble(chunks));
	}

	[Fact]
	public void CheckpointSplitAndAssemble_RoundTripsRandomStreams()
	{
		var checkpoint = new GameCheckpoint(
			Epoch,
			1,
			[],
			[new RandomStreamState("gen", "state-xyz", [5, 6, 7])]);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(checkpoint));

		var stream = Assert.Single(restored.RandomStreams!);
		Assert.Equal("gen", stream.Name);
		Assert.Equal("state-xyz", stream.State);
		Assert.Equal([5ul, 6ul, 7ul], stream.DecidedValues);
	}

	[Fact]
	public void WireCommand_MapToGameCommand()
	{
		var header = new EnvelopeHeader
		{
			ProtocolVersion = 1,
			RunEpoch = Epoch.Value,
			SenderId = 2001,
			OperationId = 9,
		};
		var command = KernelWireMapper.FromWireCommand(new WireCommand
		{
			Kind = WireCommandKind.ItemPickup,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
			NewOwner = 3001,
			ExpectedRevision = 3,
		}, header);

		var pickup = Assert.IsType<PickUpItemCommand>(command);
		Assert.Equal(42ul, pickup.InstanceId);
		Assert.Equal(3001ul, pickup.NewOwner.Value);
		Assert.Equal(3ul, pickup.ExpectedRevision);
	}
}
