using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of a timed limb effect carried by a player item-use result event.
/// </summary>
[ProtoContract]
public sealed class WirePlayerInteractionTimedLimbEffect
{
	[ProtoMember(1)]
	public int LimbIndex { get; set; }

	[ProtoMember(2)]
	public float DurationSeconds { get; set; }

	[ProtoMember(3)]
	public float BleedPerSecond { get; set; }
}
