using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Common header carried by every Phase C envelope. The header is deliberately
/// small and stable; domain-specific facts live in the typed payload.
/// </summary>
[ProtoContract]
public sealed class EnvelopeHeader
{
	[ProtoMember(1)]
	public int ProtocolVersion { get; set; }

	[ProtoMember(2)]
	public ulong RunEpoch { get; set; }

	[ProtoMember(3)]
	public ulong SenderId { get; set; }

	[ProtoMember(4)]
	public ulong MessageId { get; set; }

	[ProtoMember(5)]
	public ulong OperationId { get; set; }

	[ProtoMember(6)]
	public ulong BaseGlobalRevision { get; set; }

	[ProtoMember(7)]
	public WirePayloadType PayloadType { get; set; }
}
