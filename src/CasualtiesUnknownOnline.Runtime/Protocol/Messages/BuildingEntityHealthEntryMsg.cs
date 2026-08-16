using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One damaged building entity in the late-joiner health snapshot. X/Y are the
/// world position (the entity's identity — building entities are generated
/// deterministically), Health is the current authoritative health. Floats are
/// safe from protobuf's zero-omission: an omitted 0 decodes to the same 0.
/// </summary>
[ProtoContract]
public sealed class BuildingEntityHealthEntryMsg
{
	[ProtoMember(1)]
	public float X { get; set; }

	[ProtoMember(2)]
	public float Y { get; set; }

	[ProtoMember(3)]
	public float Health { get; set; }
}
