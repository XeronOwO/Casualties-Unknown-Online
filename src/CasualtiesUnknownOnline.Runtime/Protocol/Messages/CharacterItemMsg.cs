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

	/// <summary>Static factory — same pattern as SceneStateMsg.From: the message
	/// assembles itself from domain data, no service-level assembly.</summary>
	public static CharacterItemMsg From(string itemId, float condition, int slotIndex) => new()
	{
		ItemId = itemId,
		Condition = condition,
		SlotIndex = slotIndex,
	};
}
