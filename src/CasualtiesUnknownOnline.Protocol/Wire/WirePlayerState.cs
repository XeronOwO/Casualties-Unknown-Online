using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one authoritative player fact: terminal status, limb/body
/// latches, carry relation, and durable skill facts.
/// </summary>
[ProtoContract]
public sealed class WirePlayerState
{
	[ProtoMember(1)]
	public ulong SteamId { get; set; }

	[ProtoMember(2)]
	public bool Alive { get; set; }

	[ProtoMember(3)]
	public bool Conscious { get; set; }

	[ProtoMember(4)]
	public ulong CarrierOfSteamId { get; set; }

	[ProtoMember(5)]
	public ulong CarriedBySteamId { get; set; }

	[ProtoMember(6)]
	public List<WirePlayerLimbState> Limbs { get; set; } = [];

	[ProtoMember(7)]
	public WirePlayerBodyTerminalState? Body { get; set; }

	[ProtoMember(8)]
	public WirePlayerSkills? Skills { get; set; }
}
