using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Guest → host: protocol version + local scene state.</summary>
[ProtoContract]
public sealed class HandshakeMsg
{
	[ProtoMember(1)]
	public int Protocol { get; set; }

	[ProtoMember(2)]
	public SceneStateMsg Scene { get; set; } = new();
}
