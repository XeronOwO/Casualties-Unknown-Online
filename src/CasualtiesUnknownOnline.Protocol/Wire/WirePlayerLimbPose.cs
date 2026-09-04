using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one player limb's continuous render-pose fact carried by the
/// 20 Hz player state stream. <see cref="WorldPosition"/> is world space, not
/// local space: the frozen clone must place each visible limb exactly where
/// the owner's physics-driven rigidbody is in the world.
/// </summary>
[ProtoContract]
public sealed class WirePlayerLimbPose
{
	[ProtoMember(1)]
	public int Index { get; set; }

	[ProtoMember(2)]
	public WireVector2 WorldPosition { get; set; } = new();

	[ProtoMember(3)]
	public float RotationZ { get; set; }
}
