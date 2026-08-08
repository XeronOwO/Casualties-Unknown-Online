using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the host's authoritative block-state snapshot — every block
/// whose state deviates from the generated baseline (mined/destroyed/built).
/// Sent when a guest reports InWorld (its generation finished) so it can apply
/// the accumulated world mutations without re-witnessing them (late-joiner
/// full snapshot, architecture.md).
/// </summary>
[ProtoContract]
public sealed class BlockStateMsg
{
	[ProtoMember(1)]
	public List<BlockStateEntryMsg> Blocks { get; set; } = [];
}
