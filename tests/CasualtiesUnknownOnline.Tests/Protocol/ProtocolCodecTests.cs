using System;
using CasualtiesUnknownOnline.Protocol.Codecs;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

public class ProtocolCodecTests
{
	[Fact]
	public void CommandEnvelope_RoundTripsThroughCodec()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = Header(WirePayloadType.ItemSpawnCommand),
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemSpawn,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					Location = new WireItemLocation { Kind = WireItemLocationKind.World, X = 1.5f, Y = -2.5f },
					Data = new WireItemData { Condition = 0.8f, SlotIndex = -1 },
				},
			},
		};

		var decoded = ProtocolCodec.Decode(ProtocolCodec.Encode(frame));

		Assert.Equal(EnvelopeKind.Command, decoded.Kind);
		var command = decoded.Command!;
		Assert.Equal(WirePayloadType.ItemSpawnCommand, command.Header.PayloadType);
		Assert.Equal(WireCommandKind.ItemSpawn, command.Command.Kind);
		Assert.Equal(42ul, command.Command.Identity.InstanceId);
		Assert.Equal("water", command.Command.Identity.DefinitionId);
		Assert.Equal(1.5f, command.Command.Location!.X);
	}

	[Fact]
	public void GoldenCommandFrameBytes_AreStable()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = Header(WirePayloadType.ItemSpawnCommand),
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemSpawn,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					Location = new WireItemLocation { Kind = WireItemLocationKind.World, X = 1.5f, Y = -2.5f },
					Data = new WireItemData { Condition = 0.8f, SlotIndex = -1 },
				},
			},
		};

		var hex = BitConverter.ToString(ProtocolCodec.Encode(frame)).Replace("-", "");
		Assert.Equal("0801123C0A0B0801100118E90720013801122D08011209082A120577617465721A0C0801250000C03F2D000020C022100DCDCC4C3F18FFFFFFFFFFFFFFFFFF01", hex);
	}

	[Fact]
	public void CommittedBatchEnvelope_RoundTripsEventsAndRevisions()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = Header(WirePayloadType.ItemRelocatedEvent),
				Batch = new WireCommittedBatch
				{
					OperationId = 7,
					GlobalRevision = 12,
					Actor = 1001,
					RunEpoch = 3,
					Events =
					[
						new WireEvent
						{
							Kind = WireEventKind.ItemRelocated,
							Identity = new WireItemIdentity { InstanceId = 5, DefinitionId = "bottle" },
							OldRevision = 2,
							NewRevision = 3,
							OldLocation = new WireItemLocation { Kind = WireItemLocationKind.World, X = 3f, Y = 4f },
							NewLocation = new WireItemLocation { Kind = WireItemLocationKind.Carried, Owner = 2001 },
						},
					],
				},
			},
		};

		var decoded = ProtocolCodec.Decode(ProtocolCodec.Encode(frame));

		Assert.Equal(EnvelopeKind.CommittedBatch, decoded.Kind);
		var batch = decoded.CommittedBatch!.Batch;
		Assert.Equal(7ul, batch.OperationId);
		Assert.Equal(12ul, batch.GlobalRevision);
		Assert.Single(batch.Events);
		Assert.Equal(WireEventKind.ItemRelocated, batch.Events[0].Kind);
		Assert.Equal(2001ul, batch.Events[0].NewLocation!.Owner);
	}

	[Fact]
	public void CheckpointEnvelope_RoundTripsChunk()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Checkpoint,
			Checkpoint = new CheckpointEnvelope
			{
				Header = Header(WirePayloadType.CheckpointChunk),
				Checkpoint = new WireCheckpoint
				{
					ChunkIndex = 0,
					ChunkCount = 1,
					RunEpoch = 9,
					GlobalRevision = 40,
					Items =
					[
						new WireItem
						{
							Identity = new WireItemIdentity { InstanceId = 99, DefinitionId = "rock" },
							Revision = 4,
							Location = new WireItemLocation { Kind = WireItemLocationKind.World, X = 8f, Y = 9f },
							Data = new WireItemData { Condition = 1f },
						},
					],
				},
			},
		};

		var decoded = ProtocolCodec.Decode(ProtocolCodec.Encode(frame));

		Assert.Equal(EnvelopeKind.Checkpoint, decoded.Kind);
		var checkpoint = decoded.Checkpoint!.Checkpoint;
		Assert.Equal(1, checkpoint.ChunkCount);
		Assert.Single(checkpoint.Items);
		Assert.Equal(99ul, checkpoint.Items[0].Identity.InstanceId);
	}

	[Fact]
	public void StateStreamEnvelope_RoundTripsConvergentFields()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = Header(WirePayloadType.StateStream),
				Stream = new WireStateStream
				{
					EntityId = 123,
					BaseGlobalRevision = 50,
					Fields = [new WireStreamField { Name = "x", Kind = 3, FloatValue = 1.25f }],
				},
			},
		};

		var decoded = ProtocolCodec.Decode(ProtocolCodec.Encode(frame));

		Assert.Equal(EnvelopeKind.StateStream, decoded.Kind);
		Assert.Single(decoded.StateStream!.Stream.Fields);
		Assert.Equal(1.25f, decoded.StateStream.Stream.Fields[0].FloatValue);
	}

	[Fact]
	public void StateStreamEnvelope_RoundTripsPlayerAndEnemyEntityStates()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = Header(WirePayloadType.PlayerStateStream),
				Stream = new WireStateStream
				{
					Seq = 9,
					PlayerStates =
					[
						new WirePlayerStreamState
						{
							EntityId = new WireEntityId { Epoch = 1, Counter = 2, Generation = 0 },
							Position = new WireVector2 { X = 3f, Y = 4f },
							LookOverridePos = new WireVector2 { X = 5f, Y = 6f },
							NapVariant = 1,
							DogShakeIntensity = 0.25f,
							LimbPoses =
							[
								new WirePlayerLimbPose
								{
									Index = 2,
									WorldPosition = new WireVector2 { X = 1.5f, Y = -0.5f },
									RotationZ = 37f,
								},
							],
						},
					],
					EnemyStates =
					[
						new WireEnemyStreamState
						{
							EntityId = new WireEntityId { Epoch = 1, Counter = 7, Generation = 0 },
							Position = new WireVector2 { X = 8f, Y = 9f },
							Health = 42f,
							PresentationFlags = 1u,
						},
					],
				},
			},
		};

		var decoded = ProtocolCodec.Decode(ProtocolCodec.Encode(frame));

		var stream = decoded.StateStream!.Stream;
		Assert.Equal(9u, stream.Seq);
		var player = Assert.Single(stream.PlayerStates);
		Assert.Equal(3f, player.Position.X);
		Assert.Equal(1, player.NapVariant);
		Assert.Equal(0.25f, player.DogShakeIntensity);
		var limbPose = Assert.Single(player.LimbPoses!);
		Assert.Equal(2, limbPose.Index);
		Assert.Equal(1.5f, limbPose.WorldPosition.X);
		Assert.Equal(-0.5f, limbPose.WorldPosition.Y);
		Assert.Equal(37f, limbPose.RotationZ);
		var enemy = Assert.Single(stream.EnemyStates);
		Assert.Equal(42f, enemy.Health);
		Assert.Equal(1u, enemy.PresentationFlags);
	}

	[Fact]
	public void Decode_EmptyFrame_Throws() =>
		Assert.Throws<ArgumentException>(() => ProtocolCodec.Decode([]));

	[Fact]
	public void EnvelopeVersion_IsCurrentStableConstant()
	{
		Assert.Equal(1, ProtocolConstants.EnvelopeVersion);
		Assert.Equal(2, ProtocolConstants.CheckpointSchemaVersion);
		Assert.Equal(256, ProtocolConstants.CheckpointChunkItemCount);
	}

	private static EnvelopeHeader Header(WirePayloadType type) => new()
	{
		ProtocolVersion = ProtocolConstants.EnvelopeVersion,
		RunEpoch = 1,
		SenderId = 1001,
		MessageId = 1,
		OperationId = 0,
		BaseGlobalRevision = 0,
		PayloadType = type,
	};
}
