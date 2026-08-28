using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one typed item component state.
/// </summary>
[ProtoContract]
public sealed class WireComponentState
{
	[ProtoMember(1)]
	public string TypeName { get; set; } = "";

	[ProtoMember(2)]
	public List<WireComponentField> Fields { get; set; } = [];
}
