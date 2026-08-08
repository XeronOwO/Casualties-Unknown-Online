using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A world item the SENDER generated has settled (its velocity dropped to
/// ~zero): the generator's physics is the position authority, so the host
/// updates the table entry and aligns its own phantom to this spot (instead
/// of the receiver-side drift, which diverges — "fell through the world" /
/// "pulled back" bugs). Reliable: a settle is a terminal state worth
/// guaranteeing; the host's periodic keyframe stays unreliable.
/// </summary>
[ProtoContract]
public sealed class ItemSettleMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public NetVector2Msg? Position { get; set; }

	[ProtoMember(3)]
	public float Rotation { get; set; }
}
