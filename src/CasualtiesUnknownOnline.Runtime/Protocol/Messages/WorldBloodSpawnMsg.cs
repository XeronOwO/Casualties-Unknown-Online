using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE world-blood decal spawned by a player's BleedParticle (blood leaving the
/// body into the world). The owner side already ran the native <c>BleedParticle
/// Update</c> branch and created the local ground/wall decal; this event lets
/// the other members instantiate the same decal at the same world position.
/// Star semantics: guest → host report, host fires the received event and
/// relays to the other members (source excluded); host → guest relay fires the
/// replay. The visual itself is transient (Unity destroys it after 120 s) so
/// no periodic snapshot is used.
/// </summary>
[ProtoContract]
public sealed class WorldBloodSpawnMsg
{
	/// <summary>The decal's world position (for ground blood this is the
	/// block-snapped cell centre, matching what the native source spawned).</summary>
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>True for a ground decal (<c>Special/blockblood</c> + GroundBlood),
	/// false for a wall decal (<c>wallblood</c>).</summary>
	[ProtoMember(2)]
	public bool Ground { get; set; }
}
