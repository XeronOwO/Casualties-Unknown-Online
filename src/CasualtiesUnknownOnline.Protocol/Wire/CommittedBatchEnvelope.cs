using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Host -> guests: one atomic committed kernel batch (domain facts only).
/// </summary>
[ProtoContract]
public sealed class CommittedBatchEnvelope
{
	[ProtoMember(1)]
	public EnvelopeHeader Header { get; set; } = new();

	[ProtoMember(2)]
	public WireCommittedBatch Batch { get; set; } = new();
}
