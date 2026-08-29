using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one authoritative player terminal-status fact.
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
}
