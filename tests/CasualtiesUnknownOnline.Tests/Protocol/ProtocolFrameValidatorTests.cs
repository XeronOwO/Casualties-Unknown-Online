using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

public class ProtocolFrameValidatorTests
{
	[Fact]
	public void ValidCommandFrame_PassesValidation()
	{
		var frame = CommandFrame(WirePayloadType.ItemSpawnCommand, WireCommandKind.ItemSpawn);

		Assert.True(ProtocolFrameValidator.TryValidate(frame, 1001, out var error), error);
	}

	[Fact]
	public void ValidCommittedBatchFrame_PassesValidation()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = Header(WirePayloadType.CommittedBatch),
				Batch = new WireCommittedBatch
				{
					OperationId = 7,
					GlobalRevision = 1,
					RunEpoch = 1,
					Events = [new WireEvent { Kind = WireEventKind.ItemSpawned }],
				},
			},
		};

		Assert.True(ProtocolFrameValidator.TryValidate(frame, 1001, out var error), error);
	}

	[Fact]
	public void ValidCheckpointFrame_PassesValidation()
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
					ChunkCount = 2,
					RunEpoch = 1,
					GlobalRevision = 10,
				},
			},
		};

		Assert.True(ProtocolFrameValidator.TryValidate(frame, 1001, out var error), error);
	}

	[Fact]
	public void ValidStateStreamFrame_PassesValidation()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = Header(WirePayloadType.ItemSnapshotStream),
				Stream = new WireStateStream
				{
					ItemStates = [new WireWorldItemState()],
				},
			},
		};

		Assert.True(ProtocolFrameValidator.TryValidate(frame, 1001, out var error), error);
	}

	[Fact]
	public void PresentationPayload_IsNonFatal()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = Header(WirePayloadType.PresentationEffect),
				Stream = new WireStateStream(),
			},
		};

		Assert.True(ProtocolFrameValidator.TryValidate(frame, 1001, out var error), error);
	}

	[Fact]
	public void MultipleEnvelopes_Fail()
	{
		var frame = CommandFrame(WirePayloadType.ItemSpawnCommand, WireCommandKind.ItemSpawn);
		frame.CommittedBatch = new CommittedBatchEnvelope
		{
			Header = Header(WirePayloadType.CommittedBatch),
			Batch = new WireCommittedBatch(),
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void MissingEnvelope_Fail()
	{
		var frame = new ProtocolFrame { Kind = EnvelopeKind.Command };

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void KindEnvelopeMismatch_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = Header(WirePayloadType.CommittedBatch),
				Batch = new WireCommittedBatch(),
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void UnknownFrameKind_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = (EnvelopeKind)99,
			Command = new CommandEnvelope
			{
				Header = Header(WirePayloadType.ItemSpawnCommand),
				Command = new WireCommand { Kind = WireCommandKind.ItemSpawn },
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CommandPayloadKindMismatch_Fail()
	{
		var frame = CommandFrame(WirePayloadType.ItemSpawnCommand, WireCommandKind.ItemPickup);

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CommandWithNonCommandPayload_Fail()
	{
		var frame = CommandFrame(WirePayloadType.CommittedBatch, WireCommandKind.ItemSpawn);

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CommittedBatchWithWrongPayload_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = Header(WirePayloadType.CheckpointChunk),
				Batch = new WireCommittedBatch(),
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CheckpointWithWrongPayload_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Checkpoint,
			Checkpoint = new CheckpointEnvelope
			{
				Header = Header(WirePayloadType.CommittedBatch),
				Checkpoint = new WireCheckpoint { ChunkIndex = 0, ChunkCount = 1 },
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void StateStreamWithWrongPayload_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = Header(WirePayloadType.CommittedBatch),
				Stream = new WireStateStream(),
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void ForgedSenderId_Fail()
	{
		var frame = CommandFrame(WirePayloadType.ItemSpawnCommand, WireCommandKind.ItemSpawn);

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 9999, out _));
	}

	[Fact]
	public void UnsupportedProtocolVersion_Fail()
	{
		var frame = CommandFrame(WirePayloadType.ItemSpawnCommand, WireCommandKind.ItemSpawn);
		frame.Command!.Header.ProtocolVersion = 999;

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void UnknownCriticalPayload_Fail()
	{
		var frame = CommandFrame((WirePayloadType)500, WireCommandKind.ItemSpawn);

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CheckpointInvalidChunkIndex_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Checkpoint,
			Checkpoint = new CheckpointEnvelope
			{
				Header = Header(WirePayloadType.CheckpointChunk),
				Checkpoint = new WireCheckpoint { ChunkIndex = 2, ChunkCount = 2 },
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CheckpointInvalidChunkCount_Fail()
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Checkpoint,
			Checkpoint = new CheckpointEnvelope
			{
				Header = Header(WirePayloadType.CheckpointChunk),
				Checkpoint = new WireCheckpoint { ChunkIndex = 0, ChunkCount = 0 },
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CheckpointOversizedChunk_Fail()
	{
		var items = new List<WireItem>(ProtocolConstants.CheckpointChunkItemCount + 1);
		for (var i = 0; i <= ProtocolConstants.CheckpointChunkItemCount; i++)
		{
			items.Add(new WireItem());
		}

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
					Items = items,
				},
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void StateStreamOversizedCollection_Fail()
	{
		var items = new List<WireWorldItemState>(ProtocolConstants.MaxStateStreamCollectionSize + 1);
		for (var i = 0; i <= ProtocolConstants.MaxStateStreamCollectionSize; i++)
		{
			items.Add(new WireWorldItemState());
		}

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = Header(WirePayloadType.ItemSnapshotStream),
				Stream = new WireStateStream { ItemStates = items },
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	[Fact]
	public void CommittedBatchOversizedEvents_Fail()
	{
		var events = new List<WireEvent>(ProtocolConstants.MaxCommittedBatchEvents + 1);
		for (var i = 0; i <= ProtocolConstants.MaxCommittedBatchEvents; i++)
		{
			events.Add(new WireEvent());
		}

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = Header(WirePayloadType.CommittedBatch),
				Batch = new WireCommittedBatch { Events = events },
			},
		};

		Assert.False(ProtocolFrameValidator.TryValidate(frame, 1001, out _));
	}

	private static ProtocolFrame CommandFrame(WirePayloadType payloadType, WireCommandKind commandKind) =>
		new()
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = Header(payloadType),
				Command = new WireCommand
				{
					Kind = commandKind,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
				},
			},
		};

	private static EnvelopeHeader Header(WirePayloadType payloadType) => new()
	{
		ProtocolVersion = ProtocolConstants.EnvelopeVersion,
		RunEpoch = 1,
		SenderId = 1001,
		MessageId = 1,
		OperationId = 0,
		BaseGlobalRevision = 0,
		PayloadType = payloadType,
	};
}
