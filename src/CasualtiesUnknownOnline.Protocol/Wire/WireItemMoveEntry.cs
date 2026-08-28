using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one moving world item's continuous-position update.
/// </summary>
[ProtoContract]
public sealed class WireItemMoveEntry
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public float X { get; set; }

	[ProtoMember(3)]
	public float Y { get; set; }

	[ProtoMember(4)]
	public float VelX { get; set; }

	[ProtoMember(5)]
	public float VelY { get; set; }

	[ProtoMember(6)]
	public float Rotation { get; set; }

	[ProtoMember(7)]
	public float AngularVelocity { get; set; }
}
