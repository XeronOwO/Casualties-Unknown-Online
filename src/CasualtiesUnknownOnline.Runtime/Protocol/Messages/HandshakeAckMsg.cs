using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Host → guest: handshake completion (acknowledges every handshake, even repeats).</summary>
[ProtoContract]
public sealed class HandshakeAckMsg
{
	[ProtoMember(1)]
	public int Protocol { get; set; }

	[ProtoMember(2)]
	public SceneStateMsg Scene { get; set; } = new();

	[ProtoMember(3)]
	public bool HasWorldParams { get; set; }

	/// <summary>Static factory — same pattern as SceneStateMsg.From: the message
	/// assembles itself from domain data, no service-level assembly.</summary>
	public static HandshakeAckMsg From(int protocol, SceneStateMsg scene, bool hasWorldParams) => new()
	{
		Protocol = protocol,
		Scene = scene,
		HasWorldParams = hasWorldParams,
	};
}
