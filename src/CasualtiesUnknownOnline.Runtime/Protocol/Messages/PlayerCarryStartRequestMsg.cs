using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for the "carry another player"/"piggyback" direct
/// interaction: the local player (carrier) wants to pick up one in-world
/// target. <see cref="Piggyback"/> selects the conscious-alive ride mode;
/// otherwise the target must be unconscious or dead (the original carry rule).
/// The host is the cross-player authority — it validates the target against its
/// authoritative character snapshot, records the carry relation and tells
/// every member to apply the same visual/motion state.
/// </summary>
[ProtoContract]
public sealed class PlayerCarryStartRequestMsg
{
	/// <summary>The SteamId of the player to be carried.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>True for a conscious/alive piggyback ride; false for the classic unconscious/dead carry.</summary>
	[ProtoMember(2)]
	public bool Piggyback { get; set; }

	/// <summary>
	/// Only meaningful when <see cref="Piggyback"/> is true. False (the wire
	/// default for old peers) means the requester climbs onto the target's back
	/// (target becomes the carrier). True means the requester is the carrier and
	/// invites the target to ride on the requester's back.
	/// </summary>
	[ProtoMember(3)]
	public bool RequesterIsCarrier { get; set; }
}
