using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Host -> guests: a convergent high-frequency field update stream.
/// </summary>
[ProtoContract]
public sealed class StateStreamEnvelope
{
	[ProtoMember(1)]
	public EnvelopeHeader Header { get; set; } = new();

	[ProtoMember(2)]
	public WireStateStream Stream { get; set; } = new();
}
