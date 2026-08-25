using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for the "push another player" direct interaction: the
/// local player wants to shove an in-world target. The host is the cross-player
/// authority — it validates both participants, the distance and the pusher's
/// cooldown against its authoritative entity/character state, computes the
/// force direction and broadcasts the authoritative push result to every side.
/// <para>
/// The acting side does not apply anything locally before the host responds;
/// the host's result is the single committed fact.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerPushRequestMsg
{
	/// <summary>The SteamId of the player being pushed.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }
}
