using CasualtiesUnknownOnline.Runtime.Session;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

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

	/// <summary>Static factory — same pattern as EntityStateMsg.From: the message
	/// assembles itself from domain data, no service-level assembly.</summary>
	public static SceneStateMsg From(SceneStateType state, string sceneName, NetVector2 position, ulong steamId = 0) => new()
	{
		State = (byte)state,
		SceneName = sceneName,
		Position = NetVector2Msg.From(position),
		SteamId = steamId,
	};
}
