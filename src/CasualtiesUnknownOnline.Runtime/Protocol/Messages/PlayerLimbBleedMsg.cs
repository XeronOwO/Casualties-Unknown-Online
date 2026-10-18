using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One limb's host-computed bleed amount inside <see cref="PlayerVitalsMsg"/>.</summary>
[ProtoContract]
public sealed class PlayerLimbBleedMsg
{
	/// <summary>The limb's index into <c>Body.limbs</c> (the same index space the snapshot uses).</summary>
	[ProtoMember(1)]
	public int Index { get; set; }

	/// <summary>The host-computed <c>Limb.bleedAmount</c>.</summary>
	[ProtoMember(2)]
	public float BleedAmount { get; set; }
}
