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
	public const int CheckpointSchemaVersion = 1;

	/// <summary>Maximum checkpoint chunk item count (simple, deterministic batching).</summary>
	public const int CheckpointChunkItemCount = 256;

	/// <summary>Reserved range start for non-critical presentation payloads.</summary>
	public const int PresentationPayloadStart = 1000;
}
