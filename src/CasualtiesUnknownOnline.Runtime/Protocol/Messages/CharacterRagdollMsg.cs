using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE player-character ragdoll-toggle presentation event (the player pressed
/// the game's ragdoll key and <c>Body.Ragdoll</c> actually collapsed the local
/// body). The owner's local simulation already entered the physics ragdoll;
/// this event lets the peers replay the lying pose on the owner's render clone
/// immediately instead of waiting for the 20 Hz standing flag to arrive (and
/// covers the case where the state packet carrying the flag is lost).
/// Star semantics: guest → host report, host fires the event and relays to the
/// other members (source excluded); host → guest relay fires the replay. One
/// collapse = one message; the 20 Hz entity-state stream remains the fallback.
/// </summary>
[ProtoContract]
public sealed class CharacterRagdollMsg
{
	/// <summary>The ragdolling player's SteamId (stamped by the reporter; the host stamps its own on broadcast).</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The world-space body position at the collapse (diagnostic anchor; the receiver uses the owner clone's live body when it exists).</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();
}
