using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Host → guest: a synced member left the session — remove the member and its clone.</summary>
[ProtoContract]
public sealed class PlayerLeaveMsg
{
	[ProtoMember(1)]
	public ulong SteamId { get; set; }

	[ProtoMember(2)]
	public NetworkEntityIdMsg EntityId { get; set; } = new();
}
