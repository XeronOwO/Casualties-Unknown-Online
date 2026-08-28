using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire/save form of a named deterministic random stream or decided-result set.
/// </summary>
[ProtoContract]
public sealed class WireRandomStream
{
	[ProtoMember(1)]
	public string Name { get; set; } = "";

	[ProtoMember(2)]
	public string State { get; set; } = "";

	[ProtoMember(3)]
	public List<ulong> DecidedValues { get; set; } = [];
}
