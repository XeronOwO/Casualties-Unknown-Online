using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One item in the character snapshot — the wire form of the official save's
/// SavedItem plus its component state: the generic SavedItem fields
/// (condition/favourited/slot) plus the <c>[Saveable]</c> component states
/// (Components), the WaterContainerItem liquid stacks (Liquids) and the
/// container contents (Contents, recursive — the official save flattens these
/// into extra SavedItem rows sharing the parent's slot; nesting is the
/// equivalent shape for the recursive restore).
/// </summary>
[ProtoContract]
public sealed class CharacterItemMsg
{
	[ProtoMember(1)]
	public string ItemId { get; set; } = ""; // definition id (ItemInfo.GlobalItems key)

	[ProtoMember(2)]
	public float Condition { get; set; }

	[ProtoMember(3)]
	public int SlotIndex { get; set; } // index into Body.slots (parent's slot for container contents)

	[ProtoMember(4)]
	public bool Favourited { get; set; }

	[ProtoMember(5)]
	public List<ComponentStateMsg> Components { get; set; } = [];

	[ProtoMember(6)]
	public List<CharacterItemMsg> Contents { get; set; } = []; // items inside a Container, recursive

	[ProtoMember(7)]
	public List<LiquidStackMsg> Liquids { get; set; } = []; // WaterContainerItem stacks
}
