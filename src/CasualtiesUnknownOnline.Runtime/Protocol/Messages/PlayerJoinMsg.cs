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

	/// <summary>The joining guest's custom display name (IP-direct sessions; Steam sessions fall back to persona).</summary>
	[ProtoMember(7)]
	public string DisplayName { get; set; } = "";
}
