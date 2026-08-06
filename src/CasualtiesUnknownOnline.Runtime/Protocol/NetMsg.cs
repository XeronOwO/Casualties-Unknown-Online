namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// CUO wire message IDs. Frame format over SteamTransport: [msgId:1][payload],
/// payload length is implicit (rest of the frame). Payloads are BinaryWriter-encoded.
/// </summary>
public enum NetMsg : byte
{
	// Diagnostics
	Ping = 1,
	Pong = 2,

	// Session control
	Handshake = 16,
	HandshakeAck = 17,
	SceneState = 18,
	WorldStartParams = 19,

	// Entities
	PlayerJoin = 32,
	PlayerLeave = 33,
	PlayerState = 35,
	PlayerStateReport = 36, // guest → host: local authoritative position (no host-side simulation)
}

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 1;
}
