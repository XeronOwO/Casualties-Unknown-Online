using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of a convergent state-stream update.
/// </summary>
[ProtoContract]
public sealed class WireStateStream
{
	[ProtoMember(1)]
	public ulong EntityId { get; set; }

	[ProtoMember(2)]
	public ulong BaseGlobalRevision { get; set; }

	[ProtoMember(3)]
	public List<WireStreamField> Fields { get; set; } = [];
}
