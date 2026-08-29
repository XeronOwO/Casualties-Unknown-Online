using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one player limb's discrete terminal latch facts.
/// </summary>
[ProtoContract]
public sealed class WirePlayerLimbState
{
	[ProtoMember(1)]
	public int Index { get; set; }

	[ProtoMember(2)]
	public bool Broken { get; set; }

	[ProtoMember(3)]
	public bool Dismembered { get; set; }

	[ProtoMember(4)]
	public bool Dislocated { get; set; }

	[ProtoMember(5)]
	public bool Splinted { get; set; }

	[ProtoMember(6)]
	public bool Infected { get; set; }

	[ProtoMember(7)]
	public bool BlockedBleeding { get; set; }

	[ProtoMember(8)]
	public bool IsHead { get; set; }

	[ProtoMember(9)]
	public bool IsVital { get; set; }
}
