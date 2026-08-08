using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One <c>[Saveable]</c> item component's serialized state — the wire form of
/// the official save's per-item component dictionaries. TypeName identifies
/// the component (matched by name on restore); Fields carry the simple-typed
/// state. WaterContainerItem's liquid stacks travel separately on
/// CharacterItemMsg.Liquids (the stack field is private; restored through the
/// public AddLiquid API).
/// </summary>
[ProtoContract]
public sealed class ComponentStateMsg
{
	[ProtoMember(1)]
	public string TypeName { get; set; } = "";

	[ProtoMember(2)]
	public List<ComponentFieldMsg> Fields { get; set; } = [];
}
