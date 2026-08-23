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

	/// <summary>IP-direct: the logical peer id the host assigned/observed for this
	/// guest. 0 in Steam mode. The guest adopts this as its local peer id so the
	/// entity self-activation matches the host's roster view.</summary>
	[ProtoMember(4)]
	public ulong AssignedPeerId { get; set; }

	/// <summary>The host's display name (custom in IP-direct mode, Steam persona otherwise).</summary>
	[ProtoMember(5)]
	public string DisplayName { get; set; } = "";
}
