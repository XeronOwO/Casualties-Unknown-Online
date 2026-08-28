using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// One field update in a state-stream envelope. Streams are convergent-only:
/// they may update existing continuous fields but never create/destroy
/// aggregates or change ownership.
/// </summary>
[ProtoContract]
public sealed class WireStreamField
{
	[ProtoMember(1)]
	public string Name { get; set; } = "";

	[ProtoMember(2)]
	public int Kind { get; set; }

	[ProtoMember(3)]
	public float FloatValue { get; set; }

	[ProtoMember(4)]
	public int IntValue { get; set; }

	[ProtoMember(5)]
	public bool BoolValue { get; set; }

	[ProtoMember(6)]
	public string StringValue { get; set; } = "";
}
