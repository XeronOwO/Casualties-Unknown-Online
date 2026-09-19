using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: a speed the guest's own client has already applied (hotkey, or
/// the native left/right movement reset), Normal/Fast/SuperFast only. The host
/// arbitrates ACCEPT FIRST — only a request it cannot represent is refused — and
/// answers with the authoritative speed; the all-unconscious sleep acceleration
/// still owns the clock when it applies.
/// </summary>
[ProtoContract]
public sealed class WorldTimeRequestMsg
{
	[ProtoMember(1)]
	public WorldTimeSpeed Speed { get; set; }
}
