using System;
using CasualtiesUnknownOnline.Protocol.Versioning;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Pure structural validation for the Phase C kernel frame envelope. The frame
/// must carry exactly one envelope, the envelope must match <see cref="ProtocolFrame.Kind"/>,
/// and the header payload type must be a valid discriminator for that envelope
/// family. Unknown presentation payloads are intentionally non-fatal so future
/// optional effects can ride the protocol without requiring a new critical
/// version bump.
/// </summary>
public static class ProtocolFrameValidator
{
	/// <summary>
	/// Validates a received frame and its header consistency.
	/// </summary>
	/// <param name="frame">The decoded frame.</param>
	/// <param name="expectedSender">
	/// When non-null, the transport-level sender id. The envelope header's
	/// <see cref="EnvelopeHeader.SenderId"/> must match it in the current
	/// direct-peer protocol.
	/// </param>
	/// <param name="error">A human-readable validation failure reason.</param>
	public static bool TryValidate(ProtocolFrame? frame, ulong? expectedSender, out string error)
	{
		if (frame is null)
		{
			error = "frame is null";
			return false;
		}

		if (!Enum.IsDefined(typeof(EnvelopeKind), frame.Kind))
		{
			error = $"unknown frame kind {frame.Kind}";
			return false;
		}

		var envelopeCount = (frame.Command is not null ? 1 : 0)
			+ (frame.CommittedBatch is not null ? 1 : 0)
			+ (frame.Checkpoint is not null ? 1 : 0)
			+ (frame.StateStream is not null ? 1 : 0);
		if (envelopeCount != 1)
		{
			error = $"frame must contain exactly one envelope; found {envelopeCount}";
			return false;
		}

		var header = frame.Kind switch
		{
			EnvelopeKind.Command when frame.Command is not null => frame.Command.Header,
			EnvelopeKind.CommittedBatch when frame.CommittedBatch is not null => frame.CommittedBatch.Header,
			EnvelopeKind.Checkpoint when frame.Checkpoint is not null => frame.Checkpoint.Header,
			EnvelopeKind.StateStream when frame.StateStream is not null => frame.StateStream.Header,
			_ => null,
		};
		if (header is null)
		{
			error = $"envelope for kind {frame.Kind} is missing a header";
			return false;
		}

		if (header.ProtocolVersion != ProtocolConstants.EnvelopeVersion)
		{
			error = $"unsupported protocol version {header.ProtocolVersion}; expected {ProtocolConstants.EnvelopeVersion}";
			return false;
		}

		if (expectedSender is not null && header.SenderId != expectedSender.Value)
		{
			error = $"header sender {header.SenderId} does not match transport sender {expectedSender.Value}";
			return false;
		}

		var payloadIsKnown = Enum.IsDefined(typeof(WirePayloadType), header.PayloadType);
		if (!payloadIsKnown && (int)header.PayloadType < ProtocolConstants.PresentationPayloadStart)
		{
			error = $"unknown critical payload type {(int)header.PayloadType}";
			return false;
		}

		// Unknown and defined presentation payloads are deliberately allowed;
		// the downstream presentation policy decides whether to consume them.
		if ((int)header.PayloadType >= ProtocolConstants.PresentationPayloadStart)
		{
			error = string.Empty;
			return ValidateCollectionBounds(frame, out error);
		}

		if (!ValidatePayloadDiscriminator(frame, header, out error))
		{
			return false;
		}

		return ValidateCollectionBounds(frame, out error);
	}

