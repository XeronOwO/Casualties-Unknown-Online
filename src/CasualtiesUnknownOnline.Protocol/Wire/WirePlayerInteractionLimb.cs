using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one post-interaction limb snapshot carried by a
/// player-interaction result event.
/// </summary>
[ProtoContract]
public sealed class WirePlayerInteractionLimb
{
	[ProtoMember(1)]
	public int Index { get; set; }

	[ProtoMember(2)]
	public float SkinHealth { get; set; }

	[ProtoMember(3)]
	public float MuscleHealth { get; set; }

	[ProtoMember(4)]
	public bool Broken { get; set; }

	[ProtoMember(5)]
	public bool Dislocated { get; set; }

	[ProtoMember(6)]
	public bool Splinted { get; set; }

	[ProtoMember(7)]
	public bool Infected { get; set; }

	[ProtoMember(8)]
	public float InfectionAmount { get; set; }

	[ProtoMember(9)]
	public float BleedAmount { get; set; }

	[ProtoMember(10)]
	public float DisinfectionTime { get; set; }

	[ProtoMember(11)]
	public float Pain { get; set; }

	[ProtoMember(12)]
	public float DislocationTimer { get; set; }

	[ProtoMember(13)]
	public float BoneHealTimer { get; set; }

	[ProtoMember(14)]
	public bool BlockedBleeding { get; set; }

	[ProtoMember(15)]
	public int Shrapnel { get; set; }

	[ProtoMember(16)]
	public float FurBloodAmount { get; set; }

	[ProtoMember(17)]
	public float BandageSlowAmount { get; set; }

	[ProtoMember(18)]
	public float SkinHealAmount { get; set; }

	[ProtoMember(19)]
	public bool Dismembered { get; set; }

	[ProtoMember(20)]
	public List<WireComponentState> Components { get; set; } = [];

	[ProtoMember(21)]
	public bool IsHead { get; set; }

	[ProtoMember(22)]
	public bool IsVital { get; set; }
}
