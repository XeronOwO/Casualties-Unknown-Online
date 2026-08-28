using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Guest -> host: an intent or native observation expressed as a typed command.
/// </summary>
[ProtoContract]
public sealed class CommandEnvelope
{
	[ProtoMember(1)]
	public EnvelopeHeader Header { get; set; } = new();

	[ProtoMember(2)]
	public WireCommand Command { get; set; } = new();
}
