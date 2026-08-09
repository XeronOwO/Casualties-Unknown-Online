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
	/// <summary>Instance identity — the primary key, always first. 0 = no id (unbound/generation-time). The id follows the item through world ↔ inventory transfers (never re-allocated), which is what lets the host's arbitration target the exact instance.</summary>
	[ProtoMember(1)]
	public ulong InstanceId { get; set; }

	[ProtoMember(2)]
	public string ItemId { get; set; } = ""; // definition id (ItemInfo.GlobalItems key)

	[ProtoMember(3)]
	public float Condition { get; set; }

	[ProtoMember(4)]
	public int SlotIndex { get; set; } // index into Body.slots (parent's slot for container contents)

	[ProtoMember(5)]
	public bool Favourited { get; set; }

	[ProtoMember(6)]
	public List<ComponentStateMsg> Components { get; set; } = [];

	[ProtoMember(7)]
	public List<CharacterItemMsg> Contents { get; set; } = []; // items inside a Container, recursive

	[ProtoMember(8)]
	public List<LiquidStackMsg> Liquids { get; set; } = []; // WaterContainerItem stacks
}
