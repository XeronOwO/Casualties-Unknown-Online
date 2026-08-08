using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

[ProtoContract]
public sealed class CharacterSkillsMsg
{
	[ProtoMember(1)]
	public int Strength { get; set; }

	[ProtoMember(2)]
	public int Resistance { get; set; }

	[ProtoMember(3)]
	public int Intelligence { get; set; }

	[ProtoMember(4)]
	public float ExpStrength { get; set; }

	[ProtoMember(5)]
	public float ExpResistance { get; set; }

	[ProtoMember(6)]
	public float ExpIntelligence { get; set; }
}
