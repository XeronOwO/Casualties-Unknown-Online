using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One serializable field of a <c>[Saveable]</c> item component (wire form of
/// the official save's component state). Kind-tagged, same layout pattern as
/// SettingEntryMsg: the receiver switches on Kind to pick the value. Only the
/// simple field kinds the components actually use are defined (float/int/bool/
/// string/List&lt;string&gt;); Unity-reference fields are never serialized.
/// </summary>
[ProtoContract]
public sealed class ComponentFieldMsg
{
	[ProtoMember(1)]
	public string Name { get; set; } = "";

	[ProtoMember(2)]
	public int Kind { get; set; } // 1=float 2=int 3=bool 4=string 5=List<string>

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
