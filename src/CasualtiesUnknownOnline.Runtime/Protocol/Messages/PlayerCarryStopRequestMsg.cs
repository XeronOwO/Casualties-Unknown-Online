using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request to stop carrying a player. The requester must be the
/// current carrier; the host clears the relation and broadcasts the empty
/// carry state so every side releases the carried body.
/// </summary>
[ProtoContract]
public sealed class PlayerCarryStopRequestMsg
{
	/// <summary>The SteamId of the currently carried player being released.</summary>
	[ProtoMember(1)]
	public ulong CarriedSteamId { get; set; }
}
