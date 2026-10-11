using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using Xunit;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class KernelWireMapperTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId Guest = new(2001);

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
	public void CheckpointAssemble_ChunksDisagreeingOnEpoch_Throws()
	{
		// The set-level guard the receiver's own per-set keying cannot replace: a
		// caller holding chunks of two sets must never assemble them into one
		// checkpoint (the receiver refuses the older set before it is ever mixed in,
		// but this is the assembler's own contract).
		var source = new GameStateKernel(Epoch);
		var live = WireCheckpointAssembler.Split(source.CreateCheckpoint());
		var previousRun = WireCheckpointAssembler.Split(source.CreateCheckpoint());
		previousRun[0].RunEpoch = Epoch.Value + 1;

		Assert.Throws<InvalidOperationException>(() => WireCheckpointAssembler.Assemble([live[0], previousRun[0]]));
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

	[Fact]
	public void AppliedLocation_RoundTripsThroughTheWireWithItsOwnerAndLimb()
	{
		var wire = KernelWireMapper.ToWireLocation(ItemLocation.Applied(Host, 3));

		Assert.Equal(WireItemLocationKind.Applied, wire.Kind);
		Assert.Equal(Host.Value, wire.Owner);
		Assert.Equal(3, wire.LimbIndex);

		Assert.Equal(ItemLocation.Applied(Host, 3), KernelWireMapper.FromWireLocation(wire));
	}

	[Fact]
	public void AppliedLocation_OnTheFirstLimb_KeepsLimbZeroDistinctFromNoLimb()
	{
		// Limb 0 is the head, so "limb 0" and "no limb recorded" must not be the
		// same wire value: the field is nullable for exactly this.
		var wire = KernelWireMapper.ToWireLocation(ItemLocation.Applied(Host, 0));

		Assert.Equal(0, wire.LimbIndex);
		Assert.Equal(0, KernelWireMapper.FromWireLocation(wire).LimbIndex);
	}

	[Fact]
	public void LocationOfAnotherFamily_CarriesNoLimbOnTheWire()
	{
		// Every other family leaves the field absent, so the batch path this
		// message rides every peer's copy of grows by nothing for them.
		Assert.Null(KernelWireMapper.ToWireLocation(ItemLocation.World(1f, 2f)).LimbIndex);
		Assert.Null(KernelWireMapper.ToWireLocation(ItemLocation.World(1f, 2f, 9)).LimbIndex);
		Assert.Null(KernelWireMapper.ToWireLocation(ItemLocation.Carried(Host)).LimbIndex);
		Assert.Null(KernelWireMapper.ToWireLocation(ItemLocation.Contained(Host, 9)).LimbIndex);
		Assert.Null(KernelWireMapper.ToWireLocation(ItemLocation.Terminal()).LimbIndex);
	}

	[Fact]
	public void AppliedWireLocationWithoutALimb_IsRefusedInsteadOfReadAsTheHead()
	{
		var wire = new WireItemLocation { Kind = WireItemLocationKind.Applied, Owner = Host.Value };

		Assert.Throws<ArgumentOutOfRangeException>(() => KernelWireMapper.FromWireLocation(wire));
	}

	[Fact]
	public void UnknownWireLocationKind_IsStillRefused()
	{
		var wire = new WireItemLocation { Kind = (WireItemLocationKind)99, Owner = Host.Value, LimbIndex = 1 };

		Assert.Throws<ArgumentOutOfRangeException>(() => KernelWireMapper.FromWireLocation(wire));
	}

	[Fact]
	public void BatchRoundTrip_PreservesAnAppliedItemLocation()
	{
		var source = new GameStateKernel(Epoch);
		Assert.True(source.Execute(
			new SpawnItemCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(42, "splint"),
				ItemLocation.Carried(Host),
				0,
				new ItemData(0.75f, false, -1, [], [])),
			new CommandContext(Epoch, Host)).IsAccepted);
		var applied = source.Execute(
			new ApplyLimbItemCommand(new OperationId(2), Host, Epoch, AuthorityKind.OwnerPredictedHostValidated, 42, Host, 6, 1),
			new CommandContext(Epoch, Host));
		Assert.True(applied.IsAccepted, applied.Rejection?.Message);

		var wire = KernelWireMapper.ToWireBatch(applied.Batch!);
		var restored = KernelWireMapper.FromWireBatch(wire, Epoch);

		var relocated = Assert.IsType<ItemRelocatedEvent>(Assert.Single(restored.Events));
		Assert.Equal(ItemLocation.Carried(Host), relocated.OldLocation);
		Assert.Equal(ItemLocation.Applied(Host, 6), relocated.NewLocation);
	}

	[Fact]
	public void AWireSpawnReportThatNamesALimb_ReachesTheKernelsRefusal()
	{
		// The spawn refusal is reachable from the WIRE and not only from a hand-built
		// command: the spawn report carries its OWN location, which is the shape a
		// forged report takes. Without the refusal the kernel would accept a row for an
		// item that exists only as another body's limb component — and only that body's
		// owner may ever report its end.
		var header = new EnvelopeHeader
		{
			ProtocolVersion = 1,
			RunEpoch = Epoch.Value,
			SenderId = Guest.Value,
			OperationId = 9,
		};
		var command = KernelWireMapper.FromWireCommand(
			new WireCommand
			{
				Kind = WireCommandKind.ItemSpawn,
				Identity = new WireItemIdentity { InstanceId = 10, DefinitionId = "splint" },
				Location = new WireItemLocation { Kind = WireItemLocationKind.Applied, Owner = Guest.Value, LimbIndex = 3 },
			},
			header);
		var kernel = new GameStateKernel(Epoch);

		var decision = kernel.Execute(command, new CommandContext(Epoch, Guest));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, decision.Rejection!.Reason);
		Assert.Null(kernel.FindItem(10));
	}
}