	private static bool ValidatePayloadDiscriminator(ProtocolFrame frame, EnvelopeHeader header, out string error)
	{
		switch (frame.Kind)
		{
			case EnvelopeKind.Command:
				{
					if (frame.Command!.Command is null)
					{
						error = "command envelope is missing its command payload";
						return false;
					}

					var expectedKind = GetExpectedCommandKind(header.PayloadType);
					if (expectedKind is null)
					{
						error = $"payload type {header.PayloadType} is not valid for a command envelope";
						return false;
					}

					if (frame.Command!.Command.Kind != expectedKind.Value)
					{
						error = $"command kind {frame.Command.Command.Kind} does not match payload type {header.PayloadType}";
						return false;
					}

					break;
				}

			case EnvelopeKind.CommittedBatch:
				if (header.PayloadType != WirePayloadType.CommittedBatch)
				{
					error = $"payload type {header.PayloadType} is not valid for a committed-batch envelope";
					return false;
				}

				break;

			case EnvelopeKind.Checkpoint:
				if (header.PayloadType != WirePayloadType.CheckpointChunk)
				{
					error = $"payload type {header.PayloadType} is not valid for a checkpoint envelope";
					return false;
				}

				if (frame.Checkpoint!.Checkpoint is null)
				{
					error = "checkpoint envelope is missing its checkpoint payload";
					return false;
				}

				if (!ValidateCheckpointMetadata(frame.Checkpoint.Checkpoint, out error))
				{
					return false;
				}

				break;

			case EnvelopeKind.StateStream:
				if (!IsStateStreamPayloadType(header.PayloadType))
				{
					error = $"payload type {header.PayloadType} is not valid for a state-stream envelope";
					return false;
				}

				break;

			default:
				error = $"unknown frame kind {frame.Kind}";
				return false;
		}

		error = string.Empty;
		return true;
	}

	private static bool ValidateCollectionBounds(ProtocolFrame frame, out string error)
	{
		if (frame.Command is not null)
		{
			if (frame.Command.Command is null)
			{
				error = "command envelope is missing its command payload";
				return false;
			}

			var containerChildrenCount = frame.Command.Command.ContainerChildren?.Count ?? 0;
			if (containerChildrenCount > ProtocolConstants.MaxCommandContainerChildren)
			{
				error = $"command container children count {containerChildrenCount} exceeds limit {ProtocolConstants.MaxCommandContainerChildren}";
				return false;
			}
		}

		if (frame.CommittedBatch is not null)
		{
			if (frame.CommittedBatch.Batch is null)
			{
				error = "committed-batch envelope is missing its batch payload";
				return false;
			}

			var batch = frame.CommittedBatch.Batch;
			var eventCount = batch.Events?.Count ?? 0;
			if (eventCount > ProtocolConstants.MaxCommittedBatchEvents)
			{
				error = $"committed batch event count {eventCount} exceeds limit {ProtocolConstants.MaxCommittedBatchEvents}";
				return false;
			}

			var preconditionCount = batch.Preconditions?.Count ?? 0;
			if (preconditionCount > ProtocolConstants.MaxCommittedBatchEvents)
			{
				error = $"committed batch precondition count {preconditionCount} exceeds limit {ProtocolConstants.MaxCommittedBatchEvents}";
				return false;
			}
		}

		if (frame.StateStream is not null)
		{
			if (frame.StateStream.Stream is null)
			{
				error = "state-stream envelope is missing its stream payload";
				return false;
			}

			var stream = frame.StateStream.Stream;
			var fieldCount = stream.Fields?.Count ?? 0;
			if (fieldCount > ProtocolConstants.MaxStateStreamCollectionSize)
			{
				error = $"state-stream fields count {fieldCount} exceeds limit {ProtocolConstants.MaxStateStreamCollectionSize}";
				return false;
			}

			var itemMoveCount = stream.ItemMoves?.Count ?? 0;
			if (itemMoveCount > ProtocolConstants.MaxStateStreamCollectionSize)
			{
				error = $"state-stream item-move count {itemMoveCount} exceeds limit {ProtocolConstants.MaxStateStreamCollectionSize}";
				return false;
			}

			var itemStateCount = stream.ItemStates?.Count ?? 0;
			if (itemStateCount > ProtocolConstants.MaxStateStreamCollectionSize)
			{
				error = $"state-stream item-state count {itemStateCount} exceeds limit {ProtocolConstants.MaxStateStreamCollectionSize}";
				return false;
			}

			var playerStateCount = stream.PlayerStates?.Count ?? 0;
			if (playerStateCount > ProtocolConstants.MaxStateStreamCollectionSize)
			{
				error = $"state-stream player-state count {playerStateCount} exceeds limit {ProtocolConstants.MaxStateStreamCollectionSize}";
				return false;
			}

			var enemyStateCount = stream.EnemyStates?.Count ?? 0;
			if (enemyStateCount > ProtocolConstants.MaxStateStreamCollectionSize)
			{
				error = $"state-stream enemy-state count {enemyStateCount} exceeds limit {ProtocolConstants.MaxStateStreamCollectionSize}";
				return false;
			}
		}

		error = string.Empty;
		return true;
	}

