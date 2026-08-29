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
}
