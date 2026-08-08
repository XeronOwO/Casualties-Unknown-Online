using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: the guest's locally simulated state (no host-side
/// simulation). Same unreliable-stream semantics as <see cref="PlayerStateMsg"/>.
/// </summary>
[ProtoContract]
public sealed class PlayerStateReportMsg
{
	[ProtoMember(1)]
	public EntityStateMsg Entity { get; set; } = new();

	[ProtoMember(2)]
	public uint Seq { get; set; }
}