	private static bool ValidateCheckpointMetadata(WireCheckpoint checkpoint, out string error)
	{
		if (checkpoint.ChunkCount <= 0)
		{
			error = $"checkpoint chunk count must be positive; found {checkpoint.ChunkCount}";
			return false;
		}

		if (checkpoint.ChunkCount > ProtocolConstants.MaxCheckpointChunks)
		{
			error = $"checkpoint chunk count {checkpoint.ChunkCount} exceeds limit {ProtocolConstants.MaxCheckpointChunks}";
			return false;
		}

		if (checkpoint.ChunkIndex < 0 || checkpoint.ChunkIndex >= checkpoint.ChunkCount)
		{
			error = $"checkpoint chunk index {checkpoint.ChunkIndex} is outside 0..{checkpoint.ChunkCount - 1}";
			return false;
		}

		var itemCount = checkpoint.Items?.Count ?? 0;
		if (itemCount > ProtocolConstants.CheckpointChunkItemCount)
		{
			error = $"checkpoint chunk item count {itemCount} exceeds per-chunk limit {ProtocolConstants.CheckpointChunkItemCount}";
			return false;
		}

		error = string.Empty;
		return true;
	}

	private static bool IsStateStreamPayloadType(WirePayloadType payloadType) =>
		payloadType switch
		{
			WirePayloadType.StateStream => true,
			WirePayloadType.ItemSnapshotStream => true,
			WirePayloadType.WorldItemsSnapshotStream => true,
			WirePayloadType.PlayerStateStream => true,
			WirePayloadType.EnemyStateStream => true,
			_ => false,
		};

	private static WireCommandKind? GetExpectedCommandKind(WirePayloadType payloadType) =>
		payloadType switch
		{
			WirePayloadType.ItemSpawnCommand => WireCommandKind.ItemSpawn,
			WirePayloadType.ItemPickupCommand => WireCommandKind.ItemPickup,
			WirePayloadType.ItemDropCommand => WireCommandKind.ItemDrop,
			WirePayloadType.ItemDestroyCommand => WireCommandKind.ItemDestroy,
			WirePayloadType.ItemUpdateStateCommand => WireCommandKind.ItemUpdateState,
			WirePayloadType.ItemTransferCommand => WireCommandKind.ItemTransfer,
			WirePayloadType.ItemContainerSyncCommand => WireCommandKind.ItemContainerSync,
			WirePayloadType.RunStartCommand => WireCommandKind.RunStart,
			WirePayloadType.AdvanceLayerCommand => WireCommandKind.AdvanceLayer,
			WirePayloadType.RecordTrapConsumedCommand => WireCommandKind.RecordTrapConsumed,
			WirePayloadType.RecordBuildingEntityHealthCommand => WireCommandKind.RecordBuildingEntityHealth,
			WirePayloadType.RecordOpenedEntityCommand => WireCommandKind.RecordOpenedEntity,
			WirePayloadType.ResetWorldEntitiesCommand => WireCommandKind.ResetWorldEntities,
			WirePayloadType.UpdatePlayerStatusCommand => WireCommandKind.UpdatePlayerStatus,
			WirePayloadType.ResetPlayersCommand => WireCommandKind.ResetPlayers,
			WirePayloadType.UpsertEnemyCommand => WireCommandKind.UpsertEnemy,
			WirePayloadType.RemoveEnemyCommand => WireCommandKind.RemoveEnemy,
			WirePayloadType.ResetEnemiesCommand => WireCommandKind.ResetEnemies,
			WirePayloadType.UpdateFluidRegionCommand => WireCommandKind.UpdateFluidRegion,
			WirePayloadType.ResetFluidsCommand => WireCommandKind.ResetFluids,
			WirePayloadType.SetPlayerCarryCommand => WireCommandKind.SetPlayerCarry,
			WirePayloadType.ClearPlayerCarryCommand => WireCommandKind.ClearPlayerCarry,
			WirePayloadType.RecordEnemyBiteCommand => WireCommandKind.RecordEnemyBite,
			WirePayloadType.RecordEnemyLungeCommand => WireCommandKind.RecordEnemyLunge,
			WirePayloadType.RecordEnemyEffectCommand => WireCommandKind.RecordEnemyEffect,
			WirePayloadType.RecordTrapStateCommand => WireCommandKind.RecordTrapState,
			WirePayloadType.RangeRequestCommand => WireCommandKind.RangeRequest,
			WirePayloadType.CommandRejected => WireCommandKind.CommandRejected,
			_ => null,
		};
}
