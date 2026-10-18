using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one player entity's high-frequency stream fields. The stream
/// carries convergent presentation state only; aggregate lifecycle
/// (join/leave) and terminal facts (alive/conscious, limb/body latches) travel
/// through dedicated domain events.
/// </summary>
[ProtoContract]
public sealed class WirePlayerStreamState
{
	[ProtoMember(1)]
	public WireEntityId EntityId { get; set; } = new();

	[ProtoMember(2)]
	public WireVector2 Position { get; set; } = new();

	[ProtoMember(3)]
	public WireVector2 LookPos { get; set; } = new();

	[ProtoMember(4)]
	public WireVector2? LookOverridePos { get; set; }

	[ProtoMember(5)]
	public float LookOverrideTime { get; set; }

	[ProtoMember(6)]
	public float EyeScareTime { get; set; }

	[ProtoMember(7)]
	public float EyePanicTime { get; set; }

	[ProtoMember(8)]
	public float EyeCloseTime { get; set; }

	[ProtoMember(9)]
	public WireVector2 Velocity { get; set; } = new();

	[ProtoMember(10)]
	public byte Flags { get; set; }

	/// <summary>
	/// Bit-packed flags the byte above cannot hold: <c>0x01</c> attacking, <c>0x02</c> wall-sliding
	/// left, <c>0x04</c> wall-sliding right, <c>0x08</c> in water, <c>0x10</c> moving, <c>0x20</c>
	/// exercising. The last three are the host clone's model inputs: the clone skips the native stage
	/// that computes in-water, and the movement/workout state exists only on the owner's own body, so
	/// the owner's verdict rides the stream.
	/// </summary>
	[ProtoMember(11)]
	public uint ExtendedFlags { get; set; }

	[ProtoMember(12)]
	public byte SwingSeq { get; set; }

	[ProtoMember(13)]
	public byte WorkoutType { get; set; }

	[ProtoMember(14)]
	public byte NapVariant { get; set; }

	[ProtoMember(15)]
	public float DogShakeIntensity { get; set; }

	/// <summary>
	/// Exact render-pose per limb while the owner is not standing and not
	/// sleeping (ragdoll/dead/unconscious). Null when standing/sleeping — the
	/// proxy uses its animator/nap clips instead. The frozen clone has no
	/// physics, so this is the only way its limb transforms match the owner's
	/// body. The pose values are world-space positions/rotations, not local
	/// offsets.
	/// </summary>
	[ProtoMember(16)]
	public List<WirePlayerLimbPose>? LimbPoses { get; set; }
}
