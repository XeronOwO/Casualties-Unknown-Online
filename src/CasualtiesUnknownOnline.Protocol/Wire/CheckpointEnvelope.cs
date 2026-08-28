using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Host -> guest: one checkpoint chunk during join/reconnect or gap recovery.
/// </summary>
[ProtoContract]
public sealed class CheckpointEnvelope
{
	[ProtoMember(1)]
	public EnvelopeHeader Header { get; set; } = new();

	[ProtoMember(2)]
	public WireCheckpoint Checkpoint { get; set; } = new();
}
