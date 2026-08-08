using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

[ProtoContract]
public sealed class CharacterItemMsg
{
	[ProtoMember(1)]
	public string ItemId { get; set; } = ""; // definition id (ItemInfo.GlobalItems key)

	[ProtoMember(2)]
	public float Condition { get; set; }

	[ProtoMember(3)]
	public int SlotIndex { get; set; } // index into Body.slots
}
