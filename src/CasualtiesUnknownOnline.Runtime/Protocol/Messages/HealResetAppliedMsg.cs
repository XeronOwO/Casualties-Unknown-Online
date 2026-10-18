using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Target client → host: the full reset that host instruction <see cref="InstructionId"/> ordered
/// was applied to THIS player's own body, and this is the body state that resulted. The plain
/// character snapshot filter would keep the host's copy of the three continuous values it owns (blood
/// volume, total bleeding speed and each limb's bleed), so the reset result cannot ride that report;
/// this event is the authority path, merged in place into the host's saved snapshot the way the
/// owner's limb event is (a revive writes a whole clone through <c>SaveCharacterData</c>; this event
/// carries only the body half, so it must not replace the rest).
///
/// <para>
/// Only the body half travels: the reset changes health and limbs, never items, skills, position or
/// native character fields, and those stay whatever the host already holds. The event is a CLAIM the
/// host bounds with facts it already holds: the payload's owner must be the transport sender, the
/// instruction id must be the one outstanding for that member (superseded, expired, unsolicited and
/// another-owner results are dropped by name), and the three continuous values must be the reset
/// table's own healthy constants — anything else is refused whole, by name, and the previous
/// snapshot stays.
/// </para>
/// </summary>
[ProtoContract]
public sealed class HealResetAppliedMsg
{
	/// <summary>The player whose body was reset (stamped by the target's own client; a host-side claim check compares it with the transport sender).</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The host's ticket for the instruction this result answers — echoed from <see cref="HealResetInstructionMsg.InstructionId"/>.</summary>
	[ProtoMember(2)]
	public ulong InstructionId { get; set; }

	/// <summary>The body's full post-reset health record, including the three values the host owns (the whole point of the event).</summary>
	[ProtoMember(3)]
	public CharacterHealthMsg? Health { get; set; }

	/// <summary>Every limb's full post-reset state — the reset rebuilds the whole limb set, never a delta.</summary>
	[ProtoMember(4)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];
}
