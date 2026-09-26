using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE world-item impact presentation the AUTHORITY side played natively. A
/// world item's collision presentation belongs to the side that simulates the
/// landing; on a guest that copy is suppressed
/// (<c>NonAuthoritativeItemImpactPolicy</c>), so without this event a guest
/// hears and sees nothing when a world item lands — its own drops included.
/// Host → guest only: the authority reports, every guest replays. The
/// presentation itself is transient (a one-shot clip plus a particle object
/// Unity destroys on its own), so no snapshot accompanies it.
/// </summary>
[ProtoContract]
public sealed class ItemImpactMsg
{
	/// <summary>The impact's world position (the item's own transform, exactly
	/// where the native call played the clip and spawned the dust).</summary>
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>Which native presentation to replay.</summary>
	[ProtoMember(2)]
	public ItemImpactKind Kind { get; set; }

	/// <summary>The plush's own sound index (<c>PlushScript.index</c>) — the
	/// receiver's copy rolls its own index, so the exact clip the authority
	/// played has to travel for the squeak to match.</summary>
	[ProtoMember(3)]
	public byte SoundIndex { get; set; }
}
