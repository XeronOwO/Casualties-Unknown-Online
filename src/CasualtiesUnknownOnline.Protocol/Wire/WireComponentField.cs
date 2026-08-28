using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// One simple-typed field inside a component state.
/// </summary>
[ProtoContract]
public sealed class WireComponentField
{
	[ProtoMember(1)]
	public string Name { get; set; } = "";

	[ProtoMember(2)]
	public int Kind { get; set; }

	[ProtoMember(3)]
	public float FloatValue { get; set; }

	[ProtoMember(4)]
	public int IntValue { get; set; }

	[ProtoMember(5)]
	public bool BoolValue { get; set; }

	[ProtoMember(6)]
	public string StringValue { get; set; } = "";

	[ProtoMember(7)]
	public List<string> StringList { get; set; } = [];
}
