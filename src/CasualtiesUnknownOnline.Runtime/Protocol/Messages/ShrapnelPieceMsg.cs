using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One authoritative piece of a shared shrapnel session. The host owns the
/// mapping from native minigame slot to position, current lease owner and
/// removed state; every participating client mirrors this exact piece.
/// </summary>
[ProtoContract]
public sealed class ShrapnelPieceMsg
{
	[ProtoMember(1)]
	public int PieceIndex { get; set; }

	[ProtoMember(2)]
	public float X { get; set; }

	[ProtoMember(3)]
	public float Y { get; set; }

	[ProtoMember(4)]
	public ulong OwnerSteamId { get; set; }

	[ProtoMember(5)]
	public bool Removed { get; set; }
}
