using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Diagnostics round-trip probe (RTT measurement).</summary>
[ProtoContract]
public sealed class PingMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }
}

/// <summary>Reply to <see cref="PingMsg"/> — echoes the sender's tick.</summary>
[ProtoContract]
public sealed class PongMsg
{
	[ProtoMember(1)]
	public long Ticks { get; set; }
}

/// <summary>
/// Scene state exchange: guest → host as a report (host tracks the member's
/// scene), host → guest as a broadcast (host relays another member's change —
/// SteamId carries the reporter; host-originated changes carry the host's id).
/// Position carries the spawn anchor when entering the world.
/// </summary>
[ProtoContract]
public sealed class SceneStateMsg
{
	[ProtoMember(1)]
	public byte State { get; set; }

	[ProtoMember(2)]
	public string SceneName { get; set; } = "";

	// Always present on the wire ((0,0) when absent, mirroring the old layout).
	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>Reporter SteamId (host relay stamps it; 0 when sent by the reporter itself).</summary>
	[ProtoMember(4)]
	public ulong SteamId { get; set; }
}

/// <summary>Guest → host: protocol version + local scene state.</summary>
[ProtoContract]
public sealed class HandshakeMsg
{
	[ProtoMember(1)]
	public int Protocol { get; set; }

	[ProtoMember(2)]
	public SceneStateMsg Scene { get; set; } = new();
}

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
}
