using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One trader stock entry (the game's TraderItem: id/value/preference/bought,
/// TraderItem.cs:16-26). Serialized inside <see cref="TraderStateMsg"/> — the
/// stock is host-authoritative and travels as part of the full state
/// overwrite. Preference is the byte value of
/// TraderScript.TraderItemPreference (0 WantsTrade / 1 Indifferent /
/// 2 WantsKeep, TraderScript.cs:972-980).
/// </summary>
[ProtoContract]
public sealed class TraderItemMsg
{
	[ProtoMember(1)]
	public string Id { get; set; } = "";

	[ProtoMember(2)]
	public int Value { get; set; }

	[ProtoMember(3)]
	public byte Preference { get; set; }

	[ProtoMember(4)]
	public bool Bought { get; set; }
}
