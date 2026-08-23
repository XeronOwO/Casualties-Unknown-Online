using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// One entry in the protocol message registry: the wire id, its locked transport
/// direction and the protobuf payload type used by its handler. The payload type
/// is derived from the handler's <c>PacketHandlerBase&lt;TPacket&gt;</c> base
/// class, so a handler and its registry entry cannot silently disagree about the
/// wire payload.
/// </summary>
public readonly record struct NetMessageMetadata(NetMsg Msg, NetMessageDirection Direction, Type PayloadType)
{
	/// <summary>True when this message may be received by the given role.</summary>
	public bool IsValidFor(SessionRole role) => Direction switch
	{
		NetMessageDirection.GuestToHost => role == SessionRole.Host,
		NetMessageDirection.HostToGuest => role == SessionRole.Guest,
		NetMessageDirection.Bidirectional => true,
		_ => false,
	};
}
