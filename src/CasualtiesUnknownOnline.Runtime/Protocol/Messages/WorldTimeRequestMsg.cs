using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: the guest's speed hotkey / movement-reset wants a world-time
/// speed (Normal/Fast/SuperFast only). The host applies its policy — a
/// request is refused/cleared while any in-world player is moving and never
/// overrides the all-unconscious sleep acceleration.
/// </summary>
[ProtoContract]
public sealed class WorldTimeRequestMsg
{
	[ProtoMember(1)]
	public WorldTimeSpeed Speed { get; set; }
}
