namespace CasualtiesUnknownOnline.Protocol.Versioning;

/// <summary>
/// Version constants for the Phase C protocol and checkpoint schema. These are
/// independent of the legacy Runtime.Protocol.ProtocolVersion enum.
/// </summary>
public static class ProtocolConstants
{
	/// <summary>Version of the envelope header/frame layout.</summary>
	public const int EnvelopeVersion = 1;

	/// <summary>Version of the wire checkpoint schema.</summary>
	public const int CheckpointSchemaVersion = 2;

	/// <summary>Maximum checkpoint chunk item count (simple, deterministic batching).</summary>
	public const int CheckpointChunkItemCount = 256;

	/// <summary>Maximum checkpoint chunks accepted for one restore.</summary>
	public const int MaxCheckpointChunks = 10_000;

	/// <summary>Maximum events or preconditions accepted in one committed batch.</summary>
	public const int MaxCommittedBatchEvents = 10_000;

	/// <summary>Maximum entries in one state-stream collection.</summary>
	public const int MaxStateStreamCollectionSize = 100_000;

	/// <summary>Maximum container children accepted in one command.</summary>
	public const int MaxCommandContainerChildren = 10_000;

	/// <summary>Reserved range start for non-critical presentation payloads.</summary>
	public const int PresentationPayloadStart = 1000;
}
