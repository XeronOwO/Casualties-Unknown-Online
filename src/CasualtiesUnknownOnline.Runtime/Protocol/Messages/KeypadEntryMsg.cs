using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One keypad's code: the Openable's world position (deterministic entities — both sides have the same object at the same place) and the host-generated code.</summary>
[ProtoContract]
public sealed class KeypadEntryMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(2)]
	public string Code { get; set; } = "";
}
