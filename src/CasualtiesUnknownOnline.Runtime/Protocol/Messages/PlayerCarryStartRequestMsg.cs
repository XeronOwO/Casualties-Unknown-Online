using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for the "carry another player" direct interaction:
/// the local player (carrier) wants to pick up one in-world target who is
/// unconscious or dead. The host is the cross-player authority — it validates
/// the target against its authoritative character snapshot, records the carry
/// relation and tells every member to apply the new visual/motion state.
/// </summary>
[ProtoContract]
public sealed class PlayerCarryStartRequestMsg
{
	/// <summary>The SteamId of the player to be carried.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }
}
