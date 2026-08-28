using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// The outer wire frame for the Phase C protocol. Exactly one envelope is
/// present; the kind is part of the frame so receivers can reject unknown
/// kinds before decoding the body.
/// </summary>
[ProtoContract]
public sealed class ProtocolFrame
{
	[ProtoMember(1)]
	public EnvelopeKind Kind { get; set; }

	[ProtoMember(2)]
	public CommandEnvelope? Command { get; set; }

	[ProtoMember(3)]
	public CommittedBatchEnvelope? CommittedBatch { get; set; }

	[ProtoMember(4)]
	public CheckpointEnvelope? Checkpoint { get; set; }

	[ProtoMember(5)]
	public StateStreamEnvelope? StateStream { get; set; }
}
