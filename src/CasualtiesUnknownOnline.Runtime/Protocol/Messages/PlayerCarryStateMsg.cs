using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → every member: the authoritative carry relation changed. A zero
/// <see cref="CarriedSteamId"/> means the carrier is no longer carrying anyone.
/// One operation = one message; the carried body's own client follows the
/// carrier's reported position, so all other peers need only this state for
/// UI and local driver setup — the position stream already carries the result.
/// </summary>
[ProtoContract]
public sealed class PlayerCarryStateMsg
{
	/// <summary>The SteamId of the player doing the carrying.</summary>
	[ProtoMember(1)]
	public ulong CarrierSteamId { get; set; }

	/// <summary>The SteamId of the carried player, or 0 when this carrier released everyone.</summary>
	[ProtoMember(2)]
	public ulong CarriedSteamId { get; set; }
}
