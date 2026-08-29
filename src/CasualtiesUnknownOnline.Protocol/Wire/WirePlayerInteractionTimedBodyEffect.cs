using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of a timed body effect carried by a player item-use result event.
/// </summary>
[ProtoContract]
public sealed class WirePlayerInteractionTimedBodyEffect
{
	[ProtoMember(1)]
	public string EffectId { get; set; } = "";

	[ProtoMember(2)]
	public float DurationSeconds { get; set; }

	[ProtoMember(3)]
	public float DoseMl { get; set; }
}
