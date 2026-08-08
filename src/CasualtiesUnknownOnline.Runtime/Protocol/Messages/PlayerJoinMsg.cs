using CasualtiesUnknownOnline.Runtime.Session;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: join confirmation / roster announcement. Two modes on the
/// same wire shape — self-activation (GuestSteamId == the receiving guest;
/// the classic confirm-with-ids) and roster broadcast (GuestSteamId = another
/// guest; other members learn the new member's identity and spawn anchor).
/// The roster mode is what makes the star topology render every member on
/// every side without an envelope (membership = SteamId, world identity =
/// EntityId, kept in sync by this message).
/// </summary>
[ProtoContract]
public sealed class PlayerJoinMsg
{
	[ProtoMember(1)]
	public ulong HostSteamId { get; set; }

	[ProtoMember(2)]
	public NetworkEntityIdMsg HostEntityId { get; set; } = new();

	[ProtoMember(3)]
	public NetworkEntityIdMsg GuestEntityId { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg HostPosition { get; set; } = new();

	/// <summary>The joining guest's SteamId (roster mode; self-activation when equal to the receiver's).</summary>
	[ProtoMember(5)]
	public ulong GuestSteamId { get; set; }

	/// <summary>The joining guest's reported spawn anchor (roster mode).</summary>
	[ProtoMember(6)]
	public NetVector2Msg GuestPosition { get; set; } = new();

	/// <summary>Static factory — same pattern as SceneStateMsg.From: the message
	/// assembles itself from domain data, no service-level assembly. Host side
	/// comes first, then the joining guest.</summary>
	public static PlayerJoinMsg From(ulong hostSteamId, NetworkEntityId hostEntityId, NetVector2 hostPosition,
		ulong guestSteamId, NetworkEntityId guestEntityId, NetVector2 guestPosition) => new()
		{
			HostSteamId = hostSteamId,
			HostEntityId = hostEntityId.ToNetworkEntityIdMsg(),
			HostPosition = hostPosition.ToNetVector2Msg(),
			GuestSteamId = guestSteamId,
			GuestEntityId = guestEntityId.ToNetworkEntityIdMsg(),
			GuestPosition = guestPosition.ToNetVector2Msg(),
		};
}
