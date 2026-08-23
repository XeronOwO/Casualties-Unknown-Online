using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the authoritative radiation-line state. The line's
/// <c>active</c> flag and <c>timeGone</c> descent are world state owned by the
/// host (the boundary must be the same on every side — a late joiner or a
/// guest whose own layer timer diverged would otherwise see a different
/// radiation boundary). The guest continues its local per-frame line
/// presentation between resends and re-aligns to this absolute state.
/// </summary>
[ProtoContract]
public sealed class RadiationLineStateMsg
{
	[ProtoMember(1)]
	public bool Active { get; set; }

	[ProtoMember(2)]
	public float TimeGone { get; set; }
}
