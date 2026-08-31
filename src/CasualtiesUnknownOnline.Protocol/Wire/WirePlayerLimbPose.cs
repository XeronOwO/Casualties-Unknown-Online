using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one player limb's continuous render-pose fact carried by the
/// 20 Hz player state stream.
/// </summary>
[ProtoContract]
public sealed class WirePlayerLimbPose
{
	[ProtoMember(1)]
	public int Index { get; set; }

	[ProtoMember(2)]
	public WireVector2 LocalPosition { get; set; } = new();

	[ProtoMember(3)]
	public float RotationZ { get; set; }
}
